using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace XmsDemo
{
    class Logger
    {
        private static string m_logFile = "CsDemo.log";
        static XmsDemoForm m_formView = null;

           public static void Init(string a_logFileName, XmsDemoForm a_formView)
        {
            if ((a_logFileName != null) && !(a_logFileName == ""))
                m_logFile = a_logFileName;
//            if (!File.Exists(m_logFile))
            {
                FileStream l_fs = File.Create(m_logFile);
                l_fs.Close();
            }
            m_formView = a_formView;
        }

        public static void Log(string a_stringToAdd, bool a_request)
        {
            try
            {
                StreamWriter sw = File.AppendText(m_logFile);
                sw.WriteLine(a_stringToAdd);
                sw.Flush();
                sw.Close();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message.ToString());
            }
            try
            {

                if (m_formView.WriteMessage(a_stringToAdd, a_request) == -1) //Invoke required
                    m_formView.Invoke(m_formView.tsDelegate);
            }
            catch (Exception ex)
            {

            }
        }
    }
}
