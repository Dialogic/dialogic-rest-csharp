using System;
using System.Globalization;
using System.Xml;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Threading;
using System.Xml.Serialization;

namespace XmsDemo
{
    class EventHandler
    {
        private static string m_href; // referrence to the EH object, used for DELETing the handler
        private static string m_hId; // EH identifier
        private eventhandler m_EventHandler;
        private eventhandler_response m_EventResponse;
        private web_service m_ws;
        Thread m_eventThread;
        private static bool m_isRunning;

//        delegate void EventThread(int i);


        public int Create(string body)
        {
/*
            web_service l_ws = new web_service();
            FileStream fs = new FileStream("test.txt", FileMode.Open);
            XmlSerializer x = new XmlSerializer(typeof(web_service));
            web_service g = new web_service();
            g = (web_service)x.Deserialize(fs);
            Type tp  = g.Item.GetType();
            return 0;
*/
            m_EventHandler = new eventhandler();
            m_EventResponse = new eventhandler_response();
            m_ws = new web_service();
            return Subscribe(body);
        }
        public int Destroy()
        {
            return Unsubscribe();
        }


        private int Subscribe(string body) // sends POST method with event handler parameters
        {

            eventsubscribe[] es = new eventsubscribe[1];
            es[0] = new eventsubscribe();
            es[0].resource_id = "any";
            es[0].action = action_option.add;
            es[0].type = event_type.any;
            m_EventHandler.eventsubscribe = es;

            m_ws.Item = m_EventHandler as eventhandler;

            HttpWebRequest l_request = XmsInterface.CreateRequest("POST", "eventhandlers", "");
//            HttpWebRequest l_request = XmsInterface.CreateRequest("GET", "calls", "");
/*
            l_request.Accept =  "application/xml";
            l_request.ContentType = "application/xml";

            l_request.ContentLength = (XmsInterface.ObjectToString(m_ws)).Length;
            l_request.SendChunked = false;
            l_request.Expect = null;
            l_request.KeepAlive = false;

            l_request.ProtocolVersion = HttpVersion.Version10;
*/


            using (Stream l_requestStream = l_request.GetRequestStream())
            {
                XmlSerializer x = new XmlSerializer(typeof(web_service));
                x.Serialize(l_requestStream, m_ws);
            }

  /*
            if (body.Length > 0)
            {
                using (Stream requestStream = l_request.GetRequestStream())
                using (StreamWriter writer = new StreamWriter(requestStream))
                {
                    writer.Write(body);
                }
            }

    */      string l_srequest = XmsInterface.RequestToString(l_request, m_ws);
            Logger.Log(l_srequest, true);
            HttpWebResponse l_response = null;
            try
            {
                l_response = (HttpWebResponse)l_request.GetResponse();

                if (l_response.StatusCode != HttpStatusCode.Created)
                {
                    // process response here

                    Logger.Log(XmsInterface.ResponseToString(l_response), false);
                    return -1;
                }
                l_request.GetRequestStream().Close();
            }
            catch (Exception ex)
            {
                Logger.Log("ERROR: " + ex.Message, false);
                return -1;
            }

            using (Stream l_responseStream = l_response.GetResponseStream())
            {
                XmlSerializer x = new XmlSerializer(typeof(web_service));
                m_ws = (web_service)x.Deserialize(l_responseStream);
            }
            l_srequest = XmsInterface.ResponseToString(l_response);
            l_response.Close();
            

            Logger.Log(l_srequest, false);
            Type tp = m_ws.Item.GetType();
            switch (tp.Name)
            {
                case "eventhandler_response":
                    eventhandler_response l_ehr = (eventhandler_response)m_ws.Item;
                    EventHandler.m_href = l_ehr.href;
                    EventHandler.m_hId = l_ehr.identifier;
                    m_isRunning = true;
                    m_eventThread =  new Thread(EventHandler.EventThread);
                    m_eventThread.IsBackground = true;
                    m_eventThread.Start();


//                    l_response = (HttpWebResponse)l_request.GetResponse();
//                    m_eventStream = l_response.GetResponseStream();
//                    m_asyncResult = m_eventStream.BeginRead(m_eventBuffer, 0, 512, m_callback, null);
                     
                    break;
            }


/*
                using (Stream l_requestStream = l_request.GetRequestStream())
                {
                }
//                using (StreamWriter l_writer = new StreamWriter(l_requestStream))


                using (Stream l_requestStream = l_request.GetRequestStream())
                using (StreamWriter l_writer = new StreamWriter(l_requestStream))
                {
                    l_writer.Write(a_xmlPayload);
                }
            }


            web_service l_ws = new web_service();
            FileStream fs = new FileStream("CsDemo.log", FileMode.Open);
            XmlSerializer x = new XmlSerializer(typeof(web_service));
            web_service g = new web_service();
            g = (web_service)x.Deserialize(fs);
            Type tp  = g.Item.GetType();
            switch (tp.Name)
            {
                case "eventhandler_response":
                    eventhandler_response l_ehr = (eventhandler_response) g.Item;
                    this.m_href = l_ehr.href;
                    this.m_hId = l_ehr.identifier;
                    break;
            }
                

            


            string it = "";
//            it = g.Item.ToString();
/*
            Console.WriteLine(g.Manager.Name);
            Console.WriteLine(g.GroupID);
            Console.WriteLine(g.HexBytes[0]);
            foreach (Employee e in g.Employees)
            {
                Console.WriteLine(e.Name);
            }
 
            

            eventsubscribe[] es = new eventsubscribe[1];
            es[0] = new eventsubscribe();
            es[0].resource_id = "none";
            es[0].action = action_option.remove;
            es[0].type = event_type.any;
            eventhandler eh = new eventhandler();
            eh.eventsubscribe = es;

            l_ws.Item = eh as eventhandler;
            

            using (StreamWriter sw = new StreamWriter("CsDemo.log"))
            {

                new XmlSerializer(typeof(web_service)).Serialize(sw, l_ws);
            }



            string str = "";
            using (StreamWriter sw = new StreamWriter(str))//("CsDemo.log"))
            {

                new XmlSerializer(typeof(eventhandler)).Serialize(sw, eh);
            }
 */ 
            return 0;
        }

