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

			Program main = new Program (ConfigurationManager.AppSettings ["AdminUserID"], ConfigurationManager.AppSettings ["MinecraftAddress"], ConfigurationManager.AppSettings ["ServerID"]);
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

				switch (consoleInput [0].ToLower()) {
				case "help":
					Console.WriteLine ("Console admin interface v0");
					Console.WriteLine ("Available commands:");
					Console.WriteLine ("Help: Returns a console admin manual.");
					Console.WriteLine ("Stop/Die: Terminates the bot.");
					Console.WriteLine ("ClearAdmin: Clears the list of bot administrators.");
					Console.WriteLine ("Logs <On|Off> [LogLevel]: Enables or disables console logging. Defaults to on.");
					Console.WriteLine ("GrantAdmin <UserID>: Grants bot administrative rights to the specified user.");
					Console.WriteLine ("Say <Message>: Sends a message in the bot's default channel.");
					break;
				case "stop":
				case "die":
					EndPrgm(null, null);
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
						Console.WriteLine ("No message specified.");
						break;
					}

					if (main.DefaultChannel == null) {
						Console.WriteLine ("Default channel not defined.");
						break;
					}

					main.DefaultChannel.SendMessage (string.Join (" ", consoleInput.Skip (1)));

					Console.WriteLine ("Message broadcasted.");
					break;
				default:
					Console.WriteLine ("Unrecognized command.");
					break;
				}
			}

			System.Threading.Thread.Sleep (1000);
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

		public Program (string adminId, string minecraftAddress, string serverId)
		{
			Client = new DiscordClient ();
			Client.Ready += (object sender, EventArgs e) => {
				Client.Log.Info ("OnReady", "Signed into Discord under bot username " + Client.CurrentUser.Name);
			};

			Client.ServerAvailable += (object sender, ServerEventArgs e) => {
				// TODO check: If done right, returns null UNLESS a textchannel by the name of general can be found in the specified serve
				DefaultChannel = ServerID.HasValue ? Client.GetServer (ServerID.Value)?.FindChannels ("general", ChannelType.Text)?.FirstOrDefault () : null;
				if (DefaultChannel == null) {
					// If there's no general channel, go for a Minecraft channel
					DefaultChannel = ServerID.HasValue ? Client.GetServer (ServerID.Value)?.FindChannels ("minecraft", ChannelType.Text, exactMatch: true)?.FirstOrDefault () : null;
				}

				if (DefaultChannel != null) {
					Client.Log.Info ("ServerAvailable", string.Format ("Using channel #{0} as the main/default channel.", DefaultChannel.Name));
				} else {
					Client.Log.Warning ("ServerAvailable", "No default channel found. This will prevent status updates until a user runs a command in a public channel. The first command will set the default.");
				}
			};

//			Client.MessageReceived += (object sender, MessageEventArgs e) => {
//				Console.WriteLine ("Received message in channel {0} from {1}: {2}", e.Channel.Name, e.User.Name, e.Message.Text);
//			};

			StatusCheckTimer = new System.Threading.Timer (OnStatusTimer, null, TimeSpan.FromMinutes (1), TimeSpan.FromTicks (Delay.Ticks * 2));

			ulong adminIDNum = UInt64.Parse (adminId);

			ulong servIDtmp = 0;

			ServerID = String.IsNullOrWhiteSpace (serverId) ? null : (ulong.TryParse (serverId, out servIDtmp) ? (ulong?)servIDtmp : null);

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
			WhitelistService admins = new WhitelistService (adminIDNum);

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
				// TODO a bit of a hack
				// Set the default channel, if not already set, to the first non-private channel a status command is received from
				SetDefaultIfNeeded (arg);

				if (lastPingSuccess) {
					await arg.User.SendMessage ("The server was up when last checked; you cannot currently subscribe to downtime.");
				} else if (NotifyOnUptime.Contains (arg.User)) {
					await arg.User.SendMessage ("You are already subscribed to the next uptime notification.");
				} else {
					NotifyOnUptime.Add (arg.User);
					await arg.User.SendMessage ("You will be notified when the server comes back online.");
				}
			});

			service.CreateGroup ("cooldown", ccgb => {
				ccgb.CreateCommand ("status")
					.Alias ("check", "get")
					.Description ("Returns the current cooldown status.")
					.Do (async (arg) => {
					LogAdminCommand (arg);

					if (ServerID.HasValue && arg.Server != null && arg.Server.Id != ServerID) {
						// Not our server
						return;
					}

					TimeSpan cooldownRemaining = Delay - (DateTime.Now - LastPing);
					if (cooldownRemaining < TimeSpan.Zero) {
						await arg.Channel.SendMessage ($"The cooldown of {Delay.TotalSeconds} seconds is elapsed, you do not need to wait before pinging the server.");
					} else {
						double cooldownInSeconds = cooldownRemaining.TotalSeconds;
						cooldownInSeconds *= 100;
						cooldownInSeconds = (int)cooldownInSeconds;
						cooldownInSeconds /= 100;

						await arg.Channel.SendMessage (string.Format ("There are {0} seconds remaining on the total cooldown of {1} seconds.", cooldownInSeconds, Delay.TotalSeconds));
					}
				});

				ccgb.CreateCommand ("reset")
					.Description ("Resets the cooldown.")
					.Do (async (arg) => {

					LogAdminCommand (arg);

					if (ServerID.HasValue && arg.Server != null && arg.Server.Id != ServerID) {
						// Not our server
						return;
					}

					if (LastPing == DateTime.MaxValue) {
						LastPing = DateTime.MinValue;
						await arg.Channel.SendMessage ("Server pings enabled.");
					} else {
						LastPing = DateTime.MinValue;
						await arg.Channel.SendMessage ("Cooldown reset.");
					}

				});

				ccgb.CreateCommand ("set")
					.Parameter ("cooldown", ParameterType.Required) 
					.Description ("Sets the cooldown in between invocations in milliseconds.")
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
					.Description ("Kills the bot.")
					.Do (async (arg) => {
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
					.Description ("Disables the bots ping functionality.")
					.Do (async (arg) => {
					LogAdminCommand (arg);

					LastPing = DateTime.MaxValue;
					await arg.Channel.SendMessage ("Server pings disabled.");
				});

				cgb.CreateGroup ("defaultchannel", (ccgb) => {
					ccgb.CreateCommand ("set")
						.PublicOnly ()
						.Description ("Sets the default channel for alerts to your current channel.")
						.Do (async (arg) => {
						LogAdminCommand (arg);

						if (arg.Server == null) {
							await arg.User.SendMessage ("The default channel must be public.");
							return;
						}
						DefaultChannel = arg.Channel;
						await arg.User.SendMessage (string.Format ("Default channel for alerts set to #{0}.", arg.Channel.Name));
					});

					ccgb.CreateCommand ("get")
						.PrivateOnly ()
						.Description ("Gets the default channel.")
						.Do (async (arg) => {
						LogAdminCommand (arg);

						if (DefaultChannel == null) {
							await arg.Channel.SendMessage ("There is currently no default channel set.");
							return;
						}

						await arg.Channel.SendMessage (string.Format ("The default channel is #{0}.", DefaultChannel.Name, DefaultChannel.Server.Name));
					});
				});

				// TODO no whitelisting - ANYONE can run
				// TODO does not currently check for nonnull server var
				// TODO implement
//				cgb.CreateCommand ("block")
//					.Parameter ("bantarget", ParameterType.Required)
//					.Alias ("ban")
//					.Description ("Bans a user from using the bot.")
//					.Do (async (arg) => {
//					ulong id;
//
//					var usersMatching = arg.Channel.Server.FindUsers (arg.Args [0]);
//
//					if (ulong.TryParse (arg.Args [0], out id)) {
//						User byId = arg.Server.GetUser (id);
//						usersMatching = byId == null ? new User[]{ } : new User[] { byId };
//					}
//
//					int usrCount = usersMatching.Count ();
//					if (usrCount == 0) {
//						await arg.User.SendMessage ("No user by that name found.");
//						await arg.Channel.SendMessage ("Error banning user.");
//						return;
//					} else if (usrCount > 1) {
//						await arg.Channel.SendMessage ("Error banning user.");
//						foreach (var usr in usersMatching) {
//							await arg.User.SendMessage (string.Format ("Matching user in ban request: {0} with ID {1}", usr.Name, usr.Id));
//						}
//						return;
//					} else {
//						Client.BlacklistUser (usersMatching.First ().Id);
//						await arg.Channel.SendMessage ("User banned from bot usage.");
//						return;
//					}
//				});
			});

			string[] mcAddrComponents = minecraftAddress.Split (':');
			MinecraftAddress = mcAddrComponents [0];
			if (mcAddrComponents.Length > 1) {
				MinecraftPort = short.Parse (mcAddrComponents [1]);
			}
		}

		private void LogAdminCommand (CommandEventArgs cmd)
		{
			Client.Log.Info ("BotAdmin Chat Interface", string.Format ("Received admin command '{0}' from '{1}'", cmd.Message.Text, cmd.User.Name));
		}

		private void SetDefaultIfNeeded (CommandEventArgs arg)
		{
			// TODO a bit of a hack
			// Set the default channel, if not already set, to the first non-private channel a status command is received from
			if (DefaultChannel == null && arg.Server != null) {
				DefaultChannel = arg.Channel;
				Client.Log.Info ("Chat", string.Format ("Default channel set to #{0}.", DefaultChannel.Name));
			}
		}

		public string MinecraftAddress;
		public short MinecraftPort = 25565;
		public TimeSpan Delay = TimeSpan.FromSeconds (60);
		public ulong? ServerID;

		/// <summary>
		/// The default channel, used for timed status checks.
		/// </summary>
		public Channel DefaultChannel = null;

		private List<User> NotifyOnUptime = new List<User> ();
		private bool lastPingSuccess = true;
		private System.Threading.Timer StatusCheckTimer;

		private IMinecraftStatusProvider ServerStatus;

		private void OnStatusTimer (object userState)
		{
			Client.Log.Debug ("StatusTimer", "StatusTimer hit, checking conditions for additional server ping.");

			if (lastPingSuccess) {
				// Don't ping for failure knowing we've succeeded
				// Although this is potentially useful, subscriptions only exist at the moment for downtime transitioning to uptime
				// No alerts exist for the other way around
				return;
			}

			if (DefaultChannel == null) {
				// Not much we can do
				return;
			}

			// Do a quiet status check to potentially alert if there is new success
			try {
				Client.Log.Debug ("StatusTimer", "Performing 'quiet' server ping");
				CheckServerStatus (DefaultChannel, null);
			} catch {
				// Ignore
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
			if (DateTime.Now - LastPing < Delay) {
				if (alertOnFail != null) {
					if (LastPing == DateTime.MaxValue) {
						// Large value
						await alertOnFail.SendMessage ("My ping capabilities have been disabled. Please contact an admin to turn me back on");
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

			LastPing = DateTime.Now;

			long ping = -1;
			IServerStatus servInfo = null;

			try {
				ping = await PingAddress (MinecraftAddress);
				if (ping >= 0) {

					// TODO fix

					servInfo = await ServerStatus.GetStatus (MinecraftAddress, MinecraftPort);
					//servInfo = await TaskWithTimeout (GetServerInfo (), ErrorServerInfo);
				}
			} catch {
				Client.Log.Debug ("CheckServerStatus", "Server ping catch-all hit, defaulting to failure");

				// Blanket catch all
				ping = -1;
				servInfo = null;
			}


			if (ping == -1 || servInfo == null || servInfo.OnlinePlayerCount < 0) {
				lastPingSuccess = false;

				Client.Log.Verbose ("CheckServerStatus", "Server ping failed, informing channel");

				if (alertOnFail != null) {
					await channel.SendMessage ("I cannot reach the Minecraft server; it's probably offline. You can run my `alert` command to be alerted when the server shows up as online.");
				}
			} else {
				lastPingSuccess = true;

				Client.Log.Verbose ("CheckServerStatus", "Server ping succeeded, informing channel");

				if (NotifyOnUptime.Count > 0) {
					StringBuilder uptimeMsg = new StringBuilder ();

					foreach (var user in NotifyOnUptime) {
						uptimeMsg.Append ("<@").Append (user.Id).Append ("> ");
					}

					uptimeMsg.Append ("the Minecraft server is back online.");

					NotifyOnUptime.Clear ();

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
						                                    MinecraftAddress, MinecraftPort == 25565 ? string.Empty : ':' + MinecraftPort.ToString ()));
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

				onlineStatusMessage.Append (MinecraftAddress);
				if (MinecraftPort != 25565) {
					// Non-default
					onlineStatusMessage.Append (':').Append (MinecraftPort);
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
			if (ServerID.HasValue && args.Server.Id != ServerID) {
				// Not our server
				return;
			}

			// TODO a bit of a hack
			// Set the default channel, if not already set, to the first non-private channel a status command is received from
			SetDefaultIfNeeded (args);

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


		protected DateTime LastPing = DateTime.MinValue;

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

			Client.Disconnect ();
			Client.Dispose ();
		}
	}
}
