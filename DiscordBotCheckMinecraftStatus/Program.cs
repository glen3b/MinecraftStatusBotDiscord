using System;
using System.Linq;
using System.Collections.Generic;
using System.Configuration;
using Discord;
using Discord.Commands;
using Discord.Commands.Permissions;
using Discord.Commands.Permissions.Levels;
using Discord.Commands.Permissions.Userlist;
using Discord.Commands.Permissions.Visibility;
using System.Threading.Tasks;
using System.Net.NetworkInformation;
using System.Text;
using System.Net;

namespace DiscordBotCheckMinecraftStatus
{
	class Program
	{
		private static ConsoleCancelEventHandler EndPrgm;

		public static void Main (string[] args)
		{
			Console.WriteLine ("Discord Minecraft Checker Bot");
			Console.WriteLine ("Copyright (C) 2016 Glen Husman");
			Console.WriteLine ("Licensed under the GNU General Public License v3");

			if (ConfigurationManager.AppSettings == null || ConfigurationManager.AppSettings.AllKeys.Length == 0) {
				Console.Error.WriteLine ("Error: Configuration file not found. Exiting.");
				return;
			}

			if (ConfigurationManager.AppSettings.AllKeys.Contains ("LogLevel")) {
				Enum.TryParse<LogSeverity> (ConfigurationManager.AppSettings ["LogLevel"], true, out _logLevel);
			}

			ulong defaultAdmin;

			if (!ulong.TryParse (ConfigurationManager.AppSettings ["AdminUserID"], out defaultAdmin)) {
				Console.WriteLine ("Error parsing admin user ID.");
				return;
			}

			Program main = new Program (defaultAdmin, ConfigurationManager.AppSettings);
			main.Execute (ConfigurationManager.AppSettings ["BotToken"]);

			bool isRunning = true;

			EndPrgm = (object sender, ConsoleCancelEventArgs e) => {
				Console.WriteLine ("Disconnecting bot...");
				isRunning = false;
				main.Terminate ();
			};

			Console.CancelKeyPress += EndPrgm;

			main.Client.Log.Message += LogMessage;

			while (isRunning) {
				var consoleInput = Console.ReadLine ().Split (' ');

				switch (consoleInput [0].ToLower ()) {
				case "help":
					Console.WriteLine ("Console admin interface v0");
					Console.WriteLine ("Available commands:");
					Console.WriteLine ("Help: Returns a console admin manual.");
					Console.WriteLine ("GetServers: Returns the list of servers to which this bot is subscribed.");
					Console.WriteLine ("Stop/Die: Terminates the bot.");
					Console.WriteLine ("ClearAdmin: Clears the list of bot administrators.");
					Console.WriteLine ("Logs <On|Off> [LogLevel]: Enables or disables console logging. Defaults to on.");
					Console.WriteLine ("GrantAdmin <UserID>: Grants bot administrative rights to the specified user.");
					Console.WriteLine ("Say <Server#> <Message>: Sends a message in the given server's default channel.");
					break;
				case "getservers":
				case "listservers":
					Console.WriteLine ("Not implemented.");
					break;
				case "stop":
				case "die":
					EndPrgm (null, null);
					break;
				case "clearadmins":
				case "removeadmins":
				case "clearadmin":
				case "lockweb":
				case "blockpm":
					// Duplicate the list
					var admins = main.Client.GetWhitelistedUserIds ().Select ((a) => a);
					int removed = 0;
					foreach (var userId in admins) {
						main.Client.RemoveFromWhitelist (userId);
						removed++;
					}

					Console.WriteLine ("Administrative whitelist cleared of {0} user{1}.", removed, removed == 1 ? string.Empty : "s");
					break;
				case "addadmin":
				case "addmin":
				case "grantadmin":
					if (consoleInput.Length < 2) {
						Console.WriteLine ("Target user required.");
						break;
					}

					ulong id;

					if (!ulong.TryParse (consoleInput [1], out id)) {
						Console.WriteLine ("Specified value not a user ID.");
						break;
					}

					main.Client.WhitelistUser (id);

					Console.WriteLine ("UserID {0} added to administrative whitelist.", id);

					break;
				case "log":
				case "logs":
					if (consoleInput.Length < 2) {
						Console.WriteLine ("Toggle value required.");
						break;
					}

					bool logsEnabled = false;

					if (consoleInput [1] == "on" || consoleInput [1] == "1" || consoleInput [1] == "true") {
						logsEnabled = true;
					}

					LogSeverity logLevel = _logLevel;

					if (consoleInput.Length >= 3) {
						if (!Enum.TryParse<LogSeverity> (consoleInput [2], true, out logLevel)) {
							// Reset so we dont overwrite
							logLevel = main.Client.Log.Level;
						}
					}

					_logLevel = logLevel;

					_printLogs = logsEnabled;
					if (logsEnabled) {
						Console.WriteLine ("Logging enabled at verbosity level {0}.", logLevel);
					} else {
						Console.WriteLine ("Logging disabled.");
					}
					break;
				case "say":
				case "broadcast":
					if (consoleInput.Length < 2) {
						Console.WriteLine ("No server specified.");
						break;
					}

					if (consoleInput.Length < 3) {
						Console.WriteLine ("No message specified.");
						break;
					}

					string serverPartial = consoleInput [1];
					int? matchIndex = null;
					if (serverPartial.Contains (':')) {
						string[] servPartParts = serverPartial.Split (':');

						// Try to parse the last colon-delimited value as the index
						int outVal;
						if (int.TryParse (servPartParts.Last (), out outVal)) {
							// All other colons are part of main string
							matchIndex = outVal;
							serverPartial = string.Join (":", servPartParts.Take (servPartParts.Length - 1));
						}
					}

					// FIXME the human aspect of this RELIES on the order remaining constant within a given 2 queries
					// Probably de-facto OK, but not tested and not guaranteed
					Server[] servers = null;

					ulong testId;
					if (ulong.TryParse (serverPartial, out testId)) {

						Server byId = null;
						try {
							byId = main.Client.GetServer (testId);
						} catch {
						}

						if (byId != null) {
							servers = new Server[] { byId };
						}
					}

					if (servers == null) {
						servers = main.Client.FindServers (serverPartial).ToArray ();
					}

					Server inUse = null;

					if (servers.Length == 0) {
						Console.WriteLine ("No servers with the specified name or ID found.");
						break;
					} else if (servers.Length > 1) {
						Console.WriteLine ("{0} servers found.", servers.Length);
						if (matchIndex.HasValue) {
							Console.WriteLine ("Using server at index {0}.", matchIndex.Value);
							inUse = servers [matchIndex.Value];
						} else if (servers.Length < 10) {
							// Only list reasonable amounts of servers
							Console.WriteLine ("You can append :<number> to your queries to reference a particular server from this list.");
							for (int i = 0; i < servers.Length; i++) {
								Console.WriteLine ("#{0}: {1}", i, servers [i].Name);
							}
							break;
						} else {
							Console.WriteLine ("Please try a more specific query.");
							break;
						}
					} else {
						// Just right
						inUse = servers [0];
					}

					if (inUse == null) {
						break;
					}

					// FIXME TODO find default channel and send message

					if (main.Servers [inUse] == null || main.Servers [inUse].DefaultChannel == null) {
						Console.WriteLine ("Default channel not defined.");
						break;
					}

					main.Servers [inUse].DefaultChannel.SendMessage (string.Join (" ", consoleInput.Skip (2)));

					Console.WriteLine ("Message broadcasted on server '{0}' to channel #{1}.", inUse.Name, main.Servers [inUse].DefaultChannel.Name);
					break;
				default:
					Console.WriteLine ("Unrecognized command.");
					break;
				}
			}

			System.Threading.Thread.Sleep (1000);
		}

