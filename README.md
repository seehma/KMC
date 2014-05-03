#KukaMatlabConnector
This simple dll establishes a connection to the RSI-program of a Kuka Robot Controller.

#API
####ConnectorObject( String pathToCommandXMLDocument, String listenIPAddress, uint listenPort )
This is the constructor of connector object. It initializes the object for usage.
You need the following params:
* __String pathToCommandXMLDocument__ ... Its the full path to the command xml document. Its the document which has to be sent to the robot for commanding the correction values
* __String listenIPAddress__ ... The IP Address on which the server has to listen for the RSI connection to the robot
* __uint listenPort__ ... The Port on which the server has to listen for the RSI connection to the robot

After initialization the server does not listen you will have to call the method initializeRobotListenThread() for this.

####initializeRobotListenThread()
This method initializes the listen thread. If the given IP-Address is correct, exists and the port is not used by another programm the robot connection state changes to __listening__.
It is possible to get the connection state by calling getRobotConnectionState() method.

####stopRobotConnChannel()
If the connection is up this method stops the connection to the robot. The robot connection state changes to __init__ after calling this method. 
It is possible to get the connection state by calling getRobotConnectionState() method.

####lockCorrectionCommands()
This method is used to stop all correction commands and lock them against correcting again. It sets all correction values (AKorr and RKorr) to zero.
You can unlock this with the unlockCorrectionCommands().

####unlockCorrectionCommands()
This method is used to unlock the lock possibility to correct your robot by AKorr or RKorr. 
Nothing will happen if a locking has never been made.

####resetStatistics()
Resets all counting values or saved maximum times to get a fresh view on whats gonna happen.
Maybe you wanna call this method before any new movement or something like this...

####getCommandString()
This method returns you the actual command xml-string which was send to the robot in the last cycle as string.
It contains the whole correction values as you set it with the modify commands.

For example the following structure:

`<Sen Type="CoRob" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xsi:noNamespaceSchemaLocation="ExternalData.xsd">`
`  <Dat TaskType="b">`
`    <EStr>Info: ERX Message!</EStr>`
`    <RKorr X="0.0000" Y="0.0000" Z="0.0000" A="0.0000" B="0.0000" C="0.0000" />`
`    <AKorr A1="0.0000" A2="0.0000" A3="0.0000" A4="0.0000" A5="0.0000" A6="0.0000" />`
`    <Tech x="1" p3="0.0000" p4="0.0000" p5="0.0000" p3x1="0.0000" p4x1="0.0000" p5x1="0.0000" p3x2="0.0000" p4x2="0.0000" p5x2="0.0000" p3x3="0.0000" p4x3="0.0000" p5x3="0.0000" />`
`    <DiO>0</DiO>`
`    <IPOC>0000000000</IPOC>`
`  </Dat>`
`</Sen>`

####getRobotInfoString()
This command returns you the actual robot info xml-string which the wrapper got from the robot in the last cycle as string.
It contains all infos you described in the specific xml-file on the kuka controller in the INIT directory.

For example the following structure:
`<Rob Type="KUKA">`
`  <RIst X="1" Y="2" Z="3" A="4" B="5" C="6"/>`
`  <RSol X="7" Y="8" Z="9" A="10" B="11" C="12"/>`
`  <AIPos A1="13" A2="14" A3="15" A4="16" A5="17" A6="18"/>`
`  <ASPos A1="19" A2="19" A3="20" A4="21" A5="22" A6="23"/>`
`  <MACur A1="24" A2="25" A3="26" A4="27" A5="28" A6="29"/>`
`  <Delay D="30"/>`
`  <Tech C11="31" C12="32" C13="0.0 000" C14="33" C15="34" C16="35" C17="36" C18="37" C19="38.00" C110="39.000"/>`
`  <DIL>40</DIL>`
`  <Digout o1="41" o2="42" o3="43"/>`
`  <ST_Source>44.0000</ST_Source>`
`  <IPOC>0000000000</IPOC>`
`</Rob>`

####getPackagesSentCounter()
This method returns the actual count of sent packages to the kuka controller till the call of initializeRobotListenThread() and
until the call of the stop method. The counter value will automatically reset after a new connection is created.

