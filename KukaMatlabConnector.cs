using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Sockets;
using System.Net;
using System.Threading.Tasks;
using System.ComponentModel;

/* ===================================================================================================================================== */
/**
 *  Namespace KukaMatlabConnector
 * 
 * 
 */
/* ===================================================================================================================================== */
namespace KukaMatlabConnector
{
    /* ------------------------------------------------------------------------------------------------------------------------------------------------- */
    /**
     *  Class ConnectorObject
     * 
     * 
     * 
     */
    /* ------------------------------------------------------------------------------------------------------------------------------------------------- */
    public class ConnectorObject
    {
        // enumeration with all available connection states
        public enum ConnectionState { init = 1, starting, listening, connecting, running, closeRequest, closing }

        // defines the connection state of the class to the robot
        ConnectionState robotConnectionState_;

        // socket to the robot controller
        System.Net.Sockets.Socket serverRobotComListener_;

        // port on which the server has to listen
        uint robotCommunicationPort_;

        // ip address on which the server has to listen
        System.Net.IPAddress robotListenIPAddress_;

        // path to valid xml document which has to be sent to the robot in the specific cycle
        String pathToCommandXMLDocument_;

        // xml container which are send to the controller and coming from it
        System.Xml.XmlDocument commandXML_;
        System.Xml.XmlDocument receiveXML_;

        // stopwatches to check if the cycles are fast enough
        System.Diagnostics.Stopwatch stopWatch_;
        System.Diagnostics.Stopwatch stopWatchReceive_;
        System.Diagnostics.Stopwatch stopWatchSend_;

        // strings where the command and the actual robot info is saved
        String receiveString_;
        String sendString_;

        // count of the received and send packages, resets after every start of the communication
        long receivedPackagesCount_;
        long sendPackagesCount_;

        // byte buffer for the communication
        byte[] comBuffer_ { get; set; }
        // communication buffer read pointer
        uint comBufferReadPointer_ = 0;
        // communication buffer write pointer
        uint comBufferWritePointer_ = 0;
        // communication buffer size
        uint comBufferSize_ = 20480;
        // variable to check if the pattern has been found to send the next command to the robot
        bool patternFound_ = false;
        // pattern which we have to search for
        byte pattern_ = 10; // normally 0x0a => CR two times, unique after every xml packet

        // thread instance which makes the communication to the robot
        System.Threading.Thread kukaListenThread_;

        // mutexes which lock the write access to the command and info strings and xml containers
        System.Threading.Mutex mutexRobotCommandString_;
        System.Threading.Mutex mutexRobotInfoString_;
        System.Threading.Mutex mutexRobotCommandXML_;
        System.Threading.Mutex mutexRobotInfoXML_;

        // socket which contains the connection to the robot controller
        System.Net.Sockets.Socket comRobotHandler_ = null;

        // current correction values in cartesian and axis values
        double RKorr_X_, RKorr_Y_, RKorr_Z_, RKorr_A_, RKorr_B_, RKorr_C_;
        double AKorr_1_, AKorr_2_, AKorr_3_, AKorr_4_, AKorr_5_, AKorr_6_;

        // if set to true no correction is allowed
        bool lockCorrectionCommands_;

        // variables to save the statistical times
        long communicationTimeMilliSeconds_;
        long communicationTimeTicks_;
        long delayedPackagesCount_;
        long delayedPackagesMilliSecondsComm_;
        long delayedPackagesMilliSecondsSend_;
        long delayedPackagesMilliSecondsReceive_;
        long delayedPackagesTicksComm_;

        // signals the connected application that the wrapper is waiting for the next robot info data
        bool nextCycleStarted_; 

        // TextLogger
        public TextLogger.TextLogger logger_;

        // ------------------------------------------------------------------------------------------------------------------
        // buffer and other variables for synchron mode writing
        // ------------------------------------------------------------------------------------------------------------------
        bool synchronModeRKorrActive_;
        bool synchronModeAKorrActive_;

        const uint synchronAKorrBufferSize_ = 255;
        const uint synchronRKorrBufferSize_ = 255;

        uint synchronAKorrBufferReadPointer_;
        uint synchronAKorrBufferWritePointer_;
        uint synchronRKorrBufferReadPointer_;
        uint synchronRKorrBufferWritePointer_;

        synchronAKorrBufferStruct[] synchronAKorrBuffer_ ;
        synchronRKorrBufferStruct[] synchronRKorrBuffer_;

        public struct synchronAKorrBufferStruct
        {
            public double AKorr1;
            public double AKorr2;
            public double AKorr3;
            public double AKorr4;
            public double AKorr5;
            public double AKorr6;

            public synchronAKorrBufferStruct(double extAKorr1, double extAKorr2, double extAKorr3, double extAKorr4, double extAKorr5, double extAKorr6)
            {
                AKorr1 = extAKorr1;
                AKorr2 = extAKorr2;
                AKorr3 = extAKorr3;
                AKorr4 = extAKorr4;
                AKorr5 = extAKorr5;
                AKorr6 = extAKorr6;
            }
        }

        public struct synchronRKorrBufferStruct
        {
            public double RKorrX;
            public double RKorrY;
            public double RKorrZ;
            public double RKorrA;
            public double RKorrB;
            public double RKorrC;

            public synchronRKorrBufferStruct(double extRKorrX, double extRKorrY, double extRKorrZ, double extRKorrA, double extRKorrB, double extRKorrC)
            {
                RKorrX = extRKorrX;
                RKorrY = extRKorrY;
                RKorrZ = extRKorrZ;
                RKorrA = extRKorrA;
                RKorrB = extRKorrB;
                RKorrC = extRKorrC;
            }
        }

        /* ----------------------------------------------------------------------------------------------------------------------------------------------- */
        /**
         *  @brief 
         * 
         * 
         * 
         */
        /* ----------------------------------------------------------------------------------------------------------------------------------------------- */
        public ConnectorObject(String pathToCommandXMLDocument, String listenIPAddress, uint listenPort)
        {
            int iError = 0;

            // initialize all instances
            logger_ = new TextLogger.TextLogger( 255 );

            // initialize all communication variables and check if valid (if possible)
            if ((listenPort > 1024) && (listenPort < 65535))
            {
                robotCommunicationPort_ = listenPort;
            }
            else
            {
                robotCommunicationPort_ = 6008;
            }

            // initialize comBuffer and according variables
            comBuffer_ = new byte[comBufferSize_];
            comBufferReadPointer_ = 0;
            comBufferWritePointer_ = 0;

            // check if the path is correct otherwise take a file located in the same folder as the client is
            if ((pathToCommandXMLDocument != null) && (pathToCommandXMLDocument.Length != 0))
            {
                pathToCommandXMLDocument_ = pathToCommandXMLDocument;
            }
            else
            {
                pathToCommandXMLDocument_ = "commanddoc.xml";
            }

            // check if the ipAddress is correct otherwise write a logMessage and exit
            try
            {
                robotListenIPAddress_ = System.Net.IPAddress.Parse(listenIPAddress);
            }
            catch
            {
                logger_.addMessage("please give a correct IP-address...");
                iError = 1;
            }
            robotConnectionState_ = new ConnectionState();

            // initialize the connection states
            setRobotConnectionState(ConnectionState.init);

            // initialize the stop watches
            stopWatch_ = new System.Diagnostics.Stopwatch();
            stopWatchReceive_ = new System.Diagnostics.Stopwatch();
            stopWatchSend_ = new System.Diagnostics.Stopwatch();

            receivedPackagesCount_ = 0;
            sendPackagesCount_ = 0;

            // allow commands from initial state
            lockCorrectionCommands_ = false;

            // reset synchron mode flags
            synchronModeAKorrActive_ = false;
            synchronModeRKorrActive_ = false;

            synchronAKorrBuffer_ = new synchronAKorrBufferStruct[synchronAKorrBufferSize_];
            synchronRKorrBuffer_ = new synchronRKorrBufferStruct[synchronRKorrBufferSize_];

            mutexRobotCommandString_ = new System.Threading.Mutex(false, "robotCommandString");
            mutexRobotInfoString_ = new System.Threading.Mutex(false, "robotInfoString");
            mutexRobotCommandXML_ = new System.Threading.Mutex(false, "robotCommandXML");
            mutexRobotInfoXML_ = new System.Threading.Mutex(false, "robotInfoXML");

            communicationTimeMilliSeconds_ = 0;
            communicationTimeTicks_ = 0;
            delayedPackagesCount_ = 0;

            receiveXML_ = new System.Xml.XmlDocument();
            commandXML_ = new System.Xml.XmlDocument();

            commandXML_ = getXMLCommandDocument(pathToCommandXMLDocument_);
            if (commandXML_ == null)
            {
                logger_.addMessage("could not find command XML-file...");
                iError = 2;
            }

            if (iError == 0)
            {
                logger_.addMessage("constructor done...");
            }
        }



