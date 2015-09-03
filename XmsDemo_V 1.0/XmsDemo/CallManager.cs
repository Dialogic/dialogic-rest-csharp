using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace XmsDemo
{
    static class CallManager
    {

        private static Dictionary<string, XmsCall> m_callTable = new Dictionary<string, XmsCall>();
        
        public static int ProcessEvent(@event a_event)
        {
            XmsCall l_call = new XmsCall("");
            if(a_event.type == event_type.incoming) // new call
            {
                l_call.CallId = a_event.resource_id;
                l_call.CallState = XmsCall.e_CallState.STATE_OFFERED;
                l_call.CallDirection = XmsCall.e_CallDirection.Incoming;
                m_callTable.Add(a_event.resource_id, l_call);
                if (l_call.Answer() != 0) // failed for some reason, see logs, need to remove
                {
                    l_call.Drop();
                    m_callTable.Remove(a_event.resource_id);
                }
                else
                {
                    l_call.CallState = XmsCall.e_CallState.STATE_CONNECTED;
                    l_call.Play("file://verification/verification_intro.wav");
                }
                return 0; // done with the incoming call
            }


            switch(a_event.type)
            {
                case event_type.connected:
                    break;
                case event_type.dtmf:
                    break;
                case event_type.end_play:
                    if (m_callTable.TryGetValue(a_event.resource_id, out l_call) == false) //should not be here
                        Logger.Log("ERR - Invalid CRN", true);
                    else
                    {
                        l_call.Drop();
                        m_callTable.Remove(l_call.CallId);
                    }
                    break;
                case event_type.end_playcollect:
                    break;
                case event_type.end_playrecord:
                    break;
                case event_type.hangup:
                    try
                    {
                        l_call = m_callTable[a_event.resource_id];
                      //  l_call.Drop();
                        m_callTable.Remove(a_event.resource_id);
                    }
                    catch (KeyNotFoundException)
                    {
                        Logger.Log("ERR: Cannot find call reference to release", true);
                    }
                    
                    break;
                case event_type.ringing:
                    break;
                case event_type.tone:
                    break;

            }
            return 0;
        }
        public static int MakeCall(string a_address)
        {
            XmsCall l_outCall = new XmsCall("");
            l_outCall.CallDirection = XmsCall.e_CallDirection.Outgoung;
            if (l_outCall.MakeCall(a_address) == -1)
                return -1;
            l_outCall.CallState = XmsCall.e_CallState.STATE_DIALING;
            m_callTable.Add(l_outCall.CallId, l_outCall);
            return 0;

        }

        private static void ReleaseCall(string a_callId)
        {

        }
    }
}
