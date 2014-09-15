using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Xml.Serialization;
using System.IO;


namespace XmsDemo
{
    class XmsCall
    {
        private call m_call;
        private call_action m_call_action;
        private play m_play;
        private playrecord m_playrec;
        private playcollect m_playcollect;
        private string m_callId;
        public string CallId
        {
            get{ return m_callId;}
            set{ m_callId = value;}
        }
        public XmsCall(string a_callId)
        {
            this.CallId = a_callId;
            m_call = new call();
            m_call_action = new call_action();
            m_play = new play();
            m_playrec = new playrecord();
            m_playcollect = new playcollect();
        }

        public enum e_CallState
        {
            STATE_OFFERED = 0, STATE_DIALING, STATE_CONNECTED, STATE_PLAYING,
            STATE_RECORDING, STATE_IDLE, STATE_NULL
        };
        public enum e_CallDirection { Incoming, Outgoung }

        private e_CallState m_callState;
        private e_CallDirection m_callDirection;
        public e_CallState CallState
        {
            get { return m_callState; }
            set { m_callState = value; }
        }
        public e_CallDirection CallDirection
        {
            get { return m_callDirection; }
            set { m_callDirection = value; }
        }


        public int Answer()
        {
            HttpWebRequest l_request = XmsInterface.CreateRequest("PUT", "calls", this.CallId);
            m_call.answer = boolean_type.yes;
            m_call.answerSpecified = true;
            
            m_call.cpa = boolean_type.no;
            m_call.media = media_type.audio;

            web_service l_ws = new web_service();
            l_ws.Item = m_call as call;

            using (Stream l_requestStream = l_request.GetRequestStream())
            {
                XmlSerializer x = new XmlSerializer(typeof(web_service));
                x.Serialize(l_requestStream, l_ws);
            }
            Logger.Log(XmsInterface.RequestToString(l_request, l_ws), true);

            HttpWebResponse l_response = null;
            try
            {
                l_response = (HttpWebResponse)l_request.GetResponse();

            }
            catch (Exception ex)
            {
                Logger.Log("ERROR: " + ex.Message, false);
                return -1;
            }

            Logger.Log(XmsInterface.ResponseToString(l_response), false);

            if (l_response.StatusCode != HttpStatusCode.OK)
            {
                // process response here
                return -1;
            }
            l_request.GetRequestStream().Close();
            l_response.Close();
            return 0;
        }
        public int Drop()
        {
            HttpWebRequest l_request = XmsInterface.CreateRequest("DELETE", "calls", this.CallId);
            l_request.Accept = null;
            l_request.ContentType = null;

            Logger.Log(XmsInterface.RequestToString(l_request, null), true); // no XML pyload here
            HttpWebResponse l_response = null;
            try
            {
                l_response = (HttpWebResponse)l_request.GetResponse();

            }
            catch (Exception ex)
            {
                Logger.Log("ERROR: " + ex.Message, false);
                return -1;
            }

            Logger.Log(XmsInterface.ResponseToString(l_response), false);

            if (l_response.StatusCode != HttpStatusCode.NoContent)
            {
                // process response here
                return -1;
            }
            l_response.Close();
            return 0;
        }

        public int Play(string a_filename)
        {
            m_play.offset = "0s";
            m_play.repeat = "1";
            m_play.delay = "2s";
            m_play.skip_interval = "0s";
            m_play.max_time = "infinite";
            m_play.terminate_digits = "*";
            m_play.play_source = new play_source();
            m_play.play_source.location = a_filename;
            m_call_action.Item = m_play as play;
            m_call.call_action = m_call_action;
            web_service l_ws = new web_service();
            l_ws.Item = m_call as call;

            HttpWebRequest l_request = XmsInterface.CreateRequest("PUT", "calls", this.CallId);
            using (Stream l_requestStream = l_request.GetRequestStream())
            {
                XmlSerializer x = new XmlSerializer(typeof(web_service));
                x.Serialize(l_requestStream, l_ws);
            }
            Logger.Log(XmsInterface.RequestToString(l_request, l_ws), true);

            HttpWebResponse l_response = null;
            try
            {
                l_response = (HttpWebResponse)l_request.GetResponse();

            }
            catch (Exception ex)
            {
                Logger.Log("ERROR: " + ex.Message, false);
                return -1;
            }

            Logger.Log(XmsInterface.ResponseToString(l_response), false);

            if (l_response.StatusCode != HttpStatusCode.OK)
            {
                // process response here
//                return -1;
            }
            l_request.GetRequestStream().Close();
            l_response.Close();

            return 0;
        }
        public int Record(string a_filename, int a_msDuration)
        {
            return 0;
        }

        public int MakeCall(string a_callingAddress)
        {
            web_service l_ws = new web_service();
            m_call.destination_uri = a_callingAddress;
            m_call.cpa = boolean_type.no;
            m_call.sdp = "";
            m_call.media = media_type.audio;
            m_call.signaling = boolean_type.yes;
            m_call.dtmf_mode = dtmf_mode_option.rfc2833;
            m_call.async_dtmf = boolean_type.no;
            m_call.async_tone = boolean_type.no;
            m_call.rx_delta = "+0dB";
            m_call.tx_delta = "+0dB";
            m_call.source_uri = "sip:" + XmsInterface.XmsUri();
            

            l_ws.Item = m_call as call;


            HttpWebRequest l_request = XmsInterface.CreateRequest("POST", "calls", "");

            using (Stream l_requestStream = l_request.GetRequestStream())
/*            using (StreamWriter writer = new StreamWriter(l_requestStream))
            {
                FileStream fs = new FileStream("test.txt", FileMode.Open);
                StreamReader rd = new StreamReader(fs);
                string requestBody = rd.ReadToEnd();
                writer.Write(requestBody);
            }
 */ 
            {
                XmlSerializer x = new XmlSerializer(typeof(web_service));
                x.Serialize(l_requestStream, l_ws);
            }

            Logger.Log(XmsInterface.RequestToString(l_request, l_ws), true);

            HttpWebResponse l_response = null;
            try
            {
                l_response = (HttpWebResponse)l_request.GetResponse();
            }
            catch (Exception ex)
            {
                Logger.Log("ERROR: " + ex.Message, false);
                return -1;
            }

            if (l_response.StatusCode != HttpStatusCode.Created)
            {
                // process response here
                Logger.Log(XmsInterface.ResponseToString(l_response), false);
                return -1;
            }

            using (Stream l_responseStream = l_response.GetResponseStream())
            {
                XmlSerializer x = new XmlSerializer(typeof(web_service));
                l_ws = (web_service)x.Deserialize(l_responseStream);
            }

            Logger.Log(XmsInterface.ResponseToString(l_response), false);

            l_request.GetRequestStream().Close();

            Type tp = l_ws.Item.GetType();
            switch (tp.Name)
            {
                case "call_response":
                    call_response l_cr = (call_response)l_ws.Item;
                    this.m_callId = l_cr.identifier;
                    break;
                default:
                    Logger.Log("Unknown response object type of " + tp.Name, false);
                    break;
            }
            l_request.GetRequestStream().Close();
            l_response.Close();
            return 0;
        }
    }
}