        /* ----------------------------------------------------------------------------------------------------------------------------------------------- */
        /**
         *  @brief    initializes the thread which handles the communication to the robot
         * 
         *  @retval   none
         */
        /* ----------------------------------------------------------------------------------------------------------------------------------------------- */
        public void initializeRobotListenThread()
        {
            setRobotConnectionState(ConnectionState.starting);

            receivedPackagesCount_ = 0;
            sendPackagesCount_ = 0;

            delayedPackagesCount_ = 0;

            // create thread instance and set the threadpriority to the highest value
            kukaListenThread_ = new System.Threading.Thread(new System.Threading.ThreadStart(kukaListener), 10000000);
            kukaListenThread_.Priority = System.Threading.ThreadPriority.Highest;
            kukaListenThread_.Start();
        }

        /* ----------------------------------------------------------------------------------------------------------------------------------------------- */
        /**
         *  @brief    trys to get the xml document which has to be send to the robot
         * 
         *  @param    path ... string with the path to the document
         * 
         *  @retval   new XmlDocument Instance
         */
        /* ----------------------------------------------------------------------------------------------------------------------------------------------- */
        private System.Xml.XmlDocument getXMLCommandDocument(String pathToXMLDocument)
        {
            System.Xml.XmlDocument localXMLDocument;

            localXMLDocument = null;

            // try to load the xml document
            try
            {
                // load variable with xmlDocument instance
                localXMLDocument = new System.Xml.XmlDocument();

                // prepare xml answer to the kuka robot
                localXMLDocument.PreserveWhitespace = true;
                localXMLDocument.Load(pathToXMLDocument);
            }
            catch
            {
                localXMLDocument = null;
            }

            return localXMLDocument;
        }

        /* ----------------------------------------------------------------------------------------------------------------------------------------------- */
        /**
         *  @brief   starts the server and waits for a connection which has to be established from a client (robot or virtual)
         * 
         *  @retval  none
         */
        /* ----------------------------------------------------------------------------------------------------------------------------------------------- */
        private System.Net.Sockets.Socket startRobotComServerAndWaitForConnection()
        {
            System.Net.IPEndPoint localEndPoint;
            System.Net.Sockets.Socket localComHandler;   // create system socket

            localEndPoint = null;
            localComHandler = null;

            // create an IPEndPoint with the previosly selected IPAddress and communication port
            localEndPoint = new System.Net.IPEndPoint(robotListenIPAddress_, (int)robotCommunicationPort_);

            // create the TCP/IP socket connection
            serverRobotComListener_ = new System.Net.Sockets.Socket(System.Net.Sockets.AddressFamily.InterNetwork, System.Net.Sockets.SocketType.Stream, System.Net.Sockets.ProtocolType.Tcp);
            serverRobotComListener_.NoDelay = true;

            setRobotConnectionState(ConnectionState.listening);

            try
            {
                // open socket and listen on network
                serverRobotComListener_.Bind(localEndPoint);
                serverRobotComListener_.Listen(1);

                // program is suspended while waiting for an incoming connection, bind the first request
                logger_.addMessage("listening for RobotCom on ip:" + robotListenIPAddress_.ToString() + " port:" + robotCommunicationPort_.ToString() + ", wait for connection...");
                localComHandler = serverRobotComListener_.Accept();

                localComHandler.NoDelay = true;

                // no more connections are welcome so close the comListener object
                serverRobotComListener_.Close();

                if ((localComHandler != null) && (getRobotConnectionState() == ConnectionState.listening))
                {
                    setRobotConnectionState(ConnectionState.connecting);
                    logger_.addMessage("connection established! starting communication with RobotCom...");
                }
                else
                {
                    logger_.addMessage("connection canceled! good bye...");
                }
            }
            catch
            {
                logger_.addMessage("cant open port, please give it another try...");
                serverRobotComListener_.Close();
                localComHandler = null;
            }

            return localComHandler;
        }

        /* ----------------------------------------------------------------------------------------------------------------------------------------------- */
        /**
         *  @brief     robot listen thread starting helper method, connects to the robot and initiates the synchron communication
         * 
         *  @retval    none
         */
        /* ----------------------------------------------------------------------------------------------------------------------------------------------- */
        private void kukaListener()
        {
            // prepare sendString for first transport to robot
            setCommandString(getCommandInnerXML());

            // check if the ipAddress is set
            if (robotListenIPAddress_ != null)
            {
                while ((getRobotConnectionState() == ConnectionState.running) || (getRobotConnectionState() == ConnectionState.starting))
                {
                    // start the server and wait for the connection which has to be established from the kuka robot 
                    // goto ST_SKIPSENS in the kuka robot programm...
                    comRobotHandler_ = startRobotComServerAndWaitForConnection();
                    if (comRobotHandler_ != null)
                    {
                        // now start the server and periodically receive the robot data and send the prepared XML file
                        // should stay in the following line till the connection is refused
                        robotServerLoop(comRobotHandler_);

                        // when the loop is finished then close the socket
                        comRobotHandler_.Close();
                    }
                    else
                    {
                        logger_.addMessage("could not open the server-robot client connection => check network connection!");
                    }
                }
            }
            else
            {
                // otherwise throw an error
                logger_.addMessage("no ip host entry found to connect => check network connection!");
            }

            logger_.addMessage("connection closed, server closed, see you next time...");

            setRobotConnectionState(ConnectionState.init);
        }

