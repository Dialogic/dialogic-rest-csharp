using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using MCantale.Helpers;
using Syzygy.Core.Logging;

namespace MCantale.XMS
{
   #region enum ConnectionStateEnum

   public enum ConnectionStateEnum
   {
      [Description("Uninitialized")]
      [ShortName("N")]
      Null,
      [Description("Idle / disconnected from server")]
      [ShortName("I")]
      Idle,
      [Description("Trying to connect to XMS server...")]
      [ShortName("T")]
      Connecting,
      [Description("Connected to XMS server")]
      [ShortName("C")]
      Connected,
      [Description("Disconnecting from XMS server...")]
      [ShortName("D")]
      Disconnecting,
      [Description("Connection failed!")]
      [ShortName("X")]
      Failed,
   }

   #endregion

   #region class EventDispatcherList

   public class EventDispatcherList<T> : List<EventDispatcher<T> > where T : CallBase, new()
   {
   }

   #endregion

   public class EventDispatcher<T> : INotifyPropertyChanged, IDisposable where T : CallBase, new()
   {
      #region Fields and variables

      private ConnectionStateEnum _ConnectionState         = ConnectionStateEnum.Null;
      private Thread              _WorkerThread            = null;
      private String              _EventHandlerURI         = String.Empty;
      private String              _OID                     = String.Empty;
      private bool                _DeleteAllCallsOnConnect = true;
      
      public ConnectionStateEnum ConnectionState
      {
         get { return _ConnectionState; }
         private set { if (_ConnectionState != value) { _ConnectionState = value; RaisePropertyChanged("ConnectionState"); } }
      }

      public bool DeleteAllCallsOnConnect 
      {
         get { return _DeleteAllCallsOnConnect; }
         set { if (_DeleteAllCallsOnConnect != value) { _DeleteAllCallsOnConnect = value; RaisePropertyChanged("DeleteAllCallsOnConnect"); } }
      }

      public delegate void ConnectionStateChangedEventHandler(ConnectionStateEnum e);
      public event ConnectionStateChangedEventHandler ConnectionStateChanged;

      #region INotifyPropertyChanged implementation

      private void RaisePropertyChanged(String propertyName)
      {
         if (PropertyChanged != null)
         {
            PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
         }
      }

      public event PropertyChangedEventHandler PropertyChanged;

      #endregion

      #endregion

      #region Constructor / Destructor

      public EventDispatcher()
      {
         this.PropertyChanged        += this.OnPropertyChanged;
         this.ConnectionStateChanged += this.OnConnectionStateChanged;
         this.ConnectionState         = ConnectionStateEnum.Idle;
      }

      #region IDisposable implementation

      public void Dispose()
      {
         Dispose(true);
         GC.SuppressFinalize(true);
      } /* Dispose() */

      #endregion

      ~EventDispatcher()
      {
         Dispose(false);
      }

      void Dispose(bool Disposing)
      {
         if (Disposing)
         {
            /// Managed resources...
            ///
            if (this.ConnectionState != ConnectionStateEnum.Idle)
            {
               this.InternalStop(null);
            }
         }

         /// Unmanaged resources...
         /// 
      }

      #endregion

      public void Start()
      {
         /// This queues a function for execution. We need to use something 
         /// like this in a GUI application to keep the UI responsive.
         /// 
         ThreadPool.QueueUserWorkItem(InternalStart);
      }

      public void Stop()
      {
         ThreadPool.QueueUserWorkItem(InternalStop);
      }

      public bool WaitForStop(int timeout = 2000)
      {
         if (!this._WorkerThread.Join(timeout))
         {
            this._WorkerThread.Abort();
            return false;
         }

         return true;
      }

      private void InternalStart(object state)
      {
         if (this.ConnectionState != ConnectionStateEnum.Idle)
         {
            throw new InvalidStateException();
         }

         this.ConnectionState = ConnectionStateEnum.Connecting;

         this._WorkerThread = new Thread(InternalRunThread);
         this._WorkerThread.IsBackground = true;
         this._WorkerThread.Start();
      }

