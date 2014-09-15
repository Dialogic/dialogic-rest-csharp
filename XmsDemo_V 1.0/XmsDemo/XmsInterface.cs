using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Xml.Serialization;

namespace XmsDemo
{
    class XmsInterface
    {
        private static string m_AppId = "csdemo";
        private static string m_XMSIP = "192.168.0.110";
        private static string m_XMSPort = "81";
        public static string m_SIPURLPath = "default/";
        public static string m_MediaURLPath = "default";
        public static string m_URLScheme = "http://";
        public static string m_URLPath = "default/";
        public static string m_FinalUrl = "";

        public XmsInterface(string XmsIp, string XmsPort, string appId)
        {
            m_AppId = appId;
            m_XMSIP = XmsIp;
            m_XMSPort = XmsPort;
        }
        public static string XmsUri()
        {
            return m_XMSIP;
        }

        public static string FinalUrl(string a_request, string a_resourceId)
        {
            string l_furl = m_URLScheme + m_XMSIP + ":" + m_XMSPort +
                "/" + m_SIPURLPath + a_request;
            if (a_resourceId!=null && a_resourceId.Length > 0)
                l_furl += "/" + a_resourceId;
            l_furl += "?appid=" + m_AppId;
            return l_furl;
        }

        public static HttpWebRequest CreateRequest(string a_method, string a_request, string a_resourceId)
		{
			string l_reponseAsString = "";
            m_FinalUrl = FinalUrl(a_request, a_resourceId); 

            HttpWebRequest l_request = null;
			try
			{
                l_request = (HttpWebRequest)WebRequest.Create(m_FinalUrl);
				l_request.Method = a_method;
                l_request.Accept = "application/xml";
                l_request.ContentType = "application/xml";
                l_request.ProtocolVersion = HttpVersion.Version10;

//                l_request.ExpectContinue = false;
/*
                if (a_xmlPayload.Length > 0)
                {
                    using (Stream l_requestStream = l_request.GetRequestStream())
                    using (StreamWriter l_writer = new StreamWriter(l_requestStream))
                    {
                        l_writer.Write(a_xmlPayload);
                    }
                }
 */
            }
			catch (Exception ex)
			{
				l_reponseAsString += "ERROR: " + ex.Message;
                return null;
			}
            return l_request;
        }
        public static HttpWebResponse GetResponse(HttpWebRequest a_request)
        {
            HttpWebResponse l_response = null;
            try
            {
                l_response = (HttpWebResponse)a_request.GetResponse();

            }
            catch (Exception ex)
            {
                Logger.Log("ERROR: " + ex.Message, false);
                return null;
            }
//            Logger.Log(XmsInterface.ResponseToString(l_response), false);
            return l_response;
        }
/*
        public static HttpWebRequest CreateRequest(string a_method, string a_request, string a_resourceId)

				l_response = (HttpWebResponse)l_request.GetResponse();
                l_response.GetResponseStream();
//				l_reponseAsString = ConvertResponseToString(l_response);
			}
//            l_reponseAsString += new StreamReader(l_request.GetRequestStream()).ReadToEnd();
            Logger.Log(l_reponseAsString, false);
            return l_request;
		}

*/
        public static string RequestToString(HttpWebRequest a_request, Object a_obj)
        {
            string l_result = "Request URI: " + a_request.RequestUri + "\r\n" +
            "Method: " + a_request.Method + "\r\nHeaders:\r\n";

            foreach (string key in a_request.Headers.Keys)
            {
                l_result += string.Format("{0}: {1} \r\n", key, a_request.Headers[key]);
            }

            

            if (a_obj == null)
                return l_result;
            l_result += "Request Payload:"; ;
            l_result += "\r\n" + ObjectToString(a_obj) + "\r\n";
            return l_result;

        }
        public static string ObjectToString(Object a_obj)
        {
            StringBuilder l_sb = new StringBuilder();
            using(TextWriter writer = new StringWriter(l_sb))
            {
                new XmlSerializer(a_obj.GetType()).Serialize(writer, a_obj);
            }
            return l_sb.ToString();
        }


        public static string ResponseToString(HttpWebResponse response)
		{
			string result = "Status code: " + (int)response.StatusCode + " " + response.StatusCode + "\r\n";

			foreach (string key in response.Headers.Keys)
			{
				result += string.Format("{0}: {1} \r\n", key, response.Headers[key]);
			}

			result += "\r\n";
            try
            {
                result += new StreamReader(response.GetResponseStream()).ReadToEnd();
            }
            catch (Exception e)
            {
                // for the case the response has no body
            }
            result += "\r\n";
			return result;
		}


    }
}
