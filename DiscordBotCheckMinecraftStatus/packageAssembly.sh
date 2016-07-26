#!/bin/bash

echo "Creating standalone assembly..."
echo "If any dependencies are missing, try downloading them from NuGet into the current directory"

mono --runtime=v4.0 ../packages/ILRepack.2.0.10/tools/ILRepack.exe /out:bin/Release/DiscordBotCheckMinecraftStatus.standalone.exe /lib:bin/Release bin/Release/DiscordBotCheckMinecraftStatus.exe bin/Release/*.dll