      private void InternalStop(object state)
      {
         if (this.ConnectionState == ConnectionStateEnum.Idle || this._WorkerThread == null)
         {
            return;
         }

         /// Terminate any calls that are in progress...
         /// 
         // CallDispatcher<T>.Instance.GetCalls();

         while (CallDispatcher<T>.Instance.Calls.Count > 0)
         {
            CallDispatcher<T>.Instance.DeleteCall(CallDispatcher<T>.Instance.Calls[0].ResourceID);
         }

         LoggingSingleton.Instance.Message(LogType.Library, LogLevel.Debug2,
                                           "EventDispatcher::InternalStop : Interrupting InternalRunThread...");

         this._WorkerThread.Interrupt();

         LoggingSingleton.Instance.Message(LogType.Library, LogLevel.Debug2, 
                                           "EventDispatcher::InternalStop : Attempting to join InternalRunThread...");

         if (!this._WorkerThread.Join(5000))
         {
            LoggingSingleton.Instance.Message(LogType.Library, LogLevel.Error, 
                                              "EventDispatcher::InternalStop : InternalRunThread failed to terminate -- aborting");
            this._WorkerThread.Abort();
         }

         this._WorkerThread = null;
      }

      private void InternalRunThread()
      {
         LoggingSingleton.Instance.Message(LogType.Library, LogLevel.Debug2, 
                                           "EventDispatcher::InternalRunThread : Entering InternalRunThread()");

         /// Set the state to connect and then initiate an HTTP GET to set up
         /// the event handler...
         /// 
         this.ConnectionState = ConnectionStateEnum.Connecting;

         LoggingSingleton.Instance.Message(LogType.Library, LogLevel.Info, 
                                           "EventDispatcher::InternalRunThread : Connecting to event handler...");

         if (ConnectToEventHandler())
         {
            this.ConnectionState = ConnectionStateEnum.Connected;

            try
            {
               /// Start a thread to monitor for events...
               /// 
               Thread ProcessEventsThread = new Thread(InternalProcessEventsThread);
               ProcessEventsThread.IsBackground = true;
               ProcessEventsThread.Start();

               for (; ; )
               {
                  Thread.Sleep(50);
               }
            }
            catch (ThreadInterruptedException)
            {
               LoggingSingleton.Instance.Message(LogType.Library, LogLevel.Debug2, "EventDispatcher::InternalRunThread : Caught ThreadInterruptedException in InternalRunThread()!");
            }
            catch (ThreadAbortException)
            {
               LoggingSingleton.Instance.Message(LogType.Library, LogLevel.Debug2, "EventDispatcher::InternalRunThread : Caught ThreadAbortException in InternalRunThread()!");
            }

            this.ConnectionState = ConnectionStateEnum.Disconnecting;

            LoggingSingleton.Instance.Message(LogType.Library, LogLevel.Info, "EventDispatcher::InternalRunThread : Disconnecting from event handler...");

            DisconnectFromEventHandler();
         }

         this.ConnectionState = ConnectionStateEnum.Idle;

         LoggingSingleton.Instance.Message(LogType.Library, LogLevel.Debug2, "EventDispatcher::InternalRunThread : Leaving InternalRunThread()");
      }

