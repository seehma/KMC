using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace ConnectorClient
{
    public partial class ConnectorClient : Form
    {
        KukaMatlabConnector.ConnectorObject connector_; // connector object which handles the matlab and robot connection

        double RKorr_X_, RKorr_Y_, RKorr_Z_, RKorr_A_, RKorr_B_, RKorr_C_;
        double AKorr_1_, AKorr_2_, AKorr_3_, AKorr_4_, AKorr_5_, AKorr_6_;

        System.Threading.Thread loggerListenThread_;  // creating thread instance for reading logmessages

        uint korrChooser_; // choose which correction you want 1 ... RKorr, 2 ... AKorr, 3 ... EKorr

        public ConnectorClient()
        {
            // initialize form
            InitializeComponent();

            // set the form closing event
            this.FormClosing += KukaConnectorClient_FormClosing;

            // default correction is RKorr;
            korrChooser_ = 1;

            // create thread to fetch all the logging messages
            loggerListenThread_ = new System.Threading.Thread(new System.Threading.ThreadStart(loggerListener));
            loggerListenThread_.Priority = System.Threading.ThreadPriority.Highest;
            loggerListenThread_.Start();

            // set the garbage collector to "do not influence" mode
            //System.GC.Collect();
            //System.Runtime.GCSettings.LatencyMode = System.Runtime.GCLatencyMode.LowLatency;
            //System.GC.SuppressFinalize(connector_);
        }

        private void KukaConnectorClient_Load(object sender, EventArgs e)
        {
            System.Net.IPHostEntry localIPHostEntry;
            System.Net.IPAddress localhostIPAddress;

            localhostIPAddress = System.Net.IPAddress.Parse("127.0.0.1");

            comboBoxRobotIPAddress.Items.Clear();

            comboBoxRobotIPAddress.Items.Add(localhostIPAddress.ToString());

            localIPHostEntry = System.Net.Dns.GetHostEntry(System.Net.Dns.GetHostName());
            if (localIPHostEntry.AddressList.Length > 0)
            {
                foreach (System.Net.IPAddress element in localIPHostEntry.AddressList)
                {
                    if (element.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                    {
                        comboBoxRobotIPAddress.Items.Add(element.ToString());
                    }
                }
            }
        }

        private void appButtonStartRobotListening_Click(object sender, EventArgs e)
        {
            System.Net.IPAddress localRobotIPAddress = null;

            if (comboBoxRobotIPAddress.SelectedItem.ToString().Length != 0)
            {
                try
                {
                    localRobotIPAddress = System.Net.IPAddress.Parse(comboBoxRobotIPAddress.SelectedItem.ToString());

                    connector_ = new KukaMatlabConnector.ConnectorObject("commanddoc.xml", localRobotIPAddress.ToString(), 6008);

                    connector_.initializeRobotListenThread();
                }
                catch
                {
                    loggingEntrys.Items.Add("KukaConnectorClient: could not parse selected IPAddress");
                }
            }
        }

        private void loggerListener()
        {
            TextLogger.TextLogger.loggingBufferEntry localEntry;
            String localString;

            uint pointerDiff = 0;

            // -----------------------------------------------------------------------------------------------------------------
            // run till programm gets closed
            // -----------------------------------------------------------------------------------------------------------------
            while (true)
            {
                if (connector_ != null)
                {
                    pointerDiff = connector_.logger_.getEntryCount();
                    for (int i = 0; i < pointerDiff; i++)
                    {
                        localEntry = connector_.logger_.getActEntry();
                        if (localEntry.id != 0)
                        {
                            makeLogEntry(localEntry.message);
                        }
                    }
                    
                    makePackagesReceivedCounterEntry(connector_.getPackagesReceivedCounter());
                    makePackagesSentCounterEntry(connector_.getPackagesSentCounter());
                    makeConnectionStateEntry(connector_.getRobotConnectionState());
                    makeCycleTimeEntry(connector_.getCommunicationTimeMicroSeconds());
                    makeDebugStringEntry(connector_.getDebugCommInfo());

                    localString = connector_.getCommandString();
                    if (localString != null) makeRobotCommandXMLEntrys(localString);

                    localString = connector_.getRobotInfoString();
                    if (localString != null) makeRobotInfoXMLEntrys(localString);

                    System.Threading.Thread.Sleep(50);
                }
            }
        }

        private void radioButtonAKorr_CheckedChanged(object sender, EventArgs e)
        {
            korrChooser_ = 2;

            label_Korr_1.Text = "AKorr1";
            label_Korr_2.Text = "AKorr2";
            label_Korr_3.Text = "AKorr3";
            label_Korr_4.Text = "AKorr4";
            label_Korr_5.Text = "AKorr5";
            label_Korr_6.Text = "AKorr6";

            label_actKorr1.Text = Convert.ToString(AKorr_1_);
            label_actKorr2.Text = Convert.ToString(AKorr_2_);
            label_actKorr3.Text = Convert.ToString(AKorr_3_);
            label_actKorr4.Text = Convert.ToString(AKorr_4_);
            label_actKorr5.Text = Convert.ToString(AKorr_5_);
            label_actKorr6.Text = Convert.ToString(AKorr_6_);
        }

        private void radioButtonRKorr_CheckedChanged(object sender, EventArgs e)
        {
            korrChooser_ = 1;

            label_Korr_1.Text = "RKorrX";
            label_Korr_2.Text = "RKorrY";
            label_Korr_3.Text = "RKorrZ";
            label_Korr_4.Text = "RKorrA";
            label_Korr_5.Text = "RKorrB";
            label_Korr_6.Text = "RKorrC";

            label_actKorr1.Text = Convert.ToString(RKorr_X_);
            label_actKorr2.Text = Convert.ToString(RKorr_Y_);
            label_actKorr3.Text = Convert.ToString(RKorr_Z_);
            label_actKorr4.Text = Convert.ToString(RKorr_A_);
            label_actKorr5.Text = Convert.ToString(RKorr_B_);
            label_actKorr6.Text = Convert.ToString(RKorr_C_);
        }

        private double getCorrectionValue()
        {
            double correctionValue;

            try { correctionValue = Convert.ToDouble(textBoxStepSize.Text); }
            catch { correctionValue = 0; }

            return correctionValue;
        }

        private void appButtonStopRobotListening_Click(object sender, EventArgs e)
        {
            connector_.stopRobotConnChannel();
        }

        private void KukaConnectorClient_FormClosing(Object sender, FormClosingEventArgs e)
        {
            if (connector_ != null)
            {
                connector_.stopRobotConnChannel();

                // wait till all channels are closed correctly
                while (connector_.getRobotConnectionState() != KukaMatlabConnector.ConnectorObject.ConnectionState.init)
                {
                    System.Threading.Thread.Sleep(100);
                }

                System.Windows.Forms.Application.Exit();
            }
        }

        private void appButtonResetStatistics_Click(object sender, EventArgs e)
        {
            loggingEntrys.Items.Clear();

            connector_.resetStatistics();
        }

        private void appButtonClose_Click(object sender, EventArgs e)
        {
            System.Windows.Forms.Application.Exit();
        }

        // ####################################################################################################################################################
        // #
        // #
        // #  methods to move the robot
        // #
        // #
        // ####################################################################################################################################################
        private void appButtonIncrementKorr1_Click(object sender, EventArgs e)
        {
            double correctionValue = getCorrectionValue();
            switch (korrChooser_)
            {
                case 1: { if (RKorr_X_ < correctionValue) { RKorr_X_ = RKorr_X_ + correctionValue; } else { RKorr_X_ = correctionValue; } connector_.modifyRKorrVariable("RKorrX", RKorr_X_.ToString()); label_actKorr1.Text = Convert.ToString(RKorr_X_); } break;
                case 2: { if (AKorr_1_ < correctionValue) { AKorr_1_ = AKorr_1_ + correctionValue; } else { AKorr_1_ = correctionValue; } connector_.modifyAKorrVariable("AKorr1", AKorr_1_.ToString()); label_actKorr1.Text = Convert.ToString(AKorr_1_); } break;
            }
        }

        private void appButtonDecrementKorr1_Click(object sender, EventArgs e)
        {
            double correctionValue = getCorrectionValue();
            switch (korrChooser_)
            {
                case 1: { if (RKorr_X_ > -correctionValue) { RKorr_X_ = RKorr_X_ - correctionValue; } else { RKorr_X_ = -correctionValue; } connector_.modifyRKorrVariable("RKorrX", RKorr_X_.ToString()); label_actKorr1.Text = Convert.ToString(RKorr_X_); } break;
                case 2: { if (AKorr_1_ > -correctionValue) { AKorr_1_ = AKorr_1_ - correctionValue; } else { AKorr_1_ = -correctionValue; } connector_.modifyAKorrVariable("AKorr1", AKorr_1_.ToString()); label_actKorr1.Text = Convert.ToString(AKorr_1_); } break;
            }
        }

        private void appButtonIncrementKorr2_Click(object sender, EventArgs e)
        {
            double correctionValue = getCorrectionValue();
            switch (korrChooser_)
            {
                case 1: { if (RKorr_Y_ < correctionValue) { RKorr_Y_ = RKorr_Y_ + correctionValue; } else { RKorr_Y_ = correctionValue; } connector_.modifyRKorrVariable("RKorrY", RKorr_Y_.ToString()); label_actKorr2.Text = Convert.ToString(RKorr_Y_); } break;
                case 2: { if (AKorr_2_ < correctionValue) { AKorr_2_ = AKorr_2_ + correctionValue; } else { AKorr_2_ = correctionValue; } connector_.modifyAKorrVariable("AKorr2", AKorr_2_.ToString()); label_actKorr2.Text = Convert.ToString(AKorr_2_); } break;
            }
        }

        private void appButtonDecrementKorr2_Click(object sender, EventArgs e)
        {
            double correctionValue = getCorrectionValue();
            switch (korrChooser_)
            {
                case 1: { if (RKorr_Y_ > -correctionValue) { RKorr_Y_ = RKorr_Y_ - correctionValue; } else { RKorr_Y_ = -correctionValue; } connector_.modifyRKorrVariable("RKorrY", RKorr_Y_.ToString()); label_actKorr2.Text = Convert.ToString(RKorr_Y_); } break;
                case 2: { if (AKorr_2_ > -correctionValue) { AKorr_2_ = AKorr_2_ - correctionValue; } else { AKorr_2_ = -correctionValue; } connector_.modifyAKorrVariable("AKorr2", AKorr_2_.ToString()); label_actKorr2.Text = Convert.ToString(AKorr_2_); } break;
            }
        }

        private void appButtonIncrementKorr3_Click(object sender, EventArgs e)
        {
            double correctionValue = getCorrectionValue();
            switch (korrChooser_)
            {
                case 1: { if (RKorr_Z_ < correctionValue) { RKorr_Z_ = RKorr_Z_ + correctionValue; } else { RKorr_Z_ = correctionValue; } connector_.modifyRKorrVariable("RKorrZ", RKorr_Z_.ToString()); label_actKorr3.Text = Convert.ToString(RKorr_Z_); } break;
                case 2: { if (AKorr_3_ < correctionValue) { AKorr_3_ = AKorr_3_ + correctionValue; } else { AKorr_3_ = correctionValue; } connector_.modifyAKorrVariable("AKorr3", AKorr_3_.ToString()); label_actKorr3.Text = Convert.ToString(AKorr_3_); } break;
            }
        }

        private void appButtonDecrementKorr3_Click(object sender, EventArgs e)
        {
            double correctionValue = getCorrectionValue();
            switch (korrChooser_)
            {
                case 1: { if (RKorr_Z_ > -correctionValue) { RKorr_Z_ = RKorr_Z_ - correctionValue; } else { RKorr_Z_ = -correctionValue; } connector_.modifyRKorrVariable("RKorrZ", RKorr_Z_.ToString()); label_actKorr3.Text = Convert.ToString(RKorr_Z_); } break;
                case 2: { if (AKorr_3_ > -correctionValue) { AKorr_3_ = AKorr_3_ - correctionValue; } else { AKorr_3_ = -correctionValue; } connector_.modifyAKorrVariable("AKorr3", AKorr_3_.ToString()); label_actKorr3.Text = Convert.ToString(AKorr_3_); } break;
            }
        }

        private void appButtonIncrementKorr4_Click(object sender, EventArgs e)
        {
            double correctionValue = getCorrectionValue();
            switch (korrChooser_)
            {
                case 1: { if (RKorr_A_ < correctionValue) { RKorr_A_ = RKorr_A_ + correctionValue; } else { RKorr_A_ = correctionValue; } connector_.modifyRKorrVariable("RKorrA", RKorr_A_.ToString()); label_actKorr4.Text = Convert.ToString(RKorr_A_); } break;
                case 2: { if (AKorr_4_ < correctionValue) { AKorr_4_ = AKorr_4_ + correctionValue; } else { AKorr_4_ = correctionValue; } connector_.modifyAKorrVariable("AKorr4", AKorr_4_.ToString()); label_actKorr4.Text = Convert.ToString(AKorr_4_); } break;
            }
        }

        private void appButtonDecrementKorr4_Click(object sender, EventArgs e)
        {
            double correctionValue = getCorrectionValue();
            switch (korrChooser_)
            {
                case 1: { if (RKorr_A_ > -correctionValue) { RKorr_A_ = RKorr_A_ - correctionValue; } else { RKorr_A_ = -correctionValue; } connector_.modifyRKorrVariable("RKorrA", RKorr_A_.ToString()); label_actKorr4.Text = Convert.ToString(RKorr_A_); } break;
                case 2: { if (AKorr_4_ > -correctionValue) { AKorr_4_ = AKorr_4_ - correctionValue; } else { AKorr_4_ = -correctionValue; } connector_.modifyAKorrVariable("AKorr4", AKorr_4_.ToString()); label_actKorr4.Text = Convert.ToString(AKorr_4_); } break;
            }
        }

        private void appButtonIncrementKorr5_Click(object sender, EventArgs e)
        {
            double correctionValue = getCorrectionValue();
            switch (korrChooser_)
            {
                case 1: { if (RKorr_B_ < correctionValue) { RKorr_B_ = RKorr_B_ + correctionValue; } else { RKorr_B_ = correctionValue; } connector_.modifyRKorrVariable("RKorrB", RKorr_B_.ToString()); label_actKorr5.Text = Convert.ToString(RKorr_B_); } break;
                case 2: { if (AKorr_5_ < correctionValue) { AKorr_5_ = AKorr_5_ + correctionValue; } else { AKorr_5_ = correctionValue; } connector_.modifyAKorrVariable("RKorr5", AKorr_5_.ToString()); label_actKorr5.Text = Convert.ToString(AKorr_5_); } break;
            }
        }

        private void appButtonDecrementKorr5_Click(object sender, EventArgs e)
        {
            double correctionValue = getCorrectionValue();
            switch (korrChooser_)
            {
                case 1: { if (RKorr_B_ > -correctionValue) { RKorr_B_ = RKorr_B_ - correctionValue; } else { RKorr_B_ = -correctionValue; } connector_.modifyRKorrVariable("RKorrB", RKorr_B_.ToString()); label_actKorr5.Text = Convert.ToString(RKorr_B_); } break;
                case 2: { if (AKorr_5_ > -correctionValue) { AKorr_5_ = AKorr_5_ - correctionValue; } else { AKorr_5_ = -correctionValue; } connector_.modifyAKorrVariable("AKorr5", AKorr_5_.ToString()); label_actKorr5.Text = Convert.ToString(AKorr_5_); } break;
            }
        }

        private void appButtonIcrementKorr6_Click(object sender, EventArgs e)
        {
            double correctionValue = getCorrectionValue();
            switch (korrChooser_)
            {
                case 1: { if (RKorr_C_ < correctionValue) { RKorr_C_ = RKorr_C_ + correctionValue; } else { RKorr_C_ = correctionValue; } connector_.modifyRKorrVariable("RKorrC", RKorr_C_.ToString()); label_actKorr6.Text = Convert.ToString(RKorr_C_); } break;
                case 2: { if (AKorr_6_ < correctionValue) { AKorr_6_ = AKorr_6_ + correctionValue; } else { AKorr_6_ = correctionValue; } connector_.modifyAKorrVariable("AKorr6", AKorr_6_.ToString()); label_actKorr6.Text = Convert.ToString(AKorr_6_); } break;
            }
        }

        private void appButtonDecrementKorr6_Click(object sender, EventArgs e)
        {
            double correctionValue = getCorrectionValue();
            switch (korrChooser_)
            {
                case 1: { if (RKorr_C_ > -correctionValue) { RKorr_C_ = RKorr_C_ - correctionValue; } else { RKorr_C_ = -correctionValue; } connector_.modifyRKorrVariable("RKorrC", RKorr_C_.ToString()); label_actKorr6.Text = Convert.ToString(RKorr_C_); } break;
                case 2: { if (AKorr_6_ > -correctionValue) { AKorr_6_ = AKorr_6_ - correctionValue; } else { AKorr_6_ = -correctionValue; } connector_.modifyAKorrVariable("AKorr6", AKorr_6_.ToString()); label_actKorr6.Text = Convert.ToString(AKorr_6_); } break;
            }
        }




        // ####################################################################################################################################################
        // #
        // #
        // #  methods to write statistical info into the text fields
        // #
        // #
        // ####################################################################################################################################################
        public delegate void makeLogEntryCallback(String message);
        void makeLogEntry(String message)
        {
            if (loggingEntrys.InvokeRequired)
            {
                makeLogEntryCallback d = new makeLogEntryCallback(makeLogEntry);
                loggingEntrys.Invoke(d, new object[] { message });
            }
            else
            {
                loggingEntrys.Items.Add(message);

                loggingEntrys.SelectedItem = loggingEntrys.Items.Count - 1;
            }
        }

        public delegate void makePackagesReceivedCounterCallback(long packagesReceived);
        void makePackagesReceivedCounterEntry(long packagesReceived)
        {
            if (label_packagesReceived.InvokeRequired)
            {
                makePackagesReceivedCounterCallback d = new makePackagesReceivedCounterCallback(makePackagesReceivedCounterEntry);
                label_packagesReceived.Invoke(d, new object[] { packagesReceived });
            }
            else
            {
                label_packagesReceived.Text = Convert.ToString(packagesReceived);
            }
        }

        public delegate void makePackagesSentCounterCallback(long packagesSent);
        void makePackagesSentCounterEntry(long packagesSent)
        {
            if (label_packagesSent.InvokeRequired)
            {
                makePackagesSentCounterCallback d = new makePackagesSentCounterCallback(makePackagesSentCounterEntry);
                label_packagesSent.Invoke(d, new object[] { packagesSent });
            }
            else
            {
                label_packagesSent.Text = Convert.ToString(packagesSent);
            }
        }

        public delegate void makeRobotCommandXMLEntryCallback(String xmlEntry);
        void makeRobotCommandXMLEntrys(String xmlEntry)
        {
            if (textbox_robotCommandData.InvokeRequired)
            {
                makeRobotCommandXMLEntryCallback d = new makeRobotCommandXMLEntryCallback(makeRobotCommandXMLEntrys);
                textbox_robotCommandData.Invoke(d, new object[] { xmlEntry });
            }
            else
            {
                textbox_robotCommandData.Text = xmlEntry;
            }
        }

        public delegate void makeRobotInfoXMLEntryCallback(String xmlEntry);
        void makeRobotInfoXMLEntrys(String xmlEntry)
        {
            if (textbox_robotInfoData.InvokeRequired)
            {
                makeRobotInfoXMLEntryCallback d = new makeRobotInfoXMLEntryCallback(makeRobotInfoXMLEntrys);
                textbox_robotInfoData.Invoke(d, new object[] { xmlEntry });
            }
            else
            {
                textbox_robotInfoData.Text = xmlEntry;
            }
        }

        public delegate void makeConnectionStateEntryCallback(KukaMatlabConnector.ConnectorObject.ConnectionState connectionState);
        void makeConnectionStateEntry(KukaMatlabConnector.ConnectorObject.ConnectionState connectionState)
        {
            if (label_connectionState.InvokeRequired)
            {
                makeConnectionStateEntryCallback d = new makeConnectionStateEntryCallback(makeConnectionStateEntry);
                label_connectionState.Invoke(d, new object[] { connectionState });
            }
            else
            {
                switch (connectionState)
                {
                    case KukaMatlabConnector.ConnectorObject.ConnectionState.closeRequest: label_connectionState.Text = "closeRequest"; break;
                    case KukaMatlabConnector.ConnectorObject.ConnectionState.closing: label_connectionState.Text = "closing"; break;
                    case KukaMatlabConnector.ConnectorObject.ConnectionState.connecting: label_connectionState.Text = "connecting"; break;
                    case KukaMatlabConnector.ConnectorObject.ConnectionState.init: label_connectionState.Text = "init"; break;
                    case KukaMatlabConnector.ConnectorObject.ConnectionState.listening: label_connectionState.Text = "listening"; break;
                    case KukaMatlabConnector.ConnectorObject.ConnectionState.running: label_connectionState.Text = "running"; break;
                    case KukaMatlabConnector.ConnectorObject.ConnectionState.starting: label_connectionState.Text = "starting"; break;
                }
            }
        }

        public delegate void makeCycleTimeCallback(double cycleTime);
        void makeCycleTimeEntry(double cycleTime)
        {
            if (label_packagesSent.InvokeRequired)
            {
                makeCycleTimeCallback d = new makeCycleTimeCallback(makeCycleTimeEntry);
                label_cycleTime.Invoke(d, new object[] { cycleTime });
            }
            else
            {
                label_cycleTime.Text = Convert.ToString(cycleTime);
            }
        }

        public delegate void makeDebugStringCallback(String debug);
        void makeDebugStringEntry(String debug)
        {
            if (label_packagesSent.InvokeRequired)
            {
                makeDebugStringCallback d = new makeDebugStringCallback(makeDebugStringEntry);
                label_debugString.Invoke(d, new object[] { debug });
            }
            else
            {
                label_debugString.Text = Convert.ToString(debug);
            }
        }
    }
}
