using System;
using System.IO;
using System.Net;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using Syzygy.Core.Logging;

//////////////////////////////////////////////////////////////////////////////////////////////
/// NOTE: The XMS RESTapi files xmsrest.xsd / xmsrest.cs are generated in the following way:
/// [1] xmsrest.xsd is installed on the XMS server in the /etc/xms directory. Copy / transfer
/// this to the development machine.
/// [2] Open a Visual Studio command prompt and navigate to "xmsrest.xsd"
/// [3] Run the command "xsd.exe xmsrest.xsd /namespace:MCantale.XMS.RESTapi /classes"
/// [4] The file "xmsrest.cs" will be generated which contains the actual API.
/// 

namespace MCantale.Helpers
{
   #region class RestHelpers

   public class RestHelpers
   {
      #region Helper functions

      public static String RESTapiToXML(object api, Type type)
      {
         XmlSerializer serializer     = new XmlSerializer(type);
         StringBuilder requestContent = new StringBuilder();

         try
         {
            using (StringWriter writer = new StringWriter(requestContent))
            {
               XmlTextWriter xmlwriter = new XmlTextWriter(writer);
               xmlwriter.Formatting = Formatting.Indented;
               serializer.Serialize(xmlwriter, api);
            }
         }
         catch (Exception ex)
         {
            LoggingSingleton.Instance.Message(LogType.Library, LogLevel.Except,
                                              "XMLHelper::RESTapiToXML : {0}\n{1}",
                                              ex.Message, ex.StackTrace);
            return String.Empty;
         }

         return requestContent.ToString();
      } /* RESTapiToXML() */

      public static object XMLToRESTapi(String xml, Type type)
      {
         XmlSerializer serializer = new XmlSerializer(type);

         using (StringReader reader = new StringReader(xml))
         {
            return serializer.Deserialize(reader);
         }
      } /* XMLToRESTapi() */

      public static bool SendHttpRequest(out String responseString, String uri, String method, MCantale.XMS.RESTapi.web_service request)
      {
         String requestString = RestHelpers.RESTapiToXML(request, typeof(MCantale.XMS.RESTapi.web_service));

         return SendHttpRequest(out responseString, uri, method, requestString, "application/xml");
      } /* SendHttpRequest() */

      public static bool SendHttpRequest(out String responseString, String uri, String method = "GET", String requestContent = "", String contentType = "application/xml")
      {
         responseString = String.Empty;

         HttpWebRequest request  = (HttpWebRequest)WebRequest.Create(uri);
         request.Accept          = null;
         request.ContentType     = null;
         request.Expect          = null;
         request.KeepAlive       = true;
         request.Method          = method;
         request.ProtocolVersion = HttpVersion.Version11;

         try
         {
            if (!String.IsNullOrWhiteSpace(requestContent))
            {
               request.ContentType   = contentType;
               request.ContentLength = requestContent.Length;

               using (StreamWriter stream = new StreamWriter(request.GetRequestStream()))
               {
                  stream.Write(requestContent);
                  stream.Close();
               }
            }

            LoggingSingleton.Instance.Message(LogType.Library, LogLevel.Debug2,
                                              "XMLHelper::SendHttpRequest : Sent {0} {1}", method, uri);

            if (!String.IsNullOrWhiteSpace(requestContent))
            {
               LoggingSingleton.Instance.Message(LogType.Library, LogLevel.Debug2,
                                                 "XMLHelper::SendHttpRequest : Sent web service request :\n{0}", requestContent);
            }

            using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
            {
               using (StreamReader stream = new StreamReader(response.GetResponseStream()))
               {
                  String result = stream.ReadToEnd();

                  responseString = result;

                  /// Parse the response...
                  ///
                  LoggingSingleton.Instance.Message(LogType.Library, LogLevel.Debug2, 
                                                    "XMLHelper::SendHttpRequest : Received {0:D} {1}",
                                                    response.StatusCode, response.StatusDescription);
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
                                                    "XMLHelper::SendHttpRequest : Received HTTP failure response {0} {1}",
                                                    (int)response.StatusCode, response.StatusDescription);
               }
            }
            else
            {
               LoggingSingleton.Instance.Message(LogType.Library, LogLevel.Warning,
                                                 "XMLHelper::SendHttpRequest : {0}\n{1}",
                                                 ex.Message.ToString(), ex.StackTrace.ToString());
            }

            return false;
         }
         catch (Exception ex)
         {
            LoggingSingleton.Instance.Message(LogType.Library, LogLevel.Except,
                                              "XMLHelper::SendHttpRequest : {0}\n{1}",
                                              ex.Message.ToString(), ex.StackTrace.ToString());
            return false;
         }

         return true;
      } /* SendHttpRequest() */

      #endregion
   }

   #endregion
}
