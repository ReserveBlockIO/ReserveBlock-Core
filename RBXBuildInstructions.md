# RBX Build Instructions
The following instructions will cover building and running the RBX on Windows, Linux, and Mac.
The most important step is to install .net core 6. 

For Linux you can find install instructions here https://docs.microsoft.com/en-us/dotnet/core/install/linux. This document will cover how to install for Ubuntu.

In the last step for *dotnet ReserveBlockCore(.dll or .exe or blank)* if that does not work you can also try *dotnet run ReserveBlockCore(.dll or .exe or blank)*

## Linux
Linux Install For Ubuntu 22.04:

1. wget https://packages.microsoft.com/config/ubuntu/22.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
sudo dpkg -i packages-microsoft-prod.deb
rm packages-microsoft-prod.deb
2. sudo apt-get update && \
  sudo apt-get install -y dotnet-sdk-6.0
3. sudo apt-get update && \
  sudo apt-get install -y aspnetcore-runtime-6.0
4. run 'dotnet --version'. You should see a version number of 6.0.xxx
5. 'mkdir rbx' - Makes a direction for RBX 
6. cd rbx
7.  a) Clone the repo with git. 'git clone https://github.com/ReserveBlockIO/ReserveBlock-Core.git'


    b) Download the already packaged release https://github.com/ReserveBlockIO/ReserveBlock-Core/releases/. If   	you do this please navigate to folder you downloaded binaries from and skip to step 12.
8. 'cd ReserveBlock-Core'
9. 'dotnet build' - There should be 0 (zero) errors.
10. 'dotnet publish -c Release -r linux-x64 --output ./rbxpublished' **please note the 'linux-x64' can be changed if you are on a different architecture
11. 'cd rbxpublished'
12. 'dotnet ReserveBlockCore.dll'

You are done! You should now see a wallet running! Some common errors are usually related to file permissions. Please ensure you have given proper permission to the newly created RBX folder

## Windows
Install the latest dotnet sdk from here: https://dotnet.microsoft.com/en-us/download/dotnet/6.0
Once that is installed follow steps below.

1. Create a directory for RBX and then open command prompt in admin and navigate to that directory.
2. Please ensure you have a working version of git installed.
3. a) Clone the repo with git. 'git clone https://github.com/ReserveBlockIO/ReserveBlock-Core.git'
   b) Download the already packaged release https://github.com/ReserveBlockIO/ReserveBlock-Core/releases/. If   	you do this please navigate to folder you downloaded binaries from and skip to step 8.
4. cd ReserveBlock-Core
5. dotnet build - There should be 0 (zero) errors.
6. dotnet publish -c Release -r win-x64 --output ./rbxpublished **please note the 'win-x64' can be changed if you are on a different architecture
7. cd rbxpublished
8. dotnet ReserveBlockCore.exe

## Mac OSX
Install the latest dotnet sdk from here: https://dotnet.microsoft.com/en-us/download/dotnet/6.0
Once that is installed follow steps below.

1. Create a directory for RBX and then open command prompt in admin and navigate to that directory.
2. Please ensure you have a working version of git installed.
3. a) Clone the repo with git. 'git clone https://github.com/ReserveBlockIO/ReserveBlock-Core.git'
   b) Download the already packaged release https://github.com/ReserveBlockIO/ReserveBlock-Core/releases/. If   	you do this please navigate to folder you downloaded binaries from and skip to step 8.
4. cd ReserveBlock-Core
5. dotnet build - There should be 0 (zero) errors.
6. dotnet publish -c Release -r osx-x64 --output ./rbxpublished **please note the 'osx-x64' can be changed if you are on a different architecture like arm.
7. cd rbxpublished
8. dotnet ReserveBlockCore


## Build RIDs

## Windows ##

### Windows, not version-specific ### 
* win-x64
* win-x86
* win-arm64
### Windows 7 / Windows Server 2008 R2 ###
* win7-x64
* win7-x86
### Windows 8.1 / Windows Server 2012 R2 ###
* win81-x64
* win81-x86
### Windows 11 / Windows Server 2022 / Windows 10 / Windows Server 2016 ###
* win10-x64
* win10-x86
* win10-arm64

## Linux ##

### Linux, not distribution-specific ### 
* linux-x64 (Most desktop distributions like CentOS, Debian, Fedora, Ubuntu, and derivatives)
* linux-musl-x64 (Lightweight distributions using musl like Alpine Linux)
* linux-arm (Linux distributions running on Arm like Raspbian on Raspberry Pi Model 2+)
* linux-arm64 (Linux distributions running on 64-bit Arm like Ubuntu Server 64-bit on Raspberry Pi Model 3+)

### Red Hat Enterprise Linux ### 
* rhel-x64 (Superseded by linux-x64 for RHEL above version 6)
* rhel.6-x64

### Tizen ###
* tizen
* tizen.4.0.0
* tizen.5.0.0
* tizen.5.5.0
* tizen.6.0.0
* tizen.6.5.0
* tizen.7.0.0

## Mac ##

### macOS, not version-specific ###
* osx-x64 (Minimum OS version is macOS 10.12 Sierra)
### macOS 10.10 Yosemite ### 
* osx.10.10-x64
### macOS 10.11 El Capitan ### 
* osx.10.11-x64
### macOS 10.12 Sierra ### 
* osx.10.12-x64
### macOS 10.13 High Sierra ### 
* osx.10.13-x64
### macOS 10.14 Mojave ### 
* osx.10.14-x64
### macOS 10.15 Catalina ### 
* osx.10.15-x64
### macOS 11.0 Big Sur ### 
* osx.11.0-x64
* osx.11.0-arm64
### macOS 12 Monterey ### 
* osx.12-x64
* osx.12-arm64
### macOS 13 Ventura ### 
* osx.13-x64
* osx.13-arm64