		public static Channel GetDefaultChannel (Server server)
		{
			Channel defaultChan = server.FindChannels ("general", ChannelType.Text)?.FirstOrDefault ();
			if (defaultChan == null) {
				// If there's no general channel, go for a Minecraft channel
				defaultChan = server.FindChannels ("minecraft", ChannelType.Text)?.FirstOrDefault ();
			}

			if (defaultChan != null) {
				return defaultChan;
			}

			// TODO manually specify default channels
			throw new ArgumentException ("The specified server does not have a valid default channel.");
		}

		private static bool _printLogs = true;
		private static LogSeverity _logLevel = LogSeverity.Warning;

		private static void LogMessage (object sender, LogMessageEventArgs args)
		{
			// Higher severity numbers correspond to lower log levels
			if (!_printLogs || args.Severity > _logLevel) {
				return;
			}

			Console.WriteLine ("[{0} / {1}] {2}", args.Source, args.Severity, args.Message);
		}

		public Program (ulong adminId, System.Collections.Specialized.NameValueCollection appCfg)
		{
			Client = new DiscordClient ();
			Client.Ready += (object sender, EventArgs e) => {
				Client.Log.Info ("OnReady", "Signed into Discord under bot username " + Client.CurrentUser.Name);

				// Delay this as much as reasonable - can't do ServerAvailable because it could fire multiple times
				// TODO can Ready fire more than once
				StatusCheckTimer = new System.Threading.Timer (OnStatusTimer, null, TimeSpan.FromMinutes (1), TimeSpan.FromTicks (Delay.Ticks * 5));
			};

			Client.ServerAvailable += (object sender, ServerEventArgs e) => {

				IMinecraftServer minecraftServer = null;

				foreach (var key in appCfg.AllKeys) {
					if (!key.Contains (":")) {
						continue;
					}

					var keyParts = key.Split (':');

					string index = keyParts [0];

					// Ignore the Minecraft keys, we get those manually
					if (keyParts [1] != "ServerID") {
						continue;
					}

					// We've found a server ID key
					ulong tryId;
					if (!ulong.TryParse (appCfg [key], out tryId)) {
						Client.Log.Warning ("ServerAvailable", "Error parsing ServerID key '" + key + "'");
						continue;
					}

					string[] mcInfo = null;

					try {
						mcInfo = appCfg [index + ":MinecraftAddress"].Split (':');
					} catch {
						mcInfo = null;
					}

					if (mcInfo == null) {
						Client.Log.Warning ("ServerAvailable", "No Minecraft info found for key '" + index + "'");
						continue;
					}

					if (mcInfo.Length < 2) {
						minecraftServer = new BaseMinecraftServerInformation (mcInfo [0]);
					} else {
						short port;
						if (!short.TryParse (mcInfo [1], out port)) {
							Client.Log.Warning ("ServerAvailable", "Error parsing port for Minecraft '" + index + ",' using default port 25565");
							port = 25565;
						}

						minecraftServer = new BaseMinecraftServerInformation (mcInfo [0], port);
					}

					// Found our server
					break;
				}

				if (minecraftServer == null) {
					Client.Log.Warning ("ServerAvailable", "No Minecraft server found for server '" + e.Server.Name + "'");
					return;
				}

				Client.Log.Info ("ServerAvailable", string.Format ("Registered server '{0}' (ID:{1}) with Minecraft hostname '{2}:{3}'",
					e.Server.Name, e.Server.Id, minecraftServer.Hostname, minecraftServer.Port));

				Servers.AddServer (e.Server, minecraftServer);

//				Console.WriteLine ("Logged into Discord under bot username {0}.", Client.CurrentUser.Name);
//				Console.WriteLine ("Listening for commands on {0} server{1}.", serverCount, serverCount == 1 ? string.Empty : "s");
			};

//			Client.MessageReceived += (object sender, MessageEventArgs e) => {
//				Console.WriteLine ("Received message in channel {0} from {1}: {2}", e.Channel.Name, e.User.Name, e.Message.Text);
//			};

			// Large delay because we're going to perform lots of requests each time - one per server

			CommandServiceConfigBuilder cfg = new CommandServiceConfigBuilder ();
			cfg.AllowMentionPrefix = true;
			cfg.PrefixChar = null;
			cfg.IsSelfBot = false;
			cfg.HelpMode = HelpMode.Private;
			cfg.CustomPrefixHandler = (m) => {
				if (m.Channel.Server == null) {
					// No prefix required for PMs
					return 0;
				}

				// Other prefix handlers should've handled it
				return -1;
			};

			CommandService service = new CommandService (cfg);
			WhitelistService admins = new WhitelistService (adminId);

			Client.AddService (service);
			Client.AddService (admins);

			// May use for abuse prevention
			Client.AddService (new BlacklistService ());

			ServerStatus = new MCApiStatusChecker (Client.Log);

			// Since we have prefix-only invocations, these are not ambiguous
			service.CreateCommand ("status")
					.PublicOnly ().UseGlobalBlacklist ()
					.Alias ("players", "server", "ping")
					.Description ("Checks if the Minecraft server is up and returns statistics such as playercount and ping.")
				.Do (CheckServerStatus);

			service.CreateCommand ("alert")
					.UseGlobalBlacklist ()
					.Alias ("subscribe", "notify")
					.Description ("Notifies the invoker upon the next status check where the Minecraft server is online.")
					.Do (async (arg) => {

				Server serv = null;

				GetEffectiveServer (arg, ref serv);

				if (serv == null) {
					await arg.User.SendMessage ("I don't know which Minecraft server you want to subscribe to.");
					await arg.User.SendMessage ("Try running this command in a public channel.");
					return;
				}

				IServerInformation servData = Servers [serv];
				if (servData == null) {
					await arg.User.SendMessage (serv.Name + " doesn't have an associated Minecraft server.");
				} else if (!servData.Minecraft.LastPingSucceeded.HasValue) {
					await arg.User.SendMessage ("Please ping the Minecraft server at least once in a public channel before subscribing to its status.");
				} else if (servData.Minecraft.LastPingSucceeded.HasValue && servData.Minecraft.LastPingSucceeded.Value) {
					await arg.User.SendMessage (serv.Name + "'s gameserver was up when last checked; you cannot currently subscribe to downtime.");
				} else if (servData.UptimeSubscribers.Contains (arg.User)) {
						await arg.User.SendMessage ("You are already subscribed to the next uptime notification for __"
							+ servData.Minecraft.Hostname + (servData.Minecraft.Port == 25565 ? string.Empty : servData.Minecraft.Port.ToString()) + "__.");
				} else {
					servData.UptimeSubscribers.Add (arg.User);
					await arg.User.SendMessage (string.Format ("You will be notified when {0}{1} comes back online.",
						servData.Minecraft.Hostname, servData.Minecraft.Port == 25565 ? string.Empty : ':' + servData.Minecraft.Port.ToString ()));
				}
			});

			service.CreateGroup ("cooldown", ccgb => {
				ccgb.CreateCommand ("status")
					.PrivateOnly ()
					.Alias ("check", "get")
					.Description ("Returns the current cooldown status for your most recently queried server.")
					.Do (async (arg) => {
					//LogAdminCommand (arg);

					Server effectiveServer = null;
					GetEffectiveServer (arg, ref effectiveServer);

					if (effectiveServer == null) {
						// arg.Server == null is true
						// So it doesnt matter if we send to channel or user
						await arg.Channel.SendMessage ("I don't know which server you're querying, but the cooldown between pings is " + Delay.TotalSeconds + " seconds.");
						await arg.Channel.SendMessage ("Please run one of my commands in a public channel so I know which server you care about.");
						return;
					}

					IServerInformation info = Servers [effectiveServer];

					if (info == null) {
						await arg.User.SendMessage ("The server you're querying doesn't have an associated Minecraft instance.");
						return;
					}

					TimeSpan cooldownRemaining = Delay - (DateTime.Now - info.LastPing);
					if (cooldownRemaining < TimeSpan.Zero) {
						await arg.Channel.SendMessage ($"{effectiveServer.Name}'s {Delay.TotalSeconds} second cooldown has elapsed; you do not need to wait before pinging its gameserver.");
					} else {
						double cooldownInSeconds = cooldownRemaining.TotalSeconds;
						cooldownInSeconds *= 100;
						cooldownInSeconds = (int)cooldownInSeconds;
						cooldownInSeconds /= 100;

						await arg.Channel.SendMessage (string.Format ("There are {0} seconds remaining on {2}'s cooldown of {1} seconds.", cooldownInSeconds, Delay.TotalSeconds, effectiveServer.Name));
					}
				});

				ccgb.CreateCommand ("reset")
					.UseGlobalWhitelist ()
					.Description ("Resets the cooldown.")
					.Do (async (arg) => {

					LogAdminCommand (arg);

					Server effectiveServer = null;
					GetEffectiveServer (arg, ref effectiveServer);

					if (effectiveServer == null) {
						// arg.Server == null is true
						// So it doesnt matter if we send to channel or user
						await arg.Channel.SendMessage ("I don't know which cooldown you're resetting.");
						await arg.Channel.SendMessage ("Please run one of my commands in a public channel so I know which server you care about.");
						return;
					}

					IServerInformation info = Servers [effectiveServer];

					if (info == null) {
						await arg.User.SendMessage ("The server you're managing does not have an associated Minecraft instance.");
						return;
					}

					if (info.LastPing == DateTime.MaxValue) {
						await arg.Channel.SendMessage ("Server pings enabled for " + effectiveServer.Name + ".");
					} else {
						await arg.Channel.SendMessage ("Cooldown reset for " + effectiveServer.Name + ".");
					}

					info.LastPing = DateTime.MinValue;

				});

				ccgb.CreateCommand ("set")
					.UseGlobalWhitelist ()
					.PrivateOnly ()
					.Parameter ("cooldown", ParameterType.Required) 
					.Description ("Sets the cooldown in between invocations in milliseconds. This setting is global.")
					.Do (async (arg) => {

					LogAdminCommand (arg);

					int cooldownMs;
					if (!int.TryParse (arg.Args [0], out cooldownMs)) {
						await arg.Channel.SendMessage ("Error parsing cooldown.");
						return;
					}

					Delay = TimeSpan.FromMilliseconds (cooldownMs);
					await arg.Channel.SendMessage (string.Format ("Cooldown set to {0} seconds.", Delay.TotalSeconds));
				});
			});

			service.CreateGroup ("admin", cgb => {
				cgb.UseGlobalWhitelist ();

				cgb.CreateCommand ("ping")
					.Alias ("hello")
					.Description ("Pings the bot.")
					.Do (async (arg) => {
					LogAdminCommand (arg);

					await arg.Channel.SendMessage ("Pong!");
				});

				cgb.CreateCommand ("shutdown")
					.PrivateOnly ()
					.Alias ("die")
					.Description ("Kills the bot. This action affects all subscribed servers.")
					.Do ((arg) => {
					// No need to log, there's already a dedicated log

					Client.Log.Warning ("BotAdmin Chat Interface", string.Format ("Received shutdown command from {0}.", arg.User.Name));
//					await Client.Disconnect ();
//					Client.Dispose ();
//
					EndPrgm (this, null);

				});

				cgb.CreateCommand ("disable")
					.PrivateOnly ()
					.Alias ("lock")
					.Description ("Disables the bot's ping functionality for a given server.")
					.Do (async (arg) => {
					LogAdminCommand (arg);

					Server effectiveServer = null;
					GetEffectiveServer (arg, ref effectiveServer);

					if (effectiveServer == null) {
						// arg.Server == null is true
						// So it doesnt matter if we send to channel or user
						await arg.Channel.SendMessage ("I don't know which server's ping you're blocking.");
						await arg.Channel.SendMessage ("Please run one of my commands in a public channel so I know which server you care about.");
						return;
					}
					var servData = Servers [effectiveServer];

					if (servData != null) {
						servData.LastPing = DateTime.MaxValue;
						await arg.Channel.SendMessage ("Server pings disabled on " + effectiveServer.Name + ".");
					} else {
						await arg.Channel.SendMessage (effectiveServer.Name + " does not have an associated Minecraft instance, and thus does not ping.");
					}
				});
			});
		}

