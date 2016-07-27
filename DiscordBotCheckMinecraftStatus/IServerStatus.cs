using System;
using System.Collections.Generic;

namespace DiscordBotCheckMinecraftStatus
{
	public interface IServerStatus
	{
		int OnlinePlayerCount{ get; }

		int MaxPlayerCount{ get; }

		IEnumerable<string> PlayerSample {get;}
	}
}

