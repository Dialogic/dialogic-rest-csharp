using System;
using System.Collections.Generic;
using System.Threading;
using MCantale.XMS;
using Syzygy.Core.Logging;

namespace MCSimpleXMSTest
{
   #region class MyCall

   class MyCall : CallBase
   {
      protected override void OnConnected()
      {
         PlayFile("file://verification/video_clip_nascar.wav",
                  "file://verification/video_clip_nascar.vid");
      }

      protected override void OnEndPlay()
      {
         foreach (KeyValuePair<string, string> item in this._LastEventData)
         {
            if (item.Key.ToLower() == "reason" && item.Value.ToLower() == "hangup")
            {
               return;
            }
         }

         Hangup();
      }

      protected override void OnIncoming()
      {
         AcceptCall();
      }

      protected override void OnRinging()
      {
         if (this.Direction == "inbound")
         {
            AnswerCall();
         }
      }
   }

   #endregion

   #region class Program

   class Program
   {
      static void Main(string[] args)
      {
         LoggingSingleton.Instance.LogToFile   = true;
         LoggingSingleton.Instance.LogToStdout = true;

         RestSettings.Instance.ServerIP   = "192.168.186.104";
         RestSettings.Instance.ServerPort = 81;
         RestSettings.Instance.AppID      = "app";

         bool needExit = false;

         //////////////////////////////////////////////////////////////////////
         /// Disconnect any existing event handlers...
         ///
         EventDispatcher<MyCall>.DisconnectAllEventHandlers();

         //////////////////////////////////////////////////////////////////////
         /// Create a new event handler...
         /// 
         EventDispatcher<MyCall> ev = new EventDispatcher<MyCall>() { DeleteAllCallsOnConnect = true };

         ev.Start();

         while (!needExit)
         {
            ConsoleKeyInfo info = Console.ReadKey(true);

            switch (info.KeyChar)
            {
               case 'x':
               case 'X':
                  needExit = true;
                  break;
            }

            Thread.Sleep(10);
         }

         ev.Stop();
         ev.WaitForStop();

         ev.Dispose();
         ev = null;
      }
   }

   #endregion
}
