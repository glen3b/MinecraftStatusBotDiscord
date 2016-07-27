using System;

namespace DiscordBotCheckMinecraftStatus
{
	public interface IMinecraftStatusProvider
	{
		/// <summary>
		/// Gets the status of a Minecraft server, returning null if an error occurred.
		/// </summary>
		/// <returns>The status, or null if errored.</returns>
		/// <param name="hostname">Hostname.</param>
		/// <param name="port">Port.</param>
		IServerStatus GetStatus(string hostname, short port = 25565);
	}
}

