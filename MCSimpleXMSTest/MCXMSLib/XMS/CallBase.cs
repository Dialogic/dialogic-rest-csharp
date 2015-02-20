using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using MCantale.Helpers;
using Syzygy.Core.Logging;

namespace MCantale.XMS
{
   #region class CallBase

   public abstract class CallBase : IDisposable
   {
      #region Fields and members

      #region enum CallStateType

      public enum CallStateType
      {
         Idle,
         Dialling,
         Ringing,
         Connected,
         Hangup,
         Unknown,
      };

      #endregion

      public String        Direction    { get; set; }
      public String        ResourceID   { get; set; }
      public String        ResourceType { get; set; }

      public String        CallerUri    { get; set; }
      public String        CalledUri    { get; set; }
      public String        Name         { get; set; }
      public String        Uri          { get; set; }
      public CallStateType CallState    { get; set; }

      protected RESTapi.@event             _LastEvent     = null;
      protected Dictionary<String, String> _LastEventData = null;

      public ICallDispatcher<CallBase> Dispatcher
      {
         get;
         set;
      }

      public bool IsDeleted { get; set; }

      public String CallURI
      {
         get
         {
            return String.Format("http://{0}:{1}/default/calls/{2}?appid={3}",
                                 RestSettings.Instance.ServerIP,
                                 RestSettings.Instance.ServerPort,
                                 this.ResourceID,
                                 RestSettings.Instance.AppID);
         }
      }

      #endregion

      #region Constructor

      public CallBase()
      {
         this.Dispatcher = null;
         this.IsDeleted  = false;
      }

      public CallBase(ICallDispatcher<CallBase> _Dispatcher)
      {
         this.Dispatcher = _Dispatcher;
         this.IsDeleted  = false;
      }

      #region IDisposable implementation

      public void Dispose()
      {
         Dispose(true);
         GC.SuppressFinalize(true);
      } /* Dispose() */

      #endregion

      ~CallBase()
      {
         Dispose(false);
      }

      void Dispose(bool Disposing)
      {
         LoggingSingleton.Instance.Message(LogType.Library, LogLevel.Debug3, "Entering Call::Dispose({0})", Disposing ? "true" : "false");
         
         if (Disposing)
         {
            /// Managed resources...
            ///
         }

         /// Unmanaged resources...
         ///
         if (!IsDeleted)
         {
            DeleteCall();
         }

         LoggingSingleton.Instance.Message(LogType.Library, LogLevel.Debug3, "Leaving Call::Dispose");
      }

      #endregion

      #region Call control functions

      protected virtual void AcceptCall(bool EarlyMedia = true, RESTapi.media_type MediaType = RESTapi.media_type.audiovideo)
      {
         /// <call accept="yes" early_media="yes" media="audiovideo" signaling="yes" dtmf_mode="rfc2833"
         ///       async_dtmf="yes" async_tone="yes" rx_delta="+0dB" tx_delta="+0dB" cpa="no" info_ack_mode="automatic"/>
         /// 
         RESTapi.web_service ws = new RESTapi.web_service()
         {
            Item = new RESTapi.call()
            {
               accept                 = RESTapi.boolean_type.yes,
               acceptSpecified        = true,
               early_media            = EarlyMedia ? RESTapi.boolean_type.yes : RESTapi.boolean_type.no,
               early_mediaSpecified   = true,
               media                  = MediaType,
               mediaSpecified         = true,
               dtmf_mode              = RESTapi.dtmf_mode_option.rfc2833,
               async_dtmf             = RESTapi.boolean_type.yes,
               async_dtmfSpecified    = true,
               async_tone             = RESTapi.boolean_type.yes,
               async_toneSpecified    = true,
               info_ack_mode          = RESTapi.ack_mode_option.automatic,
               info_ack_modeSpecified = true,
            }
         };

         String responseString = String.Empty;

         if (RestHelpers.SendHttpRequest(out responseString, CallURI, "PUT", ws))
         {
            LoggingSingleton.Instance.Message(LogType.Library, LogLevel.Debug1, "Call::AcceptCall : Accept call OK");
            LoggingSingleton.Instance.Message(LogType.Library, LogLevel.Debug1, responseString);

            PutEvent(RESTapi.event_type.ringing);
         }
         else
         {
            LoggingSingleton.Instance.Message(LogType.Library, LogLevel.Error, "Call::AcceptCall : Accept call failed!");
            Hangup();
         }
      }