      /// <summary>
      /// This thread processes any events that occur whilst the connection to
      /// the XMS server is open. It sends a single "GET" request which then
      /// receives "chunks" of data. Ie. there is ONE request but MULTIPLE
      /// responses. We pass each of these "chunks" to the call dispatcher to
      /// work out what to do with them.
      /// </summary>
      /// 
      public void InternalProcessEventsThread()
      {
         LoggingSingleton.Instance.Message(LogType.Library, LogLevel.Debug2,
                                           "EventDispatcher::InternalProcessEventsThread : Entering InternalProcessEventsThread()");

         String ServerUri = String.Format("http://{0}:{1}",
                                          RestSettings.Instance.ServerIP,
                                          RestSettings.Instance.ServerPort);

         try
         {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(this._EventHandlerURI);
            request.Accept = null;
            request.ContentType = null;
            request.KeepAlive = true;
            request.Method = "GET";
            request.ProtocolVersion = HttpVersion.Version11;

            LoggingSingleton.Instance.Message(LogType.Library, LogLevel.Debug2,
                                              "EventDispatcher::InternalProcessEventsThread : Sent GET {0}",
                                              this._EventHandlerURI);

            using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
            {
               using (StreamReader stream = new StreamReader(response.GetResponseStream()))
               {
                  LoggingSingleton.Instance.Message(LogType.Library, LogLevel.Debug2,
                                                    "EventDispatcher::InternalProcessEventsThread : Received {0:D} {1}",
                                                    response.StatusCode, response.StatusDescription);

                  LoggingSingleton.Instance.Message(LogType.Library, LogLevel.Info,
                                                    "EventDispatcher::InternalProcessEventsThread : Waiting for events...");

                  while (!stream.EndOfStream)
                  {
                     String line = stream.ReadLine();

                     if (String.IsNullOrWhiteSpace(line))
                     {
                        continue;
                     }

                     int bufferLength = Int32.Parse(line, NumberStyles.AllowHexSpecifier);

                     if (bufferLength > 0)
                     {
                        /// MC : Thu Feb 19 10:52:00 GMT 2015
                        /// 
                        /// Note that there was a pretty serious bug / omission in the following section whereby I had assumed that
                        /// stream.Read(...) would always return the whole data chunk. This is not the case. Sometimes (seemingly 
                        /// when the data spans two TCP packets) stream.Read(...) returns LESS than "bufferLength" chars and we are
                        /// required to keep calling the function until we have ALL of the data. Failure to do this will cause an
                        /// exception when attempting to cast the data to a RESTapi.web_service object.
                        /// 
                        char[] buffer  = new char[bufferLength + 1];
                        int readLength = 0;
                        int count      = 0;

                        while (readLength < bufferLength)
                        {
                           readLength += stream.Read(buffer, readLength, bufferLength - readLength);
                           count++;
                        }

                        LoggingSingleton.Instance.Message(LogType.Library, LogLevel.Debug2,
                                                          "EventDispatcher::InternalProcessEventsThread : Read {0} bytes from stream [{1} read{2}]",
                                                          readLength, count, count == 1 ? "" : "s");

                        /// Cast the data to a web service event...
                        ///
                        RESTapi.web_service ws = RestHelpers.XMLToRESTapi(new String(buffer), typeof(RESTapi.web_service)) as RESTapi.web_service;

                        if (ws != null)
                        {
                           /// We have an event! We need to pass this to "something"
                           /// that will determine which call etc. the request belongs
                           /// to - ie. a call dispatcher.
                           /// 
                           CallDispatcher<T>.Instance.ProcessRequest(ws);
                        }
                     }
                  }
               }
            }
         }
         catch (WebException ex)
         {
            if (ex.Status == WebExceptionStatus.ProtocolError)
            {
               HttpWebResponse response = ex.Response as HttpWebResponse;

               if (response != null)
               {
                  LoggingSingleton.Instance.Message(LogType.Library, LogLevel.Warning,
                                                    "EventDispatcher::InternalProcessEventsThread : Received HTTP failure response {0:D} {1}",
                                                    (int)response.StatusCode, response.StatusDescription);
               }
            }
            else
            {
               LoggingSingleton.Instance.Message(LogType.Library, LogLevel.Warning,
                                                 "EventDispatcher::InternalProcessEventsThread : {0}\n{1}",
                                                 ex.Message.ToString(), ex.StackTrace.ToString());
            }
         }
         catch (ThreadInterruptedException ex)
         {
            LoggingSingleton.Instance.Message(LogType.Library, LogLevel.Warning,
                                              "EventDispatcher::InternalProcessEventsThread : {0}\n{1}",
                                              ex.Message.ToString(), ex.StackTrace.ToString());
         }
         catch (Exception ex)
         {
            LoggingSingleton.Instance.Message(LogType.Library, LogLevel.Warning,
                                              "EventDispatcher::InternalProcessEventsThread : {0}\n{1}",
                                              ex.Message.ToString(), ex.StackTrace.ToString());
         }

         LoggingSingleton.Instance.Message(LogType.Library, LogLevel.Debug2,
                                           "EventDispatcher::InternalProcessEventsThread : Leaving InternalProcessEventsThread()");
      }

