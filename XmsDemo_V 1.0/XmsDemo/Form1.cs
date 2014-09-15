using System;
using System.IO;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Net;
using System.Xml.Serialization;

namespace XmsDemo
{
    
    public partial class XmsDemoForm : Form
    {
        private static EventHandler m_EventHandler = new EventHandler();
        private string m_txtMessage;
        public delegate void AddToList();
        public AddToList tsDelegate; // thread-safe deleage to add strings to the list boxes
        private TextBox tbCurrentWindow = null;
        private static bool firstChance = true;
         
        
        public XmsDemoForm()
        {
            InitializeComponent();
            Logger.Init("CsDemo.log", this);
            m_txtMessage = new string(' ', 1024);
            tsDelegate = new AddToList(AddToListMethod);

        }

        public void AddToListMethod()
        {
            try
            {
                tbCurrentWindow.AppendText("\r\n" + m_txtMessage);
            }
            finally
            {
            }
        }

        public int WriteMessage(string a_message, bool a_request)
        {
            tbCurrentWindow = a_request?txtRequest:txtResponse;
            if (txtRequest.InvokeRequired)
            {
                m_txtMessage = a_message;
                return -1; // the calling thread will try to invoke the delegate
            }
            try
            {
                tbCurrentWindow.AppendText("\r\n" + a_message);
            }
            finally
            {
            }
            return 0;
        }

        private void btnStart_Click(object sender, EventArgs e)
        {
            Program.g_XmsIf = new XmsInterface(txtUri.Text, txtPort.Text, txtAppId.Text);

            if (m_EventHandler.Create(txtRequest.Text) != 0)
            {
                Logger.Log("ERROR creating event handler, see the logs", false);
                return;
            }
        }
        private void XmsDemoForm_Load(object sender, EventArgs e)
        {
            txtUri.Text = "192.219.76.226";
            txtAppId.Text = "app";
            txtPort.Text = "81";
            txtCallAddress.Text = "sip:test@10.129.49.156";

        }
        private void XmsDemoForm_Close(object sender, FormClosingEventArgs e)
        {
            if (firstChance)
            {
                e.Cancel = true;
                m_EventHandler.Destroy();
                firstChance = false;
                this.Close();
            }
            else
            {
                e.Cancel = false;
                
            }
       
        }

        private void btnMakeCall_Click(object sender, EventArgs e)
        {
            if (txtCallAddress.Text == "" || txtCallAddress.Text.Length < 10)
            {
                MessageBox.Show("Please enter valid destination address to call to", "",
                    MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return;
            }
            if (CallManager.MakeCall(txtCallAddress.Text) == -1)
            {
                MessageBox.Show("Call failed to initiate, check the log", "",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

    }
}