        /* ----------------------------------------------------------------------------------------------------------------------------------------------- */
        /**
         *  @brief    server loop which waits for data from robot and answers with XML File
         * 
         *  @retval   none
         */
        /* ----------------------------------------------------------------------------------------------------------------------------------------------- */
        private void robotServerLoop(System.Net.Sockets.Socket comHandler)
        {
            // variable declarations
            byte[] localIncomingDataByteBuffer;                           // data buffer for incoming data
            byte[] localReceivedFullMessageBytes;
            byte[] sendMessage;
            int bytesReceived;
            String localCommandString;
            String localInfoString;

            // variable initializations

            localReceivedFullMessageBytes = null;
            sendMessage = new Byte[2048];

            // set state to running
            setRobotConnectionState(ConnectionState.running);

            // --------------------------------------------------
            // now lets start the endles loop
            // --------------------------------------------------            
            while (true)
            {
                // reset and start the stopwatch to measure the time between two robot info cycles
                stopWatch_.Reset();
                stopWatch_.Start();

                // command the garbage collector to collect at the beginning of each cycle
                System.GC.Collect();

                // signal to the connected application that the command data is ready to be modified
                nextCycleStarted_ = true;

                // ------------------------------------------------------------
                // wait for data and receive bytes
                // ------------------------------------------------------------
                try
                {
                    while (patternFound_ == false)
                    {
                        String testString;

                        localIncomingDataByteBuffer = new Byte[2048];                 // load byte buffer with instance

                        bytesReceived = comHandler.Receive(localIncomingDataByteBuffer);
                        if (bytesReceived == 0)
                        {
                            logger_.addMessage("client closed connection (bytesReceived=0)");
                            setRobotConnectionState(ConnectionState.closing);
                            break; // client closed socket
                        }

                        testString = System.Text.Encoding.ASCII.GetString(localIncomingDataByteBuffer, 0, bytesReceived);

                        // start stopwatch for receiving task
                        stopWatchReceive_.Reset();
                        stopWatchReceive_.Start();

                        // fill the received bytes into the local buffer and check if a full message came from robot
                        localReceivedFullMessageBytes = giveRightByteArray(localIncomingDataByteBuffer, bytesReceived);
                    }

                    // increment the packages counter
                    receivedPackagesCount_++;

                    // clear the pattern found flag
                    patternFound_ = false;
                }
                catch
                {
                    logger_.addMessage("connection closed from remote host...\n\r");
                    setRobotConnectionState(ConnectionState.closing);
                    break;
                }

                stopWatchReceive_.Stop();

                stopWatchSend_.Reset();
                stopWatchSend_.Start();

                // -------------------------------------------------------------------------------------
                // signals the connector that the sending operation just started => 
                // the external system has to wait until this variable goes again to true
                // -------------------------------------------------------------------------------------
                nextCycleStarted_ = false;

                // --------------------------------------------------------------
                // only try to load the xml stuff when there is data available
                // --------------------------------------------------------------
                if (localReceivedFullMessageBytes != null)
                {
                    // -------------------------------------------------------------------------
                    // check if the string is valid and then save it to the locally xml storage
                    // -------------------------------------------------------------------------
                    if (checkReceivedMessage(localReceivedFullMessageBytes) == true)
                    {
                        try
                        {
                            // check if there is synchron AKorr active
                            doSynchronAKorr();

                            // check if there is synchron RKorr active
                            doSynchronRKorr();

                            // get the sendString variable under mutex protection from getter method
                            localCommandString = getCommandString();

                            // get the receiveString variable under mutex protection from getter method
                            localInfoString = getRobotInfoString();

                            // mirror the IPO counter you received yet
                            localCommandString = mirrorInterpolationCounter(localInfoString, localCommandString);

                            // get bytes out of string
                            sendMessage = System.Text.Encoding.ASCII.GetBytes(localCommandString);

                            // send data to robot
                            comHandler.Send(sendMessage, 0, sendMessage.Length, System.Net.Sockets.SocketFlags.None);

                            sendPackagesCount_++;

                            // copy the edited string under mutex protection back
                            setCommandString(localCommandString);
                        }
                        catch
                        {
                            logger_.addMessage("could not send XML string");
                        }
                    }
                }

                stopWatchSend_.Stop();

                stopWatch_.Stop();
                communicationTimeMilliSeconds_ = stopWatch_.ElapsedMilliseconds;
                communicationTimeTicks_ = stopWatch_.ElapsedTicks;

                // ----------------------------------------------------------------------------------------------
                // count the delayed packages
                // ----------------------------------------------------------------------------------------------
                if (communicationTimeMilliSeconds_ > 16)
                {
                    delayedPackagesCount_++;
                    delayedPackagesMilliSecondsComm_ = communicationTimeMilliSeconds_;
                    delayedPackagesMilliSecondsSend_ = stopWatchSend_.ElapsedTicks;
                    delayedPackagesTicksComm_ = stopWatch_.ElapsedTicks;
                    delayedPackagesMilliSecondsReceive_ = stopWatchReceive_.ElapsedTicks;
                }

                // ----------------------------------------------------------------------------------------------
                // close communication channel to robot if state changed to closing
                // ----------------------------------------------------------------------------------------------
                if ((getRobotConnectionState() == ConnectionState.closeRequest) ||(getRobotConnectionState() == ConnectionState.closing) )
                {
                    setRobotConnectionState(ConnectionState.closing);
                    break;
                }
            }
        }

        /* ----------------------------------------------------------------------------------------------------------------------------------------------- */
        /**
         *  @brief    checks if the received message is a valid one. it searches for the Rob xml tag
         *            
         *  @param    receivedMessageBytes ... byte array with the received message
         * 
         *  @retval   bool ... false if the rob string was not found, true if it was found
         */
        /* ----------------------------------------------------------------------------------------------------------------------------------------------- */
        private bool checkReceivedMessage(byte[] receivedMessageBytes)
        {
            bool robStringFound = false;
            String localReceivedFullMessageString = null;

            // copy the received string
            localReceivedFullMessageString = System.Text.Encoding.ASCII.GetString(receivedMessageBytes, 0, receivedMessageBytes.Length);

            // when loaded try to find the rob node
            robStringFound = checkStringIfValid(localReceivedFullMessageString);

            // if there is no rob node continue else do the other stuff
            if (robStringFound == true)
            {
                // convert bytes to string and save into variable
                setRobotInfoString(localReceivedFullMessageString);
            }
            else
            {
                logger_.addMessage("no robot specific nodes found!");
                robStringFound = false;
            }

            return robStringFound;
        }

        /* ----------------------------------------------------------------------------------------------------------------------------------------------- */
        /**
         *  @brief    fills the locally byte buffer and checks if during the copy operation the pattern 0x0a 0x0a can be found => end of robot message
         *            
         *  @param    buffer ... buffer with the message which has been received from the robot
         *  @param    size ... received bytes which have been received from the robot
         * 
         *  @retval   byte array ... with the found message, null if no full valid message is here
         */
        /* ----------------------------------------------------------------------------------------------------------------------------------------------- */
        private byte[] giveRightByteArray(byte[] buffer, long size)
        {
            byte[] localByteBuffer;
            uint localComBufferWritePointer;
            uint aktEntryCount;
            uint countOuterLoop;
            uint countInnerLoop;
            bool firstPatternFound_;

            // reset all variables, especially pattern found... when we come here in that function we copy the byte buffers and check if an end of one message comes
            localByteBuffer = null;
            localComBufferWritePointer = 0;
            patternFound_ = false;
            firstPatternFound_ = false;
            countInnerLoop = 0;
            countOuterLoop = 0;
            aktEntryCount = 0;

            // check if size is not equal to zero
            if (size != 0)
            {
                for (countOuterLoop = 0; countOuterLoop < size; countOuterLoop++)
                {
                    // save write pointer
                    localComBufferWritePointer = comBufferWritePointer_;

                    // increment write pointer and check if incrementation was right
                    if (!incrementComBufferWritePointer())
                    {
                        // copy byte to old write pointer place
                        comBuffer_[localComBufferWritePointer] = buffer[countOuterLoop];

                        // check if the first pattern was before and the current pattern is exactly the same
                        if ((firstPatternFound_ == true) && (buffer[countOuterLoop] == pattern_) && (patternFound_ == false))
                        {
                            // set pattern foudn true => only one message for one method call
                            patternFound_ = true;

                            aktEntryCount = getComBufferEntryCount();

                            // get some memor to do the copy operation
                            localByteBuffer = new byte[aktEntryCount];

                            // increment the read pointer to the place where the write pointer stands
                            for (countInnerLoop = 0; countInnerLoop < aktEntryCount; countInnerLoop++)
                            {
                                localByteBuffer[countInnerLoop] = comBuffer_[comBufferReadPointer_];
                                incrementComBufferReadPointer();
                            }
                        }

                        // set firstPatternFound to false because the string was found and we only have to copy the rest of the message
                        firstPatternFound_ = false;

                        if (buffer[countOuterLoop] == pattern_)
                        {
                            firstPatternFound_ = true;
                        }
                    }
                }
            }

            return localByteBuffer;
        }