      /// <summary>
      /// Connect to the XMS server's event handler. This demonstrates how to
      /// use the web service API that was generated from the XSD.
      /// </summary>
      /// <returns>true = success, false = failure</returns>
      /// 
      private bool ConnectToEventHandler()
      {
         /// This is the URI we are sending the request to...
         ///
         String uri = String.Format("http://{0}:{1}/default/eventhandlers?appid={2}",
                                    RestSettings.Instance.ServerIP,
                                    RestSettings.Instance.ServerPort,
                                    RestSettings.Instance.AppID);

         /// Here we create the actual HTTP request. We set the method to one
         /// of "POST" or "GET". Note that we do have to  explicitly set the 
         /// protocol version as XMS doesn't like the .NET default...
         /// 
         HttpWebRequest request  = (HttpWebRequest)WebRequest.Create(uri);
         request.Method          = "POST";
         request.ProtocolVersion = HttpVersion.Version11;

         /// This creates a RESTapi.web_service object which is the actual
         /// payload of the request... in this case, it's an event subscribe
         /// type request.
         /// 
         RESTapi.web_service service = new RESTapi.web_service()
         {
            Item = new RESTapi.eventhandler()
            {
               eventsubscribe = new RESTapi.eventsubscribe[]
               {
                  new RESTapi.eventsubscribe()
               }
            }
         };

         /// The RestHelpers class contains various functions for converting 
         /// between RESTapi objects and XML / text. In this case, we need to 
         /// convert from RESTapi -> an XML string.
         /// 
         String requestContent = RestHelpers.RESTapiToXML(service, typeof(RESTapi.web_service));

         if (String.IsNullOrWhiteSpace(requestContent))
         {
            /// The conversion failed as the string is empty...
            /// 
            return false;
         }

         /// Set the content type and length appropriately...
         /// 
         request.ContentType   = "application/xml";
         request.ContentLength = requestContent.Length;

         try
         {
            /// The following code sends the request. The "using" makes sure 
            /// that if the request fails, the stream gets closed properly (ie.
            /// it cleans up stream exceptions correctly!)
            /// 
            using (StreamWriter stream = new StreamWriter(request.GetRequestStream()))
            {
               stream.Write(requestContent);
               stream.Close();
            }

            LoggingSingleton.Instance.Message(LogType.Library, LogLevel.Debug2,
                                              "EventDispatcher::ConnectToEventHandler : Sent POST {0}", uri);

            /// This waits for the response from the XMS server...
            /// 
            using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
            {
               using (StreamReader stream = new StreamReader(response.GetResponseStream()))
               {
                  String result = stream.ReadToEnd();

                  if (String.IsNullOrWhiteSpace(result))
                  {
                     LoggingSingleton.Instance.Message(LogType.Library, LogLevel.Warning,
                                                       "EventDispatcher::ConnectToEventHandler : POST response contained no payload!");
                     return false;
                  }

                  /// If all went well and fingers (and toes) crossed, we have 
                  /// a "payload" (ie. XML...) from the XMS server which contains
                  /// "stuff" for us to do things with...
                  /// 
                  LoggingSingleton.Instance.Message(LogType.Library, LogLevel.Debug2,
                                                    "EventDispatcher::ConnectToEventHandler : Received {0:D} {1}",
                                                    response.StatusCode, response.StatusDescription);

                  try
                  {
                     /// We first convert the XML/text response into a 
                     /// web_service object...
                     /// 
                     RESTapi.web_service evresponse = RestHelpers.XMLToRESTapi(result, typeof(RESTapi.web_service)) as RESTapi.web_service;

                     if (evresponse != null)
                     {
                        /// We know that the actual object type is "eventhandler_response" 
                        /// in this case so we go ahead and cast it...
                        /// 
                        RESTapi.eventhandler_response item = evresponse.Item as RESTapi.eventhandler_response;

                        /// We need to store the OID and EventHandler URI for later
                        /// use (ie. when we disconnect / process events!)
                        /// 
                        this._OID             = item.identifier;
                        this._EventHandlerURI = String.Format("{0}?appid={1}", item.href, RestSettings.Instance.AppID);

                        if (!this._EventHandlerURI.Contains("http://"))
                        {
                           this._EventHandlerURI = String.Format("http://{0}:{1}{2}?appid={3}",
                                                                 RestSettings.Instance.ServerIP, RestSettings.Instance.ServerPort, item.href, RestSettings.Instance.AppID);
                        }

                        LoggingSingleton.Instance.Message(LogType.Library, LogLevel.Debug2, 
                                                            "EventDispatcher::ConnectToEventHandler : EventHandler URI is \"{0}\", OID is \"{1}\"",
                                                            this._EventHandlerURI, this._OID);
                     }
                  }
                  catch (Exception ex)
                  {
                     LoggingSingleton.Instance.Message(LogType.Library, LogLevel.Warning,
                                                       "EventDispatcher::ConnectToEventHandler : {0}\n{1}",
                                                       ex.Message, ex.StackTrace);
                     return false;
                  }

                  if (String.IsNullOrWhiteSpace(this._EventHandlerURI))
                  {
                     LoggingSingleton.Instance.Message(LogType.Library, LogLevel.Warning, 
                                                       "EventDispatcher::ConnectToEventHandler : EventHandler URI was null!");
                     return false;
                  }
               }
            }
         }
         catch (WebException ex)
         {
            /// Process any exceptions that we caught above...
            /// 
            if (ex.Status == WebExceptionStatus.ProtocolError)
            {
               HttpWebResponse response = ex.Response as HttpWebResponse;

               if (response != null)
               {
                  LoggingSingleton.Instance.Message(LogType.Library, LogLevel.Warning, 
                                                    "EventDispatcher::ConnectToEventHandler : Received HTTP failure response {0} {1}",
                                                    (int)response.StatusCode, response.StatusDescription);
               }
            }
            else
            {
               LoggingSingleton.Instance.Message(LogType.Library, LogLevel.Warning, 
                                                 "EventDispatcher::ConnectToEventHandler : {0}\n{1}",
                                                 ex.Message.ToString(), ex.StackTrace.ToString());
            }

            return false;
         }
         catch (Exception ex)
         {
            LoggingSingleton.Instance.Message(LogType.Library, LogLevel.Warning, 
                                              "EventDispatcher::ConnectToEventHandler : {0}\n{1}",
                                              ex.Message.ToString(), ex.StackTrace.ToString());
            return false;
         }

         LoggingSingleton.Instance.Message(LogType.Library, LogLevel.Debug2,
                                           "EventDispatcher::ConnectToEventHandler : EventHandler connected");
         return true;
      } /* ConnectToEventHandler() */