		public void GetEffectiveServer (CommandEventArgs args, ref Server serverVar)
		{
			if (args.Server != null) {
				UserCache [args.User.Id] = args.Server;
				serverVar = args.Server;
			} else if (!UserCache.TryGetValue (args.User.Id, out serverVar)) {
				// If TryGetValue succeeds it will assign to the target variable, which is what we wanted to do anyway
				// Otherwise we're here, we'll just make sure it's null
				serverVar = null;
			}
		}

		public IServerResolver Servers = new DictionaryServerResolver ();

		// UID to server instance
		public IDictionary<ulong, Server> UserCache = new Dictionary<ulong, Server> ();

		private void LogAdminCommand (CommandEventArgs cmd)
		{
			Client.Log.Info ("BotAdmin Chat Interface", string.Format ("Received admin command '{0}' from '{1}'", cmd.Message.Text, cmd.User.Name));
		}

		/// <summary>
		/// The delay between Minecraft requests. Constant across all server instances.
		/// </summary>
		public TimeSpan Delay = TimeSpan.FromSeconds (60);
		private System.Threading.Timer StatusCheckTimer;

		private IMinecraftStatusProvider ServerStatus;

		private void OnStatusTimer (object userState)
		{
			Client.Log.Debug ("StatusTimer", "StatusTimer hit, checking conditions for additional server pings.");

			foreach (var server in Servers) {

				if (!server.Minecraft.LastPingSucceeded.HasValue || (server.Minecraft.LastPingSucceeded.HasValue && server.Minecraft.LastPingSucceeded.Value)) {
					// Don't ping for failure knowing we've succeeded
					// Also dont do an auto first ping
					// Although failpings are potentially useful, subscriptions only exist at the moment for downtime transitioning to uptime
					// No alerts exist for the other way around
					continue;
				}

				// Do a quiet status check to potentially alert if there is new success
				try {
					Client.Log.Debug ("StatusTimer", "Performing 'quiet' server ping");
					CheckServerStatus (server.DefaultChannel, null);
				} catch {
					// Ignore
				}
			}
		}