        /* ----------------------------------------------------------------------------------------------------------------------------------------------- */
        /**
         *  @brief    calculates the current count of bytes entered in the buffer => it is the subtraction of write - readpointer and it also checks if
         *            a buffer wrap happened
         *            
         *  @retval   uint ... count of received bytes
         */
        /* ----------------------------------------------------------------------------------------------------------------------------------------------- */
        private uint getComBufferEntryCount()
        {
            uint localEntryCount = 0;
            int localPointerDiff = 0;

            localPointerDiff = Convert.ToInt32(comBufferWritePointer_) - Convert.ToInt32(comBufferReadPointer_);

            // check if the pointer difference is positive or negative
            if (localPointerDiff > 0)
            {
                localEntryCount = comBufferWritePointer_ - comBufferReadPointer_;
            }
            else if (localPointerDiff < 0)
            {
                localEntryCount = (comBufferSize_ - comBufferReadPointer_) + comBufferWritePointer_;
            }
            else
            {
                localEntryCount = 0;
            }

            return localEntryCount;
        }

        /* ----------------------------------------------------------------------------------------------------------------------------------------------- */
        /**
         *  @brief    increments the combuffer read pointer, if it is larger than the buffer size after incrementation => set to 0
         * 
         *  @retval   none
         */
        /* ----------------------------------------------------------------------------------------------------------------------------------------------- */
        private void incrementComBufferReadPointer()
        {
            comBufferReadPointer_++;
            if (comBufferReadPointer_ > (comBufferSize_ - 1))
            {
                comBufferReadPointer_ = 0;
            }
        }

        /* ----------------------------------------------------------------------------------------------------------------------------------------------- */
        /**
         *  @brief    increments the combuffer write pointer, if it is larger than the buffer size after incrementation => set to 0
         *            if it is equal to the readpointer after incrementation => buffer full back to the old value
         * 
         *  @retval   none
         */
        /* ----------------------------------------------------------------------------------------------------------------------------------------------- */
        private bool incrementComBufferWritePointer()
        {
            uint localWritePointer;
            bool errReturn = false;

            localWritePointer = comBufferWritePointer_;

            comBufferWritePointer_++;
            if (comBufferWritePointer_ > (comBufferSize_ - 1))
            {
                comBufferWritePointer_ = 0;
            }

            // writepointer can not be equal to readpointer after incrementation
            if (comBufferWritePointer_ == comBufferReadPointer_)
            {
                errReturn = true;
                comBufferWritePointer_ = localWritePointer;
            }

            return errReturn;
        }

        /* ----------------------------------------------------------------------------------------------------------------------------------------------- */
        /**
         *  @brief    increments the synchron AKorr read pointer, if it is larger than the buffer size after incrementation => set to 0
         * 
         *  @retval   none
         */
        /* ----------------------------------------------------------------------------------------------------------------------------------------------- */
        private void incrementSynchronAKorrReadPointer()
        {
            synchronAKorrBufferReadPointer_++;
            if (synchronAKorrBufferReadPointer_ > (synchronAKorrBufferSize_ - 1))
            {
                synchronAKorrBufferReadPointer_ = 0;
            }
        }

        /* ----------------------------------------------------------------------------------------------------------------------------------------------- */
        /**
         *  @brief    increments the synchron RKorr read pointer, if it is larger than the buffer size after incrementation => set to 0
         * 
         *  @retval   none
         */
        /* ----------------------------------------------------------------------------------------------------------------------------------------------- */
        private void incrementSynchronRKorrReadPointer()
        {
            synchronRKorrBufferReadPointer_++;
            if (synchronRKorrBufferReadPointer_ > (synchronRKorrBufferSize_ - 1))
            {
                synchronRKorrBufferReadPointer_ = 0;
            }
        }

        /* ----------------------------------------------------------------------------------------------------------------------------------------------- */
        /**
         *  @brief    increments the synchron AKorr write pointer, if it is larger than the buffer size after incrementation => set to 0
         *            if it is equal to the readpointer after incrementation => buffer full back to the old value
         * 
         *  @retval   none
         */
        /* ----------------------------------------------------------------------------------------------------------------------------------------------- */
        private bool incrementSynchronAKorrWritePointer()
        {
            uint localWritePointer;
            bool errReturn = false;

            localWritePointer = synchronAKorrBufferWritePointer_;

            synchronAKorrBufferWritePointer_++;
            if (synchronAKorrBufferWritePointer_ > (synchronAKorrBufferSize_ - 1))
            {
                synchronAKorrBufferWritePointer_ = 0;
            }

            // writepointer can not be equal to readpointer after incrementation
            if (synchronAKorrBufferWritePointer_ == synchronAKorrBufferReadPointer_)
            {
                errReturn = true;
                synchronAKorrBufferWritePointer_ = localWritePointer;
            }

            return errReturn;
        }

        /* ----------------------------------------------------------------------------------------------------------------------------------------------- */
        /**
         *  @brief    increments the synchron RKorr write pointer, if it is larger than the buffer size after incrementation => set to 0
         *            if it is equal to the readpointer after incrementation => buffer full back to the old value
         * 
         *  @retval   none
         */
        /* ----------------------------------------------------------------------------------------------------------------------------------------------- */
        private bool incrementSynchronRKorrWritePointer()
        {
            uint localWritePointer;
            bool errReturn = false;

            localWritePointer = synchronRKorrBufferWritePointer_;

            synchronRKorrBufferWritePointer_++;
            if (synchronRKorrBufferWritePointer_ > (synchronRKorrBufferSize_ - 1))
            {
                synchronRKorrBufferWritePointer_ = 0;
            }

            // writepointer can not be equal to readpointer after incrementation
            if (synchronRKorrBufferWritePointer_ == synchronRKorrBufferReadPointer_)
            {
                errReturn = true;
                synchronRKorrBufferWritePointer_ = localWritePointer;
            }

            return errReturn;
        }

        /* ----------------------------------------------------------------------------------------------------------------------------------------------- */
        /**
         *  @brief    calculates the current count of AKorr commands entered in the buffer => it is the subtraction of write - readpointer and it also checks if
         *            a buffer wrap happened
         *            
         *  @retval   uint ... count of received bytes
         */
        /* ----------------------------------------------------------------------------------------------------------------------------------------------- */
        private uint getSynchronAKorrBufferEntryCount()
        {
            uint localEntryCount = 0;
            int localPointerDiff = 0;

            localPointerDiff = Convert.ToInt32(synchronAKorrBufferWritePointer_) - Convert.ToInt32(synchronAKorrBufferReadPointer_);

            // check if the pointer difference is positive or negative
            if (localPointerDiff > 0)
            {
                localEntryCount = synchronAKorrBufferWritePointer_ - synchronAKorrBufferReadPointer_;
            }
            else if (localPointerDiff < 0)
            {
                localEntryCount = (synchronAKorrBufferSize_ - synchronAKorrBufferReadPointer_) + synchronAKorrBufferWritePointer_;
            }
            else
            {
                localEntryCount = 0;
            }

            return localEntryCount;
        }

        /* ----------------------------------------------------------------------------------------------------------------------------------------------- */
        /**
         *  @brief    calculates the current count of RKorr commands entered in the buffer => it is the subtraction of write - readpointer and it also checks if
         *            a buffer wrap happened
         *            
         *  @retval   uint ... count of received bytes
         */
        /* ----------------------------------------------------------------------------------------------------------------------------------------------- */
        private uint getSynchronRKorrBufferEntryCount()
        {
            uint localEntryCount = 0;
            int localPointerDiff = 0;

            localPointerDiff = Convert.ToInt32(synchronRKorrBufferWritePointer_) - Convert.ToInt32(synchronRKorrBufferReadPointer_);

            // check if the pointer difference is positive or negative
            if (localPointerDiff > 0)
            {
                localEntryCount = synchronRKorrBufferWritePointer_ - synchronRKorrBufferReadPointer_;
            }
            else if (localPointerDiff < 0)
            {
                localEntryCount = (synchronRKorrBufferSize_ - synchronRKorrBufferReadPointer_) + synchronRKorrBufferWritePointer_;
            }
            else
            {
                localEntryCount = 0;
            }

            return localEntryCount;
        }

