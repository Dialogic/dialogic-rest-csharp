using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using MCantale.Helpers;
using Syzygy.Core.Logging;

namespace MCantale.XMS
{
   #region CallCollection class

   public class CallCollection<T> : ObservableCollection<T> where T : CallBase, new()
   {
   }

   #endregion

   #region interface ICallDispatcher

   public interface ICallDispatcher<out T> where T : CallBase
   {
      void DeleteCall(string ResourceID);
      void Hangup(string ResourceID);
      void MakeCall(string DialledString);
      void ProcessRequest(RESTapi.web_service ws);
   }

   #endregion

   public sealed class CallDispatcher<T> : ICallDispatcher<T>
      where T : CallBase, new()
   {
      #region Fields and variables

      public static CallDispatcher<T> Instance
      {
         get { return Nested.instance; }
      }

      public CallCollection<T> Calls { get; set; }

      #region class Nested

      class Nested
      {
         static Nested()
         {
         }

         internal static readonly CallDispatcher<T> instance = new CallDispatcher<T>();
      }

      #endregion

      #endregion

      #region Constructor

      CallDispatcher()
      {
         this.Calls = new CallCollection<T>();
      }

      ~CallDispatcher()
      {
         /////////////////////////////////////////////////////////////
         /// Hangup / cleanup any existing calls on this appID...
         /// 
         // CallDispatcher.Instance.GetCalls();

         List<String> resourceids = CallDispatcher<T>.Instance.Calls.Select(x => x.ResourceID).ToList();

         foreach (String resourceid in resourceids)
         {
            CallDispatcher<T>.Instance.DeleteCall(resourceid);
         }
      }

      #endregion

      /// <summary>
      /// We received a web_service object to process... this function will 
      /// determine if the request is for a new call or an existing call. 
      /// Additionally, if the request is a hangup, we should delete the call
      /// from our list.
      /// </summary>
      /// <param name="ws"> The web_service request to process</param>
      /// 
      public void ProcessRequest(RESTapi.web_service ws)
      {
         T currentCall = null;

         RESTapi.@event @event = ws.Item as RESTapi.@event;

         if (@event.type == RESTapi.event_type.keepalive)
         {
            LoggingSingleton.Instance.Message(LogType.Library, LogLevel.Debug1,
                                              "CallDispatcher::ProcessRequest : Keepalive received");
            return;
         }

         String response = RestHelpers.RESTapiToXML(ws, typeof(RESTapi.web_service));

         LoggingSingleton.Instance.Message(LogType.Library, LogLevel.Debug1,
                                           "CallDispatcher::ProcessRequest : Received web service request :\n{0}",
                                           response);

         if (@event.type == RESTapi.event_type.incoming)
         {
            /// Note that we probably should check that this is not an existing 
            /// call / resource id rather than assuming...
            /// 
            currentCall = new T()
            {
               Dispatcher   = this as ICallDispatcher<CallBase>,
               Direction    = "inbound",
               ResourceID   = @event.resource_id,
               ResourceType = @event.resource_type,
               CallState    = CallBase.CallStateType.Ringing,
            };

            foreach (RESTapi.event_data data in @event.event_data)
            {
               switch (data.name.ToLower())
               {
                  case "caller_uri": currentCall.CallerUri = data.value; break;
                  case "called_uri": currentCall.CalledUri = data.value; break;
                  case "uri":        currentCall.Uri       = data.value; break;
                  case "name":       currentCall.Name      = data.value; break;
               }
            }
         }
         else
         {
            foreach (T call in Calls)
            {
               if (call.ResourceID == @event.resource_id)
               {
                  currentCall = call;
                  break;
               }
            }
         }

         if (currentCall != null)
         {
            /// The following is a way of calling a protected / private member
            /// of a class - kind of like using "friend class" in C++. It uses
            /// reflection.
            /// 
            MethodInfo handleEventMethod = currentCall.GetType().GetMethod("HandleEvent", BindingFlags.Instance | BindingFlags.NonPublic);

            switch (@event.type)
            {
               case RESTapi.event_type.incoming:
                  Calls.Add(currentCall);
                  handleEventMethod.Invoke(currentCall, new object[] { ws });
                  break;

               case RESTapi.event_type.hangup:
                  handleEventMethod.Invoke(currentCall, new object[] { ws });
                  Calls.Remove(currentCall);
                  break;

               default:
                  handleEventMethod.Invoke(currentCall, new object[] { ws });
                  break;
            }
         }
      } /* ProcessRequest() */

      /// <summary>
      /// Gets a list of any calls that are already in progress on the XMS server. We can
      /// use this to populate our calls list and potentially do "stuff" with them...
      /// </summary>
      /// 
      public void GetCalls()
      {
         String uri = String.Format("http://{0}:{1}/default/calls?appid={2}",
                                    RestSettings.Instance.ServerIP,
                                    RestSettings.Instance.ServerPort,
                                    RestSettings.Instance.AppID);

         String responseString = String.Empty;

         if (RestHelpers.SendHttpRequest(out responseString, uri, "GET"))
         {
            LoggingSingleton.Instance.Message(LogType.Library, LogLevel.Debug1,
                                              "CallDispatcher::GetCalls : Received web service request :\n{0}",
                                              responseString);

            RESTapi.web_service    ws        = (RESTapi.web_service) RestHelpers.XMLToRESTapi(responseString, typeof(RESTapi.web_service));
            RESTapi.calls_response Responses = (RESTapi.calls_response) ws.Item;

            int numCalls = 0;

            if (!int.TryParse(Responses.size, out numCalls))
            {
               LoggingSingleton.Instance.Message(LogType.Library, LogLevel.Error,
                                                 "CallDispatcher::GetCalls : Oops... RESTapi.calls_response.size is not a number!");
               return;
            }

            LoggingSingleton.Instance.Message(LogType.Library, LogLevel.Info,
                                              "CallDispatcher::GetCalls : Found {0} RESTapi calls",
                                              numCalls);

            if (numCalls > 0 && Responses.call_response != null)
            {
               /// Each "call_response" object corresponds to a single call in progress.
               ///
               foreach (RESTapi.call_response Response in Responses.call_response)
               {
                  bool Exists = false;

                  foreach (T call in Calls)
                  {
                     if (call.ResourceID == Response.identifier)
                     {
                        Exists = true;
                        break;
                     }
                  }

                  if (!Exists)
                  {
                     this.Calls.Add(new T()
                     {
                        Dispatcher = this as ICallDispatcher<CallBase>,
                        ResourceID = Response.identifier,
                        CalledUri  = Response.destination_uri,
                        CallerUri  = Response.source_uri,
                        Direction  = Response.call_type.ToString(),
                        Uri        = Response.destination_uri,
                        Name       = Response.appid,
                        CallState  = Response.connected == RESTapi.boolean_type.yes ? CallBase.CallStateType.Connected : CallBase.CallStateType.Unknown,
                     });
                  }
               }
            }
         }
      } /* GetCalls() */

      private T GetCallByResourceID(string ResourceID)
      {
         /// Find the call object in our calls list...
         /// 
         foreach (T call in this.Calls)
         {
            if (call.ResourceID == ResourceID)
            {
               return call;
            }
         }

         return null;
      } /* GetCallByResourceID() */

      public void DeleteCall(string ResourceID)
      {
         String requestUri = String.Format("http://{0}:{1}/default/calls/{2}?appid={3}",
                                           RestSettings.Instance.ServerIP,
                                           RestSettings.Instance.ServerPort,
                                           ResourceID,
                                           RestSettings.Instance.AppID);

         T currentCall = GetCallByResourceID(ResourceID);

         if (currentCall != null && !currentCall.IsDeleted)
         {
            String responseString = String.Empty;

            if (RestHelpers.SendHttpRequest(out responseString, requestUri, "DELETE"))
            {
               currentCall.IsDeleted = true;

               LoggingSingleton.Instance.Message(LogType.Library, LogLevel.Debug1, "CallDispatcher::Hangup : Call resource deleted");

               this.Calls.Remove(currentCall);
               currentCall.Dispose();
            }
         }
      } /* DeleteCall() */

      public void Hangup(string ResourceID)
      {
         String requestUri = String.Format("http://{0}:{1}/default/calls/{2}?appid={3}",
                                           RestSettings.Instance.ServerIP,
                                           RestSettings.Instance.ServerPort,
                                           ResourceID,
                                           RestSettings.Instance.AppID);

         T currentCall = GetCallByResourceID(ResourceID);

         if (currentCall != null)
         {
            RESTapi.web_service ws = new RESTapi.web_service()
            {
               Item = new RESTapi.call()
               {
                  call_action = new RESTapi.call_action()
                  {
                     Item = new RESTapi.hangup()
                     {
                        content_type = "text/plain",
                        content      = "data",
                     }
                  }
               }
            };

            LoggingSingleton.Instance.Message(LogType.Library, LogLevel.Debug1, "CallDispatcher::Hangup : Hanging up call with ID \"{0}\"...", ResourceID);

            String responseString = String.Empty;

            /// Send the hangup request...
            /// 
            if (RestHelpers.SendHttpRequest(out responseString, requestUri, "PUT", ws))
            {
               LoggingSingleton.Instance.Message(LogType.Library, LogLevel.Debug1, "CallDispatcher::Hangup : Hangup OK");
               LoggingSingleton.Instance.Message(LogType.Library, LogLevel.Debug1, responseString);
            }
            else
            {
               LoggingSingleton.Instance.Message(LogType.Library, LogLevel.Error, "CallDispatcher::Hangup : Hangup failed!");
            }

            /// Remember to delete the call resource...
            /// 
            DeleteCall(ResourceID);
         }
      } /* Hangup() */

      public void MakeCall(string DialledString)
      {
          /// Not implemented...
          /// 
      } /* MakeCall() */
   }
}