		//		protected async Task<TResult> TaskWithTimeout<TResult> (Task<TResult> longRunning, TResult defaultValue, int timeout = 5000)
		//		{
		//
		//			if (await Task.WhenAny (longRunning, Task.Delay (timeout)) == longRunning) {
		//				// task completed within timeout
		//				try {
		//					return await longRunning;
		//				} catch {
		//					// TODO less stupid way of doing this
		//					return defaultValue;
		//				}
		//			} else {
		//				// timeout logic
		//				// TODO cancel properly
		//				return defaultValue;
		//			}
		//		}

		// TODO alertOnFail param == null means no alert on fail, set to value means alert that user OR if applicable alert the channel the CMD was received in
		// A wee bit of a hack
		private async void CheckServerStatus (Channel channel, User alertOnFail)
		{
			// Channel is NonNull guaranteed
			// That means it has a server we can extrapolate from
			IServerInformation voiceServInfo = Servers [channel.Server];

			if (DateTime.Now - voiceServInfo.LastPing < Delay) {
				if (alertOnFail != null) {
					if (voiceServInfo.LastPing == DateTime.MaxValue) {
						// Large value
						await alertOnFail.SendMessage ("My ping capabilities have been disabled. Please contact an admin to turn me back on.");
					} else {
						await alertOnFail.SendMessage ("I only ping the Minecraft server once every " + ((int)Delay.TotalSeconds) + " seconds at most. Try again later.");
					}
				}
				return;
			}

			Client.Log.Debug ("CheckServerStatus", "Beginning server ping");

			if (alertOnFail != null) {
				await channel.SendIsTyping ();
			}

			voiceServInfo.LastPing = DateTime.Now;

			long ping = -1;
			IMinecraftServerStatus servInfo = null;

			try {
				ping = await PingAddress (voiceServInfo.Minecraft.Hostname);
				if (ping >= 0) {

					// TODO fix

					servInfo = await ServerStatus.GetStatus (voiceServInfo.Minecraft);
					//servInfo = await TaskWithTimeout (GetServerInfo (), ErrorServerInfo);
				}
			} catch {
				Client.Log.Debug ("CheckServerStatus", "Server ping catch-all hit, defaulting to failure");

				// Blanket catch all
				ping = -1;
				servInfo = null;
			}


			if (ping == -1 || servInfo == null || servInfo.OnlinePlayerCount < 0) {
				voiceServInfo.Minecraft.LastPingSucceeded = false;

				Client.Log.Verbose ("CheckServerStatus", "Server ping failed, informing channel");

				if (alertOnFail != null) {
					await channel.SendMessage ("I cannot reach the Minecraft server; it's probably offline. You can run my `alert` command to be alerted when the server shows up as online.");
				}
			} else {
				voiceServInfo.Minecraft.LastPingSucceeded = true;

				Client.Log.Verbose ("CheckServerStatus", "Server ping succeeded, informing channel");

				if (voiceServInfo.UptimeSubscribers.Count > 0) {
					StringBuilder uptimeMsg = new StringBuilder ();

					foreach (var user in voiceServInfo.UptimeSubscribers) {
						uptimeMsg.Append ("<@").Append (user.Id).Append ("> ");
					}

					uptimeMsg.Append ("the Minecraft server is back online.");

					voiceServInfo.UptimeSubscribers.Clear ();

					Client.Log.Verbose ("CheckServerStatus", "Informing subscribed users of newfound server uptime");

					await channel.SendMessage (uptimeMsg.ToString ());

					// Still going to send another message
					await channel.SendIsTyping ();
				}

				StringBuilder onlineStatusMessage = new StringBuilder (
					                                    string.Format
					("The Minecraft server currently has **{0}** out of **{2}** player{1} online, and I can reach it with a ping of **{3} ms**.",
						                                    servInfo.OnlinePlayerCount, servInfo.MaxPlayerCount == 1 ? string.Empty : "s",
						                                    servInfo.MaxPlayerCount, ping,
						                                    voiceServInfo.Minecraft.Hostname, voiceServInfo.Minecraft.Port == 25565 ? string.Empty : ':' + voiceServInfo.Minecraft.Port.ToString ()));
				if (servInfo.PlayerSample.Count () > 0) {
					
					Client.Log.Debug ("CheckServerStatus", "Appending sample userlist of size " + servInfo.PlayerSample.Count () + " to message");

					const string moreHumans = "others";

					string[] players = servInfo.PlayerSample.Select ((p) => p).Union (
						                   servInfo.OnlinePlayerCount > servInfo.PlayerSample.Count () && servInfo.PlayerSample.Count () > 0 ?
						new string[] { moreHumans } : new string[0]).ToArray ();
					

					for (int i = 0; i < players.Length; i++) {
						
						if (i != 0 && players.Length > 2) {
							// If not first, append comma to previous entry
							// Also no comma if only two entries
							onlineStatusMessage.Append (',');
						}

						if (players.Length > 1 && i == players.Length - 1) {
							// If multiple elements exist, use an and to join them
							onlineStatusMessage.Append (" and ");
						} else {
							onlineStatusMessage.Append (' ');
						}

						//Markdown italics

						bool dontUseMarkdown = players [i] == moreHumans && i == players.Length - 1;

						if (!dontUseMarkdown) {
							onlineStatusMessage.Append ('*');
						}

						onlineStatusMessage.Append (players [i]);

						if (!dontUseMarkdown) {
							onlineStatusMessage.Append ('*');
						}
					}

					onlineStatusMessage.Append (' ');

					if (players.Length == 1) {
						onlineStatusMessage.Append ("is");
					} else {
						onlineStatusMessage.Append ("are");
					}

					onlineStatusMessage.Append (" online. You can join them at ");

				} else {
					onlineStatusMessage.Append (" Join it now at ");
					
				}

				// Markdown underline
				onlineStatusMessage.Append ("__");

				onlineStatusMessage.Append (voiceServInfo.Minecraft.Hostname);
				if (voiceServInfo.Minecraft.Port != 25565) {
					// Non-default
					onlineStatusMessage.Append (':').Append (voiceServInfo.Minecraft.Port);
				}

				// End markdown
				onlineStatusMessage.Append ("__");

				// FIXME Punctuation - yes or no?
				onlineStatusMessage.Append ('.');

				await channel.SendMessage (onlineStatusMessage.ToString ());

				// await channel.SendMessage (string.Format ("The Minecraft server currently has {0} out of {2} player{1} online, and I can reach it with a ping of {3} ms.", servInfo.Players.OnlinePlayers, servInfo.Players.MaxPlayers == 1 ? string.Empty : "s", servInfo.Players.MaxPlayers, ping, MinecraftAddress, MinecraftPort == 25565 ? string.Empty : ':' + MinecraftPort.ToString ()));
//				if (servInfo.Players.OnlinePlayers > 0) {
//					foreach (var player in servInfo.Players.Players) {
//						await channel.SendMessage (string.Format ("{0} is online on the Minecraft.", player.Name));
//					}
//					if (servInfo.Players.Players.Length != servInfo.Players.OnlinePlayers) {
//						// TODO indicate without being stupid or spammy that other players are online
//					}
//				}
			}
		}