      protected virtual void AnswerCall(RESTapi.media_type MediaType = RESTapi.media_type.audiovideo, bool AsyncCompletion = true)
      {
         /// <call answer="yes" media="audiovideo" signaling="yes" dtmf_mode="rfc2833" async_completion="yes"
         ///       async_dtmf="yes" async_tone="yes" rx_delta="+0dB" tx_delta="+0dB" cpa="no" info_ack_mode="automatic"/>
         ///

         RESTapi.web_service ws = new RESTapi.web_service()
         {
            Item = new RESTapi.call()
            {
               answer                    = RESTapi.boolean_type.yes,
               answerSpecified           = true,
               async_completion          = AsyncCompletion ? RESTapi.boolean_type.yes : RESTapi.boolean_type.no,
               async_completionSpecified = true,
               media                     = MediaType,
               mediaSpecified            = true,
               dtmf_mode                 = RESTapi.dtmf_mode_option.rfc2833,
               async_dtmf                = RESTapi.boolean_type.yes,
               async_dtmfSpecified       = true,
               async_tone                = RESTapi.boolean_type.yes,
               async_toneSpecified       = true,
               info_ack_mode             = RESTapi.ack_mode_option.automatic,
               info_ack_modeSpecified    = true,
            }
         };

         String responseString = String.Empty;

         if (RestHelpers.SendHttpRequest(out responseString, CallURI, "PUT", ws))
         {
            LoggingSingleton.Instance.Message(LogType.Library, LogLevel.Debug1, "Call::AnswerCall : AnswerCall OK");
            LoggingSingleton.Instance.Message(LogType.Library, LogLevel.Debug1, responseString);

            // Note: If we use async_completion, we DON'T have to put the event
            // manually as it will (or rather... should) appear as a separate
            // event from the XMS server...
            //
            if (!AsyncCompletion)
            {
               PutEvent(RESTapi.event_type.answered);
            }
         }
         else
         {
            LoggingSingleton.Instance.Message(LogType.Library, LogLevel.Error, "Call::AnswerCall : AnswerCall failed!");
            Hangup();
         }
      }

      protected void DeleteCall()
      {
         if (!this.IsDeleted && this.Dispatcher != null)
         {
            this.Dispatcher.DeleteCall(this.ResourceID);
            this.IsDeleted = true;
         }
      }

      protected void Hangup()
      {
         if (this.Dispatcher != null)
         {
            this.CallState = CallStateType.Hangup;
            this.Dispatcher.Hangup(this.ResourceID);
         }
      }

      protected void PlayFile(String audioUri, String videoUri)
      {
         PlayFile(audioUri, RESTapi.audio_type_option.audioxwav, videoUri, RESTapi.video_type_option.videoxvid);
      } /* PlayFile() */

      /// <summary>
      /// This is an example of how to populate and call a more complicated 
      /// RESTapi structure. We play an audio/video file from here...
      /// </summary>
      /// <param name="audioUri"></param>
      /// <param name="videoUri"></param>
      ///
      protected virtual void PlayFile(String audioUri, RESTapi.audio_type_option audioType = RESTapi.audio_type_option.audioxwav, String videoUri = "", RESTapi.video_type_option videoType = RESTapi.video_type_option.videoxvid)
      {
         bool hasAudio = !String.IsNullOrWhiteSpace(audioUri);
         bool hasVideo = !String.IsNullOrWhiteSpace(videoUri);

         RESTapi.web_service ws = new RESTapi.web_service()
         {
            Item = new RESTapi.call()
            {
               call_action = new RESTapi.call_action()
               {
                  Item = new RESTapi.play()
                  {
                     offset      = "0s",
                     repeat      = "0",
                     delay       = "0s",
                     play_source = new RESTapi.play_source()
                     {
                        audio_uri           = hasAudio ? audioUri : String.Empty,
                        audio_type          = audioType,
                        audio_typeSpecified = hasAudio,
                        video_uri           = hasVideo ? videoUri : String.Empty,
                        video_type          = videoType,
                        video_typeSpecified = hasVideo,
                     }
                  }
               }
            }
         };

         if (hasAudio)
         {
            LoggingSingleton.Instance.Message(LogType.Library, LogLevel.Debug1, "Call::PlayFile : Playing audio \"{0}\"...", audioUri);
         }

         if (hasVideo)
         {
            LoggingSingleton.Instance.Message(LogType.Library, LogLevel.Debug1, "Call::PlayFile : Playing video \"{0}\"...", videoUri);
         }

         String responseString = String.Empty;

         if (RestHelpers.SendHttpRequest(out responseString, CallURI, "PUT", ws))
         {
            LoggingSingleton.Instance.Message(LogType.Library, LogLevel.Debug1, "Call::PlayFile : Play file OK");
            LoggingSingleton.Instance.Message(LogType.Library, LogLevel.Debug1, responseString);
         }
         else
         {
            LoggingSingleton.Instance.Message(LogType.Library, LogLevel.Error, "Call::PlayFile : Play file failed!");
            PutEvent(RESTapi.event_type.end_play);
         }
      } /* PlayFile() */