        /* ----------------------------------------------------------------------------------------------------------------------------------------------- */
        /**
         *  @brief    sends the current AKorr command set in the synchron buffer to the robot
         * 
         *  @retval   none
         */
        /* ----------------------------------------------------------------------------------------------------------------------------------------------- */
        private void doSynchronAKorr()
        {
            if (synchronModeAKorrActive_ == true)
            {
                if (getSynchronAKorrBufferEntryCount() != 0)
                {
                    modifyAKorrVariable("AKorr1", synchronAKorrBuffer_[synchronAKorrBufferReadPointer_].AKorr1.ToString());
                    modifyAKorrVariable("AKorr2", synchronAKorrBuffer_[synchronAKorrBufferReadPointer_].AKorr2.ToString());
                    modifyAKorrVariable("AKorr3", synchronAKorrBuffer_[synchronAKorrBufferReadPointer_].AKorr3.ToString());
                    modifyAKorrVariable("AKorr4", synchronAKorrBuffer_[synchronAKorrBufferReadPointer_].AKorr4.ToString());
                    modifyAKorrVariable("AKorr5", synchronAKorrBuffer_[synchronAKorrBufferReadPointer_].AKorr5.ToString());
                    modifyAKorrVariable("AKorr6", synchronAKorrBuffer_[synchronAKorrBufferReadPointer_].AKorr6.ToString());

                    incrementSynchronAKorrReadPointer();
                }
            }
        }

        /* ----------------------------------------------------------------------------------------------------------------------------------------------- */
        /**
         *  @brief    sends the current RKorr command set in the synchron buffer to the robot
         * 
         *  @retval   none
         */
        /* ----------------------------------------------------------------------------------------------------------------------------------------------- */
        private void doSynchronRKorr()
        {
            if (synchronModeRKorrActive_ == true)
            {
                if( getSynchronRKorrBufferEntryCount() != 0 )
                {
                    modifyRKorrVariable("RKorrX", synchronRKorrBuffer_[synchronRKorrBufferReadPointer_].RKorrX.ToString());
                    modifyRKorrVariable("RKorrY", synchronRKorrBuffer_[synchronRKorrBufferReadPointer_].RKorrY.ToString());
                    modifyRKorrVariable("RKorrZ", synchronRKorrBuffer_[synchronRKorrBufferReadPointer_].RKorrZ.ToString());
                    modifyRKorrVariable("RKorrA", synchronRKorrBuffer_[synchronRKorrBufferReadPointer_].RKorrA.ToString());
                    modifyRKorrVariable("RKorrB", synchronRKorrBuffer_[synchronRKorrBufferReadPointer_].RKorrB.ToString());
                    modifyRKorrVariable("RKorrC", synchronRKorrBuffer_[synchronRKorrBufferReadPointer_].RKorrC.ToString());

                    incrementSynchronRKorrReadPointer();
                }
            }
        }

        /* ----------------------------------------------------------------------------------------------------------------------------------------------- */
        /**
         *  @brief    There is a specific counting value coming from the kuka controller inside the receive string.
         *            This value has to be sent back so separate it and insert it into the xml send string
         *            
         *  @param    receive ... the string which comes from the kuka controller
         *  @param    send ... the string which we will send to the controller
         * 
         *  @retval   string ... the modified string for the controller
         */
        /* ----------------------------------------------------------------------------------------------------------------------------------------------- */
        private String mirrorInterpolationCounter(String receiveString, String sendString)
        {
            // tried XML separation first, but it was too slow for realtime comm...

            // separate the IPO counter which comes from the robot and save it as string 
            // +6 because "<IPOC>" has 6 character...
            int startIpocReceiveIndex = receiveString.IndexOf("<IPOC>") + 6;
            int endIpocReceiveIndex = receiveString.IndexOf("</IPOC>");

            string ipocount = receiveString.Substring(startIpocReceiveIndex, endIpocReceiveIndex - startIpocReceiveIndex);

            // find the position where this number has to be inserted
            int startIpocSendIndex = sendString.IndexOf("<IPOC>") + 6;
            int endIpocSendIndex = sendString.IndexOf("</IPOC>");

            // remove the old value and insert the actual ipoc value
            sendString = sendString.Remove(startIpocSendIndex, endIpocSendIndex - startIpocSendIndex);
            sendString = sendString.Insert(startIpocSendIndex, ipocount);

            return sendString;
        }

        /* ----------------------------------------------------------------------------------------------------------------------------------------------- */
        /**
         *  @brief    checks the received string if it has some valid characters included
         *            
         *  @param    receiveString ... the string which we will send to the controller
         * 
         *  @retval   bool ... returns true if the string is valid, returns false if it is invalid
         */
        /* ----------------------------------------------------------------------------------------------------------------------------------------------- */
        private bool checkStringIfValid(String receiveString)
        {
            bool boReturn = false;

            if (receiveString.IndexOf("Rob") != -1)
            {
                boReturn = true;
            }
            else
            {
                boReturn = false;
            }

            return boReturn;
        }

        /* ----------------------------------------------------------------------------------------------------------------------------------------------- */
        /**
         *  @brief    modify the trajectory with one command
         *  
         *  @param    command ... given the command for all cartesian axis in the following format: (R:1:2:3:4:5:6) => 1-6 are double values e.g.: 0,234
         *            
         *  @retval   uint errorreturn != 0 if error happened
         */
        /* ----------------------------------------------------------------------------------------------------------------------------------------------- */
        public uint modifyRKorr(String command)
        {
            uint uiError = 0;
            String localCommandString = command;
            String localKorrType = null;
            String[] localKorrAttributes = null;

            localKorrAttributes = command.Split(':');

            if (localKorrAttributes.Length == 7)
            {
                localKorrType = localKorrAttributes[0];

                if (String.Compare(localKorrType, "R") == 0)
                {
                    modifyRKorrVariable("RKorrX", localKorrAttributes[1]);
                    modifyRKorrVariable("RKorrY", localKorrAttributes[2]);
                    modifyRKorrVariable("RKorrZ", localKorrAttributes[3]);
                    modifyRKorrVariable("RKorrA", localKorrAttributes[4]);
                    modifyRKorrVariable("RKorrB", localKorrAttributes[5]);
                    modifyRKorrVariable("RKorrC", localKorrAttributes[6]);
                }
                else
                {
                    logger_.addMessage("illegal commands received...");
                    uiError = 1;
                }
            }
            else
            {
                logger_.addMessage("illergal commands received...."); 
                uiError = 1;
            }  

            return uiError;
        }

        /* ----------------------------------------------------------------------------------------------------------------------------------------------- */
        /**
         *  @brief    modify the trajectory with one command
         *  
         *  @param    command ... given the command for all axis in the following format: (A:1:2:3:4:5:6) => 1-6 are double values e.g.: 0,234
         *            
         *  @retval   uint errorreturn != 0 if error happened
         */
        /* ----------------------------------------------------------------------------------------------------------------------------------------------- */
        public uint modifyAKorr(String command)
        {
            uint uiError = 0;
            String localCommandString = command;
            String localKorrType = null;
            String[] localKorrAttributes = null;

            localKorrAttributes = command.Split(':');
            if (localKorrAttributes.Length == 7)
            {
                localKorrType = localKorrAttributes[0];

                if (String.Compare(localKorrType, "A") == 0)
                {
                    modifyAKorrVariable("AKorr1", localKorrAttributes[1]);
                    modifyAKorrVariable("AKorr2", localKorrAttributes[2]);
                    modifyAKorrVariable("AKorr3", localKorrAttributes[3]);
                    modifyAKorrVariable("AKorr4", localKorrAttributes[4]);
                    modifyAKorrVariable("AKorr5", localKorrAttributes[5]);
                    modifyAKorrVariable("AKorr6", localKorrAttributes[6]);
                }
                else
                {
                    logger_.addMessage("illegal commands received...");
                    uiError = 1;
                }
            }
            else
            {
                logger_.addMessage("illergal commands received....");
                uiError = 2;
            }    

            return uiError;
        }