        private int Unsubscribe()
        {
            if (m_hId == null || m_hId == "")

                return -1;
            HttpWebRequest l_request = XmsInterface.CreateRequest("DELETE", "eventhandlers", m_hId);
            string l_srequest = XmsInterface.RequestToString(l_request, null);
            Logger.Log(l_srequest, true);

            HttpWebResponse l_response = (HttpWebResponse)l_request.GetResponse();
            l_srequest = XmsInterface.ResponseToString(l_response);
            Logger.Log(l_srequest, false);
            return 0;
        }

        private static void EventThread()
        {
            Thread.Sleep(300);
            HttpWebRequest l_request = XmsInterface.CreateRequest("GET", "eventhandlers", m_hId);
            l_request.KeepAlive = true;
            l_request.Accept = null;
            l_request.ContentType = null;
            l_request.ProtocolVersion = HttpVersion.Version11;

//            l_request.Connection = " ";
//            HttpWebResponse l_response = null;
            string l_srequest = XmsInterface.RequestToString(l_request, null);
            Logger.Log(l_srequest, true);

            HttpWebResponse l_response = (HttpWebResponse)l_request.GetResponse();
            l_srequest = "Status code: " + (int)l_response.StatusCode + " " + l_response.StatusCode;
            foreach (string key in l_response.Headers.Keys)
            {
                l_srequest += string.Format("{0}: {1} \r\n", key, l_response.Headers[key]);
            }
            Logger.Log(l_srequest, false);

            StreamReader l_evtStream = new StreamReader(l_response.GetResponseStream());
            string line = "";
            while (m_isRunning)
            {
                try
                {
/*
                    Logger.Log("EVENT :\r\n", false);
                    line = l_evtStream.ReadLine(); // skipping unreadable
                    try
                    {
                        line = l_evtStream.ReadLine(); // reading event length
                    }
                    catch (Exception ex0)
                    {
                        Logger.Log("ERROR Dbg: " + ex0.Message, false);
                    }

                    int buf_length = Int32.Parse(line, NumberStyles.AllowHexSpecifier); // parsing hex to int
                    char[] r_buf = new char[buf_length];
                    l_evtStream.Read(r_buf, 0, buf_length);
                    string str = new string(r_buf);
                    Logger.Log(str, false);

                    ProcessEvent(str);
*/
                    // Receive the buffer size

                    //

                    line = l_evtStream.ReadLine(); // skipping unreadable

                    line = l_evtStream.ReadLine(); // reading event length

                    if (line == null || line.Length < 1) // may happen upon exit
                    {

                        continue;

                    }

                    int bufLength = Int32.Parse(line, NumberStyles.AllowHexSpecifier); // parsing hex to int

                    //

                    // Read the string

                    //

                    string sBuf = "";

                    while (!l_evtStream.EndOfStream && !sBuf.EndsWith("</web_service>"))
                    {

                        sBuf += l_evtStream.ReadLine();

                    }

                    Logger.Log(sBuf, false);

                    //

                    // Process the full request

                    // 

                    if (sBuf.Length > 0) // not to pass empty payload

                        ProcessEvent(sBuf);


                }

                catch (Exception ex)
                {
                    Logger.Log("ERROR: " + ex.Message, false);
                }
            }
        }

        private static void ProcessEvent(string a_xmlString)
        {
            XmlReader reader = XmlReader.Create(new StringReader(a_xmlString));

            web_service l_ws = new web_service();
            try
            {
                XmlSerializer x = new XmlSerializer(typeof(web_service));
                l_ws = (web_service)x.Deserialize(reader);
            }
            catch(Exception ex)
            {
                Logger.Log(ex.Message, false);
                return;
            }


//            Logger.Log(XmsInterface.ResponseToString(a_xmlEvent), false);
            Type tp = l_ws.Item.GetType();
            @event l_event = null;
            Logger.Log("Event of type " + tp.Name, false);
            switch (tp.Name)
            {
                case "event":
                    l_event = (@event)l_ws.Item;
                    CallManager.ProcessEvent(l_event);
//                    XmsCall l_newCall = new XmsCall(l_event.resource_id);
//                    l_newCall.CallId = l_event.resource_id;
                    break;
                case "call_response":
                    break;
                default:
//                    Logger.Log(tp.Name, false);
                    break;
            }
        }
 
    }

}