      /// <summary>
      /// Disconnect from the XMS server's event handler.
      /// </summary>
      /// <returns>true = success, false = failure</returns>
      /// 
      private bool DisconnectFromEventHandler()
      {
         /// Similar to the connection, we create an HTTP web request but this
         /// time, we use the "DELETE" method. This is MUCH simpler as we have
         /// no payload or anything this time. We pass the request the URI we
         /// obtained when we connected...
         /// 
         HttpWebRequest request  = (HttpWebRequest)WebRequest.Create(this._EventHandlerURI);
         request.Method          = "DELETE";
         request.ProtocolVersion = HttpVersion.Version11;

         try
         {
            LoggingSingleton.Instance.Message(LogType.Library, LogLevel.Debug2,
                                              "EventDispatcher::DisconnectFromEventHandler : Sent DELETE {0}",
                                              this._EventHandlerURI);

            /// Again, this waits for a response from the far end...
            /// 
            using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
            {
               using (StreamReader stream = new StreamReader(response.GetResponseStream()))
               {
                  /// Read the result from the far end...
                  /// 
                  String result = stream.ReadToEnd();

                  LoggingSingleton.Instance.Message(LogType.Library, LogLevel.Debug2,
                                                    "EventDispatcher::DisconnectFromEventHandler : Received {0:D} {1}",
                                                    response.StatusCode, response.StatusDescription);

                  /// Release the URI and OID as they are no longer valid...
                  /// 
                  this._EventHandlerURI = String.Empty;
                  this._OID             = String.Empty;
               }
            }
         }
         catch (WebException ex)
         {
            if (ex.Status == WebExceptionStatus.ProtocolError)
            {
               HttpWebResponse response = ex.Response as HttpWebResponse;

               if (response != null)
               {
                  LoggingSingleton.Instance.Message(LogType.Library, LogLevel.Warning, 
                                                    "EventDispatcher::DisconnectFromEventHandler : Received HTTP failure response {0} {1}",
                                                    (int)response.StatusCode, response.StatusDescription);
               }
            }
            else
            {
               LoggingSingleton.Instance.Message(LogType.Library, LogLevel.Warning,
                                                 "EventDispatcher::DisconnectFromEventHandler : {0}\n{1}",
                                                 ex.Message.ToString(), ex.StackTrace.ToString());
            }

            return false;
         }
         catch (Exception ex)
         {
            LoggingSingleton.Instance.Message(LogType.Library, LogLevel.Warning,
                                              "EventDispatcher::DisconnectFromEventHandler : {0}\n{1}",
                                              ex.Message.ToString(), ex.StackTrace.ToString());
            return false;
         }

         LoggingSingleton.Instance.Message(LogType.Library, LogLevel.Debug2,
                                           "EventDispatcher::DisconnectFromEventHandler : EventHandler disconnected");
         return true;
      } /* DisconnectFromEventHandler() */

