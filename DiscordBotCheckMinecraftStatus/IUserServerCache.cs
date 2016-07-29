using System;
using Discord;

namespace DiscordBotCheckMinecraftStatus
{
	public interface IUserServerCache
	{
		Server GetEffectiveServer (ulong userId, Server currentServer);
	}
}

