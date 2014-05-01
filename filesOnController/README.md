#Info

The files in this folder have to be on specific places on the robot controller.
To establish a connection between controller and the Wrapper programm KMC you will have to start the KRL-programm KukaMatlabConnection.src
This file requires the KukaRobotInfo.xml file in the directory C:\KRC\ROBOTER\INIT\..., to read the configuration for the communication task.

There are two example configurations:

* KukaRobotInfo.xml: In this configuration nearly the whole possibilities of RSI external network connection are shown. Analog and Digital Inputs/Outpus are connection. Many sensor signals from the robot for example encoder values, currents ...
* KukaRobotInfo_min.xml: In this configuration only the encoder values and currents are shown

On the client side you have to decode the xml-string which you get with the object.getCommandString() method. For this an example is placed in the KMCMatlab repository on this account.