      #endregion

      #region Event handlers

      protected virtual void OnAccepted()
      {
      }

      protected virtual void OnAlarm()
      {
      }
      
      protected virtual void OnConnected()
      {
      }

      protected virtual void OnDtmf()
      {
      }

      protected virtual void OnEndPlay()
      {
      }

      protected virtual void OnHangup()
      {
         // Make sure to delete the call!!
         //
         DeleteCall();
      }

      protected virtual void OnIncoming()
      {
      }

      protected virtual void OnRinging()
      {
      }

      #endregion

      #region Event handling helper functions

      /// <summary>
      /// Ok - if we get here (!) then we have a valid event associated with a
      /// specific call. We should handle the event (obviously!) appropriately.
      /// </summary>
      /// <param name="ws">The web_service request to process</param>
      /// 
      protected virtual void HandleEvent(RESTapi.web_service ws)
      {
         RESTapi.@event @event = ws.Item as RESTapi.@event;

         LoggingSingleton.Instance.Message(LogType.Library, LogLevel.Event,
                                           "Call::HandleEvent : Processing event \"{0}\"",
                                           @event.type.ToString());

         this._LastEvent     = ws.Item as RESTapi.@event;
         this._LastEventData = null;

         if (this._LastEvent.event_data != null)
         {
            this._LastEventData = this._LastEvent.event_data.ToDictionary(x => x.name, x => x.value);

            /*string _Buf = "";

            foreach (KeyValuePair<string, string> kv in this._LastEventData)
            {
               _Buf += string.Format("  => {0}->{1}\n", kv.Key, kv.Value);
            }

            Logger.LogMessage("Call::HandleEvent : Event has associated event data:\n{0}", _Buf);*/
         }

         switch (@event.type)
         {
            case RESTapi.event_type.incoming:
               this.CallState = CallStateType.Ringing;
               OnIncoming();
               break;

            case RESTapi.event_type.ringing:
               this.CallState = CallStateType.Ringing;
               OnRinging();
               break;

            case RESTapi.event_type.accepted:
               this.CallState = CallStateType.Ringing;
               OnAccepted();
               break;

            case RESTapi.event_type.answered:
            case RESTapi.event_type.connected:
               this.CallState = CallStateType.Connected;
               OnConnected();
               break;

            case RESTapi.event_type.alarm:
               OnAlarm();
               break;

            case RESTapi.event_type.dtmf:
               OnDtmf();
               break;

            case RESTapi.event_type.end_play:
               OnEndPlay();
               break;

            case RESTapi.event_type.hangup:
               this.CallState = CallStateType.Hangup;
               OnHangup();
               break;

            default:
               LoggingSingleton.Instance.Message(LogType.Library, LogLevel.Error,
                                                 "Call::HandleEvent : Unhandled event \"{0}\"",
                                                 @event.type.ToString());
               break;
         }
      }

      private void PutEvent(RESTapi.event_type type)
      {
         RESTapi.web_service ws = new RESTapi.web_service()
         {
            Item = new RESTapi.@event()
            {
               type          = type,
               resource_type = ResourceType,
               resource_id   = ResourceID,
            }
         };

         /// IMPORTANT: Call the dispatcher IN A NEW THREAD or stuff will just
         /// randomly lock up / fail for no apparent reason!
         /// 
         ThreadPool.QueueUserWorkItem(x => { this.Dispatcher.ProcessRequest(ws); });
      } /* PutEvent() */

      #endregion
   }

   #endregion
}