        /* ----------------------------------------------------------------------------------------------------------------------------------------------- */
        /**
         *  @brief    modifys the specified cartesian correction value, has to come in the following format 
         *  
         *  @param    variable ... the param which has to be modified by the next value as string (e.g.: "RKorrX")
         *  @param    value    ... the desired value as string (e.g.: "0,1")
         *            
         *  @retval   XmlDocument ... actual robot info data
         */
        /* ----------------------------------------------------------------------------------------------------------------------------------------------- */
        public uint modifyRKorrVariable(String variable, String value)
        {
            System.Xml.XmlNodeList localRKorrNodeList;
            double valueToSend = 0;
            uint uiError = 0;

            localRKorrNodeList = null;

            // lock all correction possibilities
            if (isCorrectionCommandAllowed() == true)
            {
                // check if the given variable is a valid one
                if ((variable == "RKorrX") || (variable == "RKorrY") || (variable == "RKorrZ") || (variable == "RKorrA") || (variable == "RKorrB") || (variable == "RKorrC"))
                {
                    try
                    {
                        valueToSend = Convert.ToDouble(value);
                        uiError = 0;

                        // first save variables local
                        switch (variable)
                        {
                            case "RKorrX": RKorr_X_ = valueToSend; break;  //X
                            case "RKorrY": RKorr_Y_ = valueToSend; break;  //Y
                            case "RKorrZ": RKorr_Z_ = valueToSend; break;  //Z
                            case "RKorrA": RKorr_A_ = valueToSend; break;  //A
                            case "RKorrB": RKorr_B_ = valueToSend; break;  //B
                            case "RKorrC": RKorr_C_ = valueToSend; break;  //C
                        }

                        // search for the right tag
                        localRKorrNodeList = commandXML_.GetElementsByTagName("RKorr");

                        if (localRKorrNodeList != null)
                        {
                            // change the attribute to the desired value
                            switch (variable)
                            {
                                case "RKorrX": localRKorrNodeList[0].Attributes["X"].Value = Convert.ToString(valueToSend, System.Globalization.CultureInfo.InvariantCulture); break;  //X
                                case "RKorrY": localRKorrNodeList[0].Attributes["Y"].Value = Convert.ToString(valueToSend, System.Globalization.CultureInfo.InvariantCulture); break;  //Y
                                case "RKorrZ": localRKorrNodeList[0].Attributes["Z"].Value = Convert.ToString(valueToSend, System.Globalization.CultureInfo.InvariantCulture); break;  //Z
                                case "RKorrA": localRKorrNodeList[0].Attributes["A"].Value = Convert.ToString(valueToSend, System.Globalization.CultureInfo.InvariantCulture); break;  //A
                                case "RKorrB": localRKorrNodeList[0].Attributes["B"].Value = Convert.ToString(valueToSend, System.Globalization.CultureInfo.InvariantCulture); break;  //B
                                case "RKorrC": localRKorrNodeList[0].Attributes["C"].Value = Convert.ToString(valueToSend, System.Globalization.CultureInfo.InvariantCulture); break;  //C
                            }
                        }

                        setCommandString(commandXML_.InnerXml);
                    }
                    catch
                    {
                        uiError = 1;
                    }
                }
                else
                {
                    uiError = 2;
                }
            }

            return uiError;
        }

        /* ----------------------------------------------------------------------------------------------------------------------------------------------- */
        /**
         *  @brief    modifys the specified Axis correction value, has to come in the following format 
         *  
         *  @param    variable ... the param which has to be modified by the next value as string (e.g.: "AKorr1 ... 6")
         *  @param    value    ... the desired value as string (e.g.: "0,1")
         *            
         *  @retval   XmlDocument ... actual robot info data
         */
        /* ----------------------------------------------------------------------------------------------------------------------------------------------- */
        public uint modifyAKorrVariable(String variable, String value)
        {
            System.Xml.XmlNodeList localAKorrNodeList;
            double valueToSend = 0;
            uint uiError = 0;

            localAKorrNodeList = null;

            // check if correction is allowed
            if (isCorrectionCommandAllowed() == true)
            {
                // check if the variable is a valid one
                if ((variable == "AKorr1") || (variable == "AKorr2") || (variable == "AKorr3") || (variable == "AKorr4") || (variable == "AKorr5") || (variable == "AKorr6"))
                {
                    try
                    {
                        valueToSend = Convert.ToDouble(value);
                        uiError = 0;

                        // first save variables local
                        switch (variable)
                        {
                            case "AKorr1": AKorr_1_ = valueToSend; break;  //A1
                            case "AKorr2": AKorr_2_ = valueToSend; break;  //A2
                            case "AKorr3": AKorr_3_ = valueToSend; break;  //A3
                            case "AKorr4": AKorr_4_ = valueToSend; break;  //A4
                            case "AKorr5": AKorr_5_ = valueToSend; break;  //A5
                            case "AKorr6": AKorr_6_ = valueToSend; break;  //A6
                        }

                        // search for the right tag
                        localAKorrNodeList = commandXML_.GetElementsByTagName("AKorr");

                        if (localAKorrNodeList != null)
                        {
                            // change the attribute to the desired value
                            switch (variable)
                            {
                                case "AKorr1": localAKorrNodeList[0].Attributes["A1"].Value = Convert.ToString(valueToSend, System.Globalization.CultureInfo.InvariantCulture); break;  //A1
                                case "AKorr2": localAKorrNodeList[0].Attributes["A2"].Value = Convert.ToString(valueToSend, System.Globalization.CultureInfo.InvariantCulture); break;  //A2
                                case "AKorr3": localAKorrNodeList[0].Attributes["A3"].Value = Convert.ToString(valueToSend, System.Globalization.CultureInfo.InvariantCulture); break;  //A3
                                case "AKorr4": localAKorrNodeList[0].Attributes["A4"].Value = Convert.ToString(valueToSend, System.Globalization.CultureInfo.InvariantCulture); break;  //A4
                                case "AKorr5": localAKorrNodeList[0].Attributes["A5"].Value = Convert.ToString(valueToSend, System.Globalization.CultureInfo.InvariantCulture); break;  //A5
                                case "AKorr6": localAKorrNodeList[0].Attributes["A6"].Value = Convert.ToString(valueToSend, System.Globalization.CultureInfo.InvariantCulture); break;  //A6
                            }
                        }

                        setCommandString(commandXML_.InnerXml);
                    }
                    catch
                    {
                        uiError = 1;
                    }
                }
                else
                {
                    uiError = 2;
                }
            }

            return uiError;
        }

        public bool startSynchronAKorr()
        {
            bool localReturn;

            if ((synchronModeAKorrActive_ == false) && (synchronModeRKorrActive_ == false))
            {
                synchronModeAKorrActive_ = true;

                localReturn = false;
            }
            else 
            {
                localReturn = true;
            }

            return localReturn;
        }

        public void endSynchronAKorr()
        {
            synchronModeAKorrActive_ = false;
        }

        public bool modifyAKorrSynchron( String command )
        {
            bool errReturn = false;
            uint localWritePointer;
            String localCommandString = command;
            String localKorrType = null;
            String[] localKorrAttributes = null;

            if (synchronModeAKorrActive_ == true)
            {
                // safe write pointer
                localWritePointer = synchronAKorrBufferWritePointer_;
                // increment write pointer
                if( !incrementSynchronAKorrWritePointer() )
                {
                    localKorrAttributes = command.Split(':');
                    if (localKorrAttributes.Length == 7)
                    {
                        localKorrType = localKorrAttributes[0];

                        if (localKorrType == "A")
                        {
                            synchronAKorrBuffer_[localWritePointer].AKorr1 = Convert.ToDouble(localKorrAttributes[1]);
                            synchronAKorrBuffer_[localWritePointer].AKorr2 = Convert.ToDouble(localKorrAttributes[2]);
                            synchronAKorrBuffer_[localWritePointer].AKorr3 = Convert.ToDouble(localKorrAttributes[3]);
                            synchronAKorrBuffer_[localWritePointer].AKorr4 = Convert.ToDouble(localKorrAttributes[4]);
                            synchronAKorrBuffer_[localWritePointer].AKorr5 = Convert.ToDouble(localKorrAttributes[5]);
                            synchronAKorrBuffer_[localWritePointer].AKorr6 = Convert.ToDouble(localKorrAttributes[6]);
                        }
                        else
                        {
                            logger_.addMessage("illegal command received...");
                            errReturn = true;
                        }
                    }
                    else
                    {
                        logger_.addMessage("illegal commands received...");
                        errReturn = true;
                    }
                }
            }
            else
            {
                logger_.addMessage("start synchron AKorr mode first...");
                errReturn = true;
            }

            return (errReturn);
        }

