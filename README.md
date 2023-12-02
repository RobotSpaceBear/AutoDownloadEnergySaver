# ADES - AutoDownloadEnergySaver
A tool to automatically command your PC to hibernate when network download activity dies down for awhile. Great for slow broadband connections...

# What's the usecase?
My internet broadband is very slow and I need to leave my computer downloading for the night. In order to save on energy bills, I want my computer to hibernate as soon as the last download has completed.

# How it works
ADES looks up your network card interfaces and counts the amount of traffic it sees, then makes a moving average over the last 10 seconds. If the average stays under a set network speed (i.e: 100kbps) for a set number of seconds (i.e: 30 seconds), it will hibernate the computer.

# Note
`AutoDownloadEnergySaver.dll.config` contains config variables you can change so tailor the app's behaviour to your network speer and/or use.
 
 ```xml
  <appSettings>
    <!--amount of time between network pool loops -->
    <add key="NETWORK_POOL_RATE_MILLISECONDS" value="1000" />
    <!--amount of seconds over which the moving average download speed is calculated -->
    <add key="TIME_SECONDS_USED_FOR_AVERAGE" value="10" />
    <!--traffic under which computer shutdown is considered -->
    <add key="MIN_KILOBYTES_THRESHOLD" value="800" />
    <!--time spent under traffic threshold before shutdown is triggered -->
    <add key="TIME_SECONDS_BEFORE_SHUTDOWN" value="30" />
    <!--Shutdown mode : 0=suspend, 1=hibernate, 2=power off  -->
    <add key="SHUTDOWN_MODE" value="0" />
    <!--"true" allows for the app to really hibernate the PC -->
    <add key="SIMULATION_MODE" value="false" />
  </appSettings>
  ```