		private async void CheckServerStatus (CommandEventArgs args)
		{
			Server dummy = null;

			// Set cache if needed, but public command means we don't need the output
			GetEffectiveServer (args, ref dummy);

			if (Servers [dummy] == null) {
				await args.User.SendMessage ("The server you're querying doesn't have an associated Minecraft instance.");
				return;
			}

			CheckServerStatus (args.Channel, args.User);
		}

		private Task<long> PingAddress (string address)
		{
			return Task.Run (
				() => {
					try {
						Ping pingCmd = new Ping ();
						var result = pingCmd.Send (address);
						if (result.Status != IPStatus.Success) {
							return -1;
						}
						return result.RoundtripTime;
					} catch {
						return -1;
					}
				}
			);
		}

		//		private IPEndPoint ParseMinecraftEndPoint ()
		//		{
		//			IPAddress address;
		//
		//			if (!IPAddress.TryParse (MinecraftAddress, out address)) {
		//				address = ResolveDNS (MinecraftAddress);
		//			}
		//
		//			return new IPEndPoint (address, MinecraftPort);
		//		}
		//
		//		private static IPAddress ResolveDNS (string arg)
		//		{
		//			return Dns.GetHostEntry (arg).AddressList.FirstOrDefault (item => item.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);
		//		}