        public bool startSynchronRKorr()
        {
            bool localReturn;

            if ((synchronModeAKorrActive_ == false) && (synchronModeRKorrActive_ == false))
            {
                synchronModeRKorrActive_ = true;

                localReturn = false;
            }
            else
            {
                localReturn = true;
            }

            return localReturn;
        }

        public void endSynchronRKorr()
        {
            synchronModeRKorrActive_ = false;
        }

        public bool modifyRKorrSynchron(String command)
        {
            bool errReturn = false;
            uint localWritePointer;
            String localCommandString = command;
            String localKorrType = null;
            String[] localKorrAttributes = null;

            if (synchronModeRKorrActive_ == true)
            {
                // safe write pointer
                localWritePointer = synchronRKorrBufferWritePointer_;
                // increment write pointer
                if( !incrementSynchronRKorrWritePointer() )
                {
                    localKorrAttributes = command.Split(':');
                    if (localKorrAttributes.Length == 7)
                    {
                        localKorrType = localKorrAttributes[0];

                        if (localKorrType == "R")
                        {
                            synchronRKorrBuffer_[localWritePointer].RKorrX = Convert.ToDouble(localKorrAttributes[1]);
                            synchronRKorrBuffer_[localWritePointer].RKorrY = Convert.ToDouble(localKorrAttributes[2]);
                            synchronRKorrBuffer_[localWritePointer].RKorrZ = Convert.ToDouble(localKorrAttributes[3]);
                            synchronRKorrBuffer_[localWritePointer].RKorrA = Convert.ToDouble(localKorrAttributes[4]);
                            synchronRKorrBuffer_[localWritePointer].RKorrB = Convert.ToDouble(localKorrAttributes[5]);
                            synchronRKorrBuffer_[localWritePointer].RKorrC = Convert.ToDouble(localKorrAttributes[6]);
                        }
                        else
                        {
                            logger_.addMessage("illegal command received...");
                            errReturn = true;
                        }
                    }
                    else
                    {
                        logger_.addMessage("illegal commands received...");
                        errReturn = true;
                    }
                }
            }
            else
            {
                logger_.addMessage("start synchron RKorr mode first...");
                errReturn = true;
            }

            return (errReturn);
        }

        /* ----------------------------------------------------------------------------------------------------------------------------------------------- */
        /**
         *  @brief    returns a debugable string with information about the current loop times and so on...
         *            
         *  @retval   String ... debug string to see what times it took for communication
         */
        /* ----------------------------------------------------------------------------------------------------------------------------------------------- */
        public String getDelayedCommInfo()
        {
            String returnal;

            returnal = delayedPackagesCount_.ToString()+" "+delayedPackagesMilliSecondsComm_.ToString()+" "+delayedPackagesMilliSecondsSend_.ToString()+" "+delayedPackagesTicksComm_.ToString()+" "+delayedPackagesMilliSecondsReceive_.ToString();

            return returnal;
        }

        /* ----------------------------------------------------------------------------------------------------------------------------------------------- */
        /**
         *  @brief    returns the actual time needed for one cycle in Milliseconds
         *            
         *  @retval   long ... actual time needed for one cycle in Milliseconds
         */
        /* ----------------------------------------------------------------------------------------------------------------------------------------------- */
        public long getCommunicationTimeMilliSeconds()
        {
            long returnal;

            returnal = communicationTimeMilliSeconds_;

            return returnal;
        }

        /* ----------------------------------------------------------------------------------------------------------------------------------------------- */
        /**
         *  @brief    returns the actual time needed for one cycle in ticks
         *            
         *  @retval   long ... actual time needed for one cycle in ticks
         */
        /* ----------------------------------------------------------------------------------------------------------------------------------------------- */
        public long getCommunicationTimeTicks()
        {
            long returnal;

            returnal = communicationTimeTicks_;

            return returnal;
        }

        /* ----------------------------------------------------------------------------------------------------------------------------------------------- */
        /**
         *  @brief    returns the actual time needed for one cycle in us
         *            
         *  @retval   long ... actual time needed for one cycle in us
         */
        /* ----------------------------------------------------------------------------------------------------------------------------------------------- */
        public double getCommunicationTimeMicroSeconds()
        {
            double returnal;
            double frequency;
            double ticks;

            frequency = Convert.ToDouble(System.Diagnostics.Stopwatch.Frequency);
            ticks = Convert.ToDouble(communicationTimeTicks_);

            returnal = 1000000000.0 * ticks / frequency / 1000;

            return returnal;
        }

        /* ----------------------------------------------------------------------------------------------------------------------------------------------- */
        /**
         *  @brief    reset all statistic counters
         *            
         *  @retval   none
         */
        /* ----------------------------------------------------------------------------------------------------------------------------------------------- */
        public void resetStatistics()
        {
            // TODO: count packages send, received and here i can delete the statistic counter...
            receivedPackagesCount_ = 0;
            sendPackagesCount_ = 0;
        }

        /* ----------------------------------------------------------------------------------------------------------------------------------------------- */
        /**
         *  @brief    gives the actual command string back
         *            
         *  @retval   command string as String
         */
        /* ----------------------------------------------------------------------------------------------------------------------------------------------- */
        public String getCommandString()
        {
            String returnString = null;

            mutexRobotCommandString_.WaitOne();

            returnString = sendString_;

            mutexRobotCommandString_.ReleaseMutex();

            return returnString;
        }

        /* ----------------------------------------------------------------------------------------------------------------------------------------------- */
        /**
         *  @brief    sets the actual command string which has to be send to the robot
         *         
         *  @param    newCommandString ... sets the actual command string
         * 
         *  @retval   none
         */
        /* ----------------------------------------------------------------------------------------------------------------------------------------------- */
        private void setCommandString(String newCommandString)
        {
            mutexRobotCommandString_.WaitOne();

            sendString_ = newCommandString;

            mutexRobotCommandString_.ReleaseMutex();
        }

        /* ----------------------------------------------------------------------------------------------------------------------------------------------- */
        /**
         *  @brief    gives the actual robot info string back
         *         
         *  @retval   robot info string as String
         */
        /* ----------------------------------------------------------------------------------------------------------------------------------------------- */
        public String getRobotInfoString()
        {
            String returnString = null;

            mutexRobotInfoString_.WaitOne();

            returnString = receiveString_;

            mutexRobotInfoString_.ReleaseMutex();

            return returnString;
        }

        /* ----------------------------------------------------------------------------------------------------------------------------------------------- */
        /**
         *  @brief    sets the actual robot info string, usually from the cyclic task
         *         
         *  @param    newRobotInfoString ... sets the actual robot info string
         *  
         *  @retval   none
         */
        /* ----------------------------------------------------------------------------------------------------------------------------------------------- */
        private void setRobotInfoString(String newRobotInfoString)
        {
            mutexRobotInfoString_.WaitOne();

            receiveString_ = newRobotInfoString;

            mutexRobotInfoString_.ReleaseMutex();
        }

