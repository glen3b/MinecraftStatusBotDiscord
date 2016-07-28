using System;
using System.Collections.Generic;

namespace DiscordBotCheckMinecraftStatus
{
	public interface IMinecraftServerStatus
	{
		int OnlinePlayerCount{ get; }

		int MaxPlayerCount{ get; }

		IEnumerable<string> PlayerSample {get;}
	}
}