####getPackagesReceivedCounter()
This method returns the actual count of received packages from the kuka controller till the call of initializeRobotListenThread() and
until the call of the stop method. The counter value will automatically reset after a new connection is created.

####getRobotConnectionState()
By calling this mehtod you get the actual connection state of the server.
The following states are available:

* __init__ ... Its the state after constructing the object or if a connection has been closed
* __starting__ ... Its the state after thread starting method has been called till the socket has been opened
* __listening__ ... This state is active when waiting after the robot to connect
* __connecting__ ... Its connecting after creating socket and till the first packages has been send
* __running__ ... When the real time communication has started its state is running
* __closeRequest__ ... When you request the closing of the communication channel
* __closing__ ... Till it is closed

The name of the Enumeration is ConnectionState (KukaMatlabConnector.ConnectorObject.ConnectionState)

####isCorrectionCommandAllowed()
If the correction commands are allowed this method returns true, otherwise false.
It depends on the locking and connection state.

* true ... When the connection is up (running) and not locked
* false ... When the connection is down (!= running) or locked

####isNextCycleStarted()
This method signals you when the next cycle is started.

* true ... The wrapper is just sending the commands to the robot.
* false ... The wrapper waits for its next cycle (usually after 12 Milliseconds)

####modifyAKorr( String command )
If you want to correct the axis values, this is the right method. 
This method corrects all axis at once, the modifyAKorrVariable(...) method corrects only one axis at once.
The command string has to be the following structure "A:0,0:0,1:0,2:0,3:0,4:0,5". 
This command would correct Axis 1 by 0.0, Axis2 by 0.1, Axis3 by 0.2, Axis4 by 0.3, Axis5 by 0.4 and Axis6 by 0.5.

more detailed information will follow here...

####modifyRKorr( String command )
If you want to correct the cartesian values of the TCP, world or base coordinates this is the right method. 
This method corrects all cartesian values at once, the modifyRKorrVariable(...) method corrects only one cartesian value at once.
The command string has to be the following structure "R:0,0:0,1:0,2:0,3:0,4:0,5". 
This command would correct cartesian X-Axis by 0.0, Y-Axis by 0.1, Z-Axis by 0.2, A-Axis by 0.3, B-Axis by 0.4 and C-Axis by 0.5.

more detailed information will follow here...

####modifyAKorrVariable( String variable, String value )
This command does exactly the same only with one Axis. The variable String has to be of "AKorr1", "AKorr2", "AKorr3", "AKorr4", "AKorr5" or "AKorr6".
The value has to be a number converted into a String.

####modifyRKorrVariable( String variable, String value )
This command does exactly the same only with one cartesian Axis. The variable String has to be of "RKorrX", "RKorrY", "RKorrZ", "RKorrA", "RKorrB" or "RKorrC".
The value has to be a number converted into a String.

#Examples

#Author & Info
Matthias S.<br/>
matthias@seehauser.at<br/>
http://www.seesle.at<br/>
MCI - Mechatronics
http://www.mci.at

#License
<a rel="license" href="http://creativecommons.org/licenses/by-sa/4.0/"><img alt="Creative Commons Lizenzvertrag" style="border-width:0" src="http://i.creativecommons.org/l/by-sa/4.0/88x31.png" /></a><br /><span xmlns:dct="http://purl.org/dc/terms/" href="http://purl.org/dc/dcmitype/Text" property="dct:title" rel="dct:type">KukaMatlabConnector</span> von <a xmlns:cc="http://creativecommons.org/ns#" href="http://www.github.com/seehma/KMC" property="cc:attributionName" rel="cc:attributionURL">Matthias Seehauser</a> ist lizenziert unter einer <a rel="license" href="http://creativecommons.org/licenses/by-sa/4.0/">Creative Commons Namensnennung - Weitergabe unter gleichen Bedingungen 4.0 International Lizenz</a>.<br />Beruht auf dem Werk unter <a xmlns:dct="http://purl.org/dc/terms/" href="http://www.github.com/seehma/KMC" rel="dct:source">http://www.github.com/seehma/KMC</a>.