      static public EventDispatcherList<T> GetEventHandlers()
      {
         EventDispatcherList<T> retval = new EventDispatcherList<T>();

         String uri = String.Format("http://{0}:{1}/default/eventhandlers?appid={2}",
                                    RestSettings.Instance.ServerIP,
                                    RestSettings.Instance.ServerPort,
                                    RestSettings.Instance.AppID);

         HttpWebRequest request  = (HttpWebRequest)WebRequest.Create(uri);
         request.Method          = "GET";
         request.ProtocolVersion = HttpVersion.Version11;

         try
         {
            LoggingSingleton.Instance.Message(LogType.Library, LogLevel.Debug2,
                                              "EventDispatcher::GetEventHandlers : Sent GET {0}", uri);

            /// This waits for a response from the far end...
            /// 
            using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
            {
               using (StreamReader stream = new StreamReader(response.GetResponseStream()))
               {
                  /// Read the result from the far end...
                  /// 
                  String result = stream.ReadToEnd();

                  LoggingSingleton.Instance.Message(LogType.Library, LogLevel.Debug2,
                                                    "EventDispatcher::GetEventHandlers : Received {0:D} {1}",
                                                    response.StatusCode, response.StatusDescription);

                  LoggingSingleton.Instance.Message(LogType.Library, LogLevel.Debug3,
                                                    "EventDispatcher::GetEventHandlers : {0}", result);

                  RESTapi.web_service evresponse = RestHelpers.XMLToRESTapi(result, typeof(RESTapi.web_service)) as RESTapi.web_service;

                  if (evresponse != null)
                  {
                     /// We know that the actual object type is "eventhandlers_response" 
                     /// in this case so we go ahead and cast it...
                     /// 
                     RESTapi.eventhandlers_response evhandlers = evresponse.Item as RESTapi.eventhandlers_response;

                     if (evhandlers != null)
                     {
                        LoggingSingleton.Instance.Message(LogType.Library, LogLevel.Info,
                                                          "EventDispatcher::GetEventHandlers : Found {0} existing event handlers",
                                                          evhandlers.size);

                        if (evhandlers.eventhandler_response != null)
                        {
                           foreach (RESTapi.eventhandler_response ev in evhandlers.eventhandler_response)
                           {
                              LoggingSingleton.Instance.Message(LogType.Library, LogLevel.Debug3,
                                                                "EventDispatcher::GetEventHandlers : Event handler :\n" +
                                                                "  => identifier = {0}\n" +
                                                                "  => href       = {1}\n" +
                                                                "  => appid      = {2}\n",
                                                                ev.identifier,
                                                                ev.href,
                                                                ev.appid);

                              retval.Add(new EventDispatcher<T>()
                              {
                                 _OID             = ev.identifier,
                                 _EventHandlerURI = ev.href.Contains("http://") ? String.Format("{0}?appid={1}", ev.href, ev.appid) : String.Format("http://{0}:{1}{2}?appid={3}", RestSettings.Instance.ServerIP, RestSettings.Instance.ServerPort, ev.href, ev.appid),
                                 _ConnectionState = ConnectionStateEnum.Connected,
                              });
                           }
                        }
                     }
                  }
               }
            }
         }
         catch (WebException ex)
         {
            if (ex.Status == WebExceptionStatus.ProtocolError)
            {
               HttpWebResponse response = ex.Response as HttpWebResponse;

               if (response != null)
               {
                  LoggingSingleton.Instance.Message(LogType.Library, LogLevel.Warning,
                                                    "EventDispatcher::GetEventHandlers : Received HTTP failure response {0} {1}",
                                                    (int)response.StatusCode, response.StatusDescription);
               }
            }
            else
            {
               LoggingSingleton.Instance.Message(LogType.Library, LogLevel.Warning,
                                                 "EventDispatcher::GetEventHandlers : {0}\n{1}",
                                                 ex.Message.ToString(), ex.StackTrace.ToString());
            }

            return retval;
         }
         catch (Exception ex)
         {
            LoggingSingleton.Instance.Message(LogType.Library, LogLevel.Warning,
                                              "EventDispatcher::GetEventHandlers : {0}\n{1}",
                                              ex.Message.ToString(), ex.StackTrace.ToString());
            return retval;
         }

         return retval;
      } /* GetEventHandlers() */