		//		private string ProcessAdminMessage (Message message)
		//		{
		//			if (message.Text.Contains ("ello")) {
		//				return "Hello world!";
		//			}
		//
		//			return null;
		//		}
		//
		//		private async void OnMessageReceived (object sender, MessageEventArgs args)
		//		{
		//			if (args.Message.IsAuthor) {
		//				// Ignore our own messages
		//				return;
		//			}
		//
		//			if (args.Channel.IsPrivate) {
		//				// Received a private message
		//				if (args.User.Id == AdminID) {
		//					// Received admin command
		//					string response = ProcessAdminMessage (args.Message);
		//					if (response != null) {
		//						await args.Channel.SendMessage (response);
		//					}
		//				} else {
		//					// Received unsolicitated private message
		//					// Potentially in the future allow commands here, but for now, ignore
		//					return;
		//				}
		//			}
		//
		//			// Received message in a public channel
		//			// Let the commands API handle it
		//		}

		public DiscordClient Client;

		public void Execute (string token)
		{
			Client.Connect (token);
		}

		public void Terminate ()
		{

			Client.Log.Info ("Terminate", "Client connection terminating");

			// TODO is this needed?
			GC.KeepAlive (StatusCheckTimer);

			Client.Disconnect ();
			Client.Dispose ();
		}
	}
}