        /* ----------------------------------------------------------------------------------------------------------------------------------------------- */
        /**
         *  @brief    returns the actual count of send packages
         *         
         *  @retval   total number of send packages from the current session
         */
        /* ----------------------------------------------------------------------------------------------------------------------------------------------- */
        public long getPackagesSentCounter()
        {
            long returnal;

            returnal = sendPackagesCount_;

            return returnal;
        }

        /* ----------------------------------------------------------------------------------------------------------------------------------------------- */
        /**
         *  @brief    returns the actual count of received packages
         *         
         *  @retval   total number of received packages from the current session
         */
        /* ----------------------------------------------------------------------------------------------------------------------------------------------- */
        public long getPackagesReceivedCounter()
        {
            long returnal;

            returnal = receivedPackagesCount_;

            return returnal;
        }

        /* ----------------------------------------------------------------------------------------------------------------------------------------------- */
        /**
         *  @brief    loads the given command string as xml document and saves it in the local static xmlDocument variable
         *         
         *  @param    newCommandString ... command string which has to be set as XML-Document
         *  
         *  @retval   none
         */
        /* ----------------------------------------------------------------------------------------------------------------------------------------------- */
        private void loadCommandXML(String newCommandString)
        {
            mutexRobotCommandXML_.WaitOne();

            commandXML_.LoadXml(newCommandString);

            mutexRobotCommandXML_.ReleaseMutex();
        }

        /* ----------------------------------------------------------------------------------------------------------------------------------------------- */
        /**
         *  @brief    gives the actual data from the xml-data saved command string back
         *         
         *  @retval   actual xml saved command string
         */
        /* ----------------------------------------------------------------------------------------------------------------------------------------------- */
        private String getCommandInnerXML()
        {
            String returnString;

            mutexRobotCommandXML_.WaitOne();

            returnString = commandXML_.InnerXml;

            mutexRobotCommandXML_.ReleaseMutex();

            return returnString;
        }

        /* ----------------------------------------------------------------------------------------------------------------------------------------------- */
        /**
         *  @brief    sets the locally saved static xmlDocument variable with the given robot info string
         *         
         *  @param    newInfoString ... info string which has to be loaded in the xmldocument variable
         * 
         *  @retval   none
         */
        /* ----------------------------------------------------------------------------------------------------------------------------------------------- */
        private void loadInfoXML(String newInfoString)
        {
            mutexRobotInfoXML_.WaitOne();

            receiveXML_.LoadXml(newInfoString);

            mutexRobotInfoXML_.ReleaseMutex();
        }

        /* ----------------------------------------------------------------------------------------------------------------------------------------------- */
        /**
         *  @brief    gives the data from the xmldocument variable (robot info) as string back
         * 
         *  @retval   string with the actual data saved in the robot info xmldocument
         */
        /* ----------------------------------------------------------------------------------------------------------------------------------------------- */
        private String getRobotInfoInnerXML()
        {
            String returnString;

            mutexRobotInfoXML_.WaitOne();

            returnString = receiveXML_.InnerXml;

            mutexRobotInfoXML_.ReleaseMutex();

            return returnString;
        }

        /* ----------------------------------------------------------------------------------------------------------------------------------------------- */
        /**
         *  @brief    stops the actual connected robot communication
         * 
         *  @retval   none
         */
        /* ----------------------------------------------------------------------------------------------------------------------------------------------- */
        public void stopRobotConnChannel()
        {
            if (getRobotConnectionState() == ConnectionState.running)
            {
                setRobotConnectionState(ConnectionState.closeRequest);
            }
            else
            {
                try
                {
                    setRobotConnectionState(ConnectionState.init);
                    serverRobotComListener_.Close();
                }
                catch { }
            }
        }

        /* ----------------------------------------------------------------------------------------------------------------------------------------------- */
        /**
         *  @brief    sets the actual robot communication state
         * 
         *  @param    nextState ... state which has to be set
         * 
         *  @retval   none
         */
        /* ----------------------------------------------------------------------------------------------------------------------------------------------- */
        private void setRobotConnectionState(ConnectionState nextState)
        {
            robotConnectionState_ = nextState;
        }

        /* ----------------------------------------------------------------------------------------------------------------------------------------------- */
        /**
         *  @brief    sets the actual robot communication state
         * 
         *  @retval   gives the actual robot communication state back
         */
        /* ----------------------------------------------------------------------------------------------------------------------------------------------- */
        public ConnectionState getRobotConnectionState()
        {
            ConnectionState localConnectionState;

            localConnectionState = robotConnectionState_;

            return localConnectionState;
        }

        /* ----------------------------------------------------------------------------------------------------------------------------------------------- */
        /**
         *  @brief    emergency function, locks the correction commands and sets all correction values to zero
         * 
         *  @retval   none
         */
        /* ----------------------------------------------------------------------------------------------------------------------------------------------- */
        public void lockCorrectionCommands()
        {
            // clear all modifications on axis correction attributes
            modifyAKorrVariable("AKorr1", "0,0");
            modifyAKorrVariable("AKorr2", "0,0");
            modifyAKorrVariable("AKorr3", "0,0");
            modifyAKorrVariable("AKorr4", "0,0");
            modifyAKorrVariable("AKorr5", "0,0");
            modifyAKorrVariable("AKorr6", "0,0");

            // clear all modifications on cartesian correction attributes
            modifyRKorrVariable("RKorrX", "0,0");
            modifyRKorrVariable("RKorrY", "0,0");
            modifyRKorrVariable("RKorrZ", "0,0");
            modifyRKorrVariable("RKorrA", "0,0");
            modifyRKorrVariable("RKorrB", "0,0");
            modifyRKorrVariable("RKorrC", "0,0");

            // lock all future commands
            lockCorrectionCommands_ = true;
        }

        /* ----------------------------------------------------------------------------------------------------------------------------------------------- */
        /**
         *  @brief    unlocks the locking state, from this time on it is allowed to send correction commands
         * 
         *  @retval   none
         */
        /* ----------------------------------------------------------------------------------------------------------------------------------------------- */
        public void unlockCorrectionCommands()
        {
            lockCorrectionCommands_ = false;

            // clear all modifications on axis correction attributes
            modifyAKorrVariable("AKorr1", "0,0");
            modifyAKorrVariable("AKorr2", "0,0");
            modifyAKorrVariable("AKorr3", "0,0");
            modifyAKorrVariable("AKorr4", "0,0");
            modifyAKorrVariable("AKorr5", "0,0");
            modifyAKorrVariable("AKorr6", "0,0");

            // clear all modifications on cartesian correction attributes
            modifyRKorrVariable("RKorrX", "0,0");
            modifyRKorrVariable("RKorrY", "0,0");
            modifyRKorrVariable("RKorrZ", "0,0");
            modifyRKorrVariable("RKorrA", "0,0");
            modifyRKorrVariable("RKorrB", "0,0");
            modifyRKorrVariable("RKorrC", "0,0");

        }

        /* ----------------------------------------------------------------------------------------------------------------------------------------------- */
        /**
         *  @brief    gives the locking state of the command allowed flag
         * 
         *  @retval   bool ... true if it is allowed to do correctioncommands, fals if not
         */
        /* ----------------------------------------------------------------------------------------------------------------------------------------------- */
        public bool isCorrectionCommandAllowed()
        {
            bool localCorrectionCommand;

            if (lockCorrectionCommands_ == true)
            {
                localCorrectionCommand = false;
            }
            else
            {
                localCorrectionCommand = true;
            }
            return localCorrectionCommand;
        }

        /* ----------------------------------------------------------------------------------------------------------------------------------------------- */
        /**
         *  @brief    if the wrapper waits for the next robot info, the nextCycleStarte_ boolean is set => signals the connected application that 
         *            the data for the next cycle can be modified
         * 
         *  @retval   bool ... true if the wrapper waits for the next robot info data
         */
        /* ----------------------------------------------------------------------------------------------------------------------------------------------- */
        public bool isNextCycleStarted()
        {
            bool localBool;

            localBool = nextCycleStarted_;

            return localBool;
        }
    }
}
