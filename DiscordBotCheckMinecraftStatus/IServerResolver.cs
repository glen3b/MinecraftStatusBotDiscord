using System;
using System.Collections.Generic;

namespace DiscordBotCheckMinecraftStatus
{
	public interface IServerResolver : IEnumerable<IServerInformation>
	{
		/// <summary>
		/// Gets information about the given guild that is specific to it.
		/// If information is found it is expected to be returned quickly.
		/// If no specific information exists for the given voice server, <code>null</code> should be returned.
		/// </summary>
		/// <param name="voice">The voice server for which the server information is requested.</param>
		IServerInformation this [Discord.Server voice]{ get; }

		/// <summary>
		/// Adds a voice to minecraft server pairing to the configuration.
		/// </summary>
		/// <param name="voice">The voice server for which information is being added.</param>
		/// <param name="minecraft">The Minecraft server to attach to the given voice server.</param>
		/// <exception cref="System.InvalidOperationException">Thrown if the specified server already has an entry in this configuration.</exception>
		/// <exception cref="System.NotSupportedException">Thrown if this backend does not support adding servers.</exception>
		void AddServer(Discord.Server voice, IMinecraftServer minecraft);

		/// <summary>
		/// Gets the number of configured servers in this instance of IServerResolver.
		/// </summary>
		/// <value>The number of configured servers in this server resolver instance.</value>
		int Count{ get; }
	}

	public interface IServerInformation
	{
		/// <summary>
		/// Gets information about this voice server's corresponding Minecraft server.
		/// Returns <code>null</code> if no Minecraft server is configured for this voice server.
		/// </summary>
		/// <value>The minecraft server corresponding to this voice server.</value>
		IMinecraftServer Minecraft { get; }

		/// <summary>
		/// Gets or sets the last ping time.
		/// Expected to default to <see cref="System.DateTime.MinValue"/>
		/// </summary>
		/// <value>The last time the Minecraft server was pinged.</value>
		DateTime LastPing { get; set; }

		/// <summary>
		/// Gets a set of users subscribed to the uptime event.
		/// Expected to be cleared on each uptime notification.
		/// </summary>
		/// <value>The uptime subscribers.</value>
		ISet<Discord.User> UptimeSubscribers{get;}

		/// <summary>
		/// Gets the default channel for this server.
		/// Expected to be, for a configured server, non-null and text. 
		/// </summary>
		/// <value>The default channel.</value>
		Discord.Channel DefaultChannel{get;}


	}

	public interface IMinecraftServer
	{
		string Hostname { get; }

		short Port { get; }

		bool? LastPingSucceeded { get; set; }
	}

	public class BaseMinecraftServerInformation : IMinecraftServer {
		public string Hostname{ get; protected set;}

		public short Port{ get; protected set;}

		public bool? LastPingSucceeded{ get; set;}

		public BaseMinecraftServerInformation(string hostname) : this(hostname, 25565){}

		public BaseMinecraftServerInformation(string hostname, short port){
			if (hostname == null) {
				throw new ArgumentNullException ("hostname");
			}

			Hostname = hostname;
			Port = port;
			LastPingSucceeded = null;
		}

	}
}

