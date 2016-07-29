using System;
using System.Collections.Generic;

namespace DiscordBotCheckMinecraftStatus
{
	public interface IMinecraftServerStatus
	{
		bool IsOnline{ get; }

		int OnlinePlayerCount{ get; }

		int MaxPlayerCount{ get; }

		IEnumerable<string> PlayerSample { get; }
	}
}

