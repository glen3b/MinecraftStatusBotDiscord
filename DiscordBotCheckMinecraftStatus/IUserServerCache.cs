using System;
using Discord;

namespace DiscordBotCheckMinecraftStatus
{
	public interface IUserServerCache
	{
		Server GetEffectiveServer (User user, Server currentServer);
	}
}

