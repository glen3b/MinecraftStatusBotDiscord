using System;

namespace DiscordBotCheckMinecraftStatus
{
	public interface IServerResolver
	{
		/// <summary>
		/// Gets information about the Minecraft server corresponding to the given voice server.
		/// If information is found it is expected to be returned quickly.
		/// If no specific information exists for the given voice server, <code>null</code> should be returned.
		/// The returned value is expected to be a reference to the original data, not a copy.
		/// </summary>
		/// <param name="voice">The voice server for which the Minecraft server information is requested.</param>
		IMinecraftServer this [Discord.Server voice]{ get; }
	}

	public interface IMinecraftServer
	{
		string Hostname { get; }

		short Port { get; }

		bool? LastPingSucceeded { get; set; }
	}
}

