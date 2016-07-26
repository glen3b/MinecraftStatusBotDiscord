# Minecraft Status Bot for Discord
A Discord bot that pings the status of a Minecraft server.

Uses [MineLib.Network](https://github.com/MineLib/MineLib.Network) for Minecraft pinging; a DLL of that name is expected in the project directory. Also extensively utilizes [Discord.Net](https://github.com/RogueException/Discord.Net/), which is referenced in NuGet.

## Workarounds and issues
Currently `ILRepack.MSBuild.Task.dll` must manually be copied to the project directory.