      static public void DisconnectEventHandler(String _oid)
      {
         if (String.IsNullOrWhiteSpace(_oid))
         {
            return;
         }

         EventDispatcherList<T> handlers = EventDispatcher<T>.GetEventHandlers();

         foreach (EventDispatcher<T> dispatcher in handlers.FindAll(x => x._OID == _oid))
         {
            dispatcher.DisconnectFromEventHandler();
         }
      }

      static public void DisconnectAllEventHandlers()
      {
         EventDispatcherList<T> handlers = EventDispatcher<T>.GetEventHandlers();

         foreach (EventDispatcher<T> dispatcher in handlers)
         {
            dispatcher.DisconnectFromEventHandler();
         }
      }

      #region Event handlers

      private void OnConnectionStateChanged(ConnectionStateEnum state)
      {
         switch (state)
         {
            case ConnectionStateEnum.Connected:
               {
                  if (this.DeleteAllCallsOnConnect)
                  {
                     /////////////////////////////////////////////////////////////
                     /// Cleanup any existing calls on this appID...
                     /// 
                     CallDispatcher<T>.Instance.GetCalls();

                     List<String> resourceids = CallDispatcher<T>.Instance.Calls.Select(x => x.ResourceID).ToList();

                     foreach (String resourceid in resourceids)
                     {
                        CallDispatcher<T>.Instance.DeleteCall(resourceid);
                     }
                  }
               }
               break;
         }
      } /* OnConnectionStateChanged() */

      private void OnPropertyChanged(object sender, PropertyChangedEventArgs e)
      {
         switch (e.PropertyName)
         {
            case "ConnectionState":
               {
                  if (ConnectionStateChanged != null)
                  {
                     ConnectionStateChanged(this.ConnectionState);
                  }
               }
               break;
         }
      } /* OnPropertyChanged() */

      #endregion
   }
}
