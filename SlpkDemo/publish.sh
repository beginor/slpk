#!/bin/bash -e
rm -rf ./bin
dotnet publish -r osx-x64 -c Release -p:PublishSingleFile=true -p:PublishTrimmed=true -p:DebugType=None -o ./bin/Publish/osx-x64
cp appsettings.json ./bin/Publish/osx-x64
dotnet publish -r win-x64 -c Release -p:PublishSingleFile=true -p:PublishTrimmed=true -p:DebugType=None -o ./bin/Publish/win-x64
cp appsettings.json ./bin/Publish/win-x64
dotnet publish -r linux-x64 -c Release -p:PublishSingleFile=true -p:PublishTrimmed=true -p:DebugType=None -o ./bin/Publish/linux-x64
cp appsettings.json ./bin/Publish/linux-x64
