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
8. cd ReserveBlock-Core
9. dotnet build - There should be 0 (zero) errors.
10. dotnet publish -c Release -r linux-x64 --output ./rbxpublished **please note the 'linux-x64' can be changed if you are on a different architecture
11. cd rbxpublished
12. dotnet ReserveBlockCore.dll

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
