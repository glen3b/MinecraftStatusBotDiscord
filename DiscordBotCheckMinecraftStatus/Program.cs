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
using MineLib.Network;
using System.Threading.Tasks;
using System.Net.NetworkInformation;
using System.Text;

namespace DiscordBotCheckMinecraftStatus
{
	class Program
	{
		public static void Main (string[] args)
		{
			Console.WriteLine ("Discord Minecraft Checker Bot");
			Console.WriteLine ("Copyright (C) 2016 Glen Husman");
			Console.WriteLine ("Licensed under the GNU General Public License v3");

			new Program (ConfigurationManager.AppSettings ["AdminUserID"], ConfigurationManager.AppSettings ["MinecraftAddress"], ConfigurationManager.AppSettings ["ServerID"]).Execute (ConfigurationManager.AppSettings ["BotToken"]);
		}

		public Program (string adminId, string minecraftAddress, string serverId)
		{
			Client = new DiscordClient ();
			Client.Ready += (object sender, EventArgs e) => {
				Console.WriteLine ("Signed into Discord under bot username {0}", Client.CurrentUser.Name);

				// TODO check: If done right, returns null UNLESS a textchannel by the name of general can be found in the specified serve
				DefaultChannel = ServerID.HasValue ? Client.GetServer (ServerID.Value)?.FindChannels ("general", ChannelType.Text)?.FirstOrDefault () : null;
				if (DefaultChannel == null) {
					// If there's no general channel, go for a Minecraft channel
					DefaultChannel = ServerID.HasValue ? Client.GetServer (ServerID.Value)?.FindChannels ("minecraft", ChannelType.Text, exactMatch: true)?.FirstOrDefault () : null;
				}

				if (DefaultChannel != null) {
					Console.WriteLine ("Using channel #{0} as the main channel.", DefaultChannel.Name);
				} else {
					Console.WriteLine ("No default channel found. This will prevent status updates until a user runs a command in a public channel.");
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
			cfg.PrefixChar = '/';
			cfg.IsSelfBot = false;
			cfg.HelpMode = HelpMode.Private;

			CommandService service = new CommandService (cfg);
			WhitelistService admins = new WhitelistService (adminIDNum);


			Client.AddService (service);
			Client.AddService (admins);

			// May use for abuse prevention
			Client.AddService (new BlacklistService ());

			service.CreateGroup ("minecraft", cgb => {
				cgb.PublicOnly ().UseGlobalBlacklist ().CreateCommand ("status")
					.Alias ("playercount", "ping", "list")
					.Description ("Checks if the Minecraft server is up and returns statistics such as playercount.")
					.Do (CheckServerStatus);

				cgb.CreateCommand ("alert")
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
			});

			service.CreateGroup ("botadmin", cgb => {
				cgb.PrivateOnly ().UseGlobalWhitelist ().CreateCommand ("hello")
					.Alias ("helloworld", "ping")
					.Description ("Pings the bot.")
					.Do (async (arg) => {
					await arg.Channel.SendMessage ("Hello world!");
				});

				cgb.PrivateOnly ().UseGlobalWhitelist ().CreateCommand ("shutdown")
					.Alias ("die")
					.Description ("Kills the bot.")
					.Do (async (arg) => {
					await arg.Channel.SendMessage ("Shutting down :(");
					Console.WriteLine ("Received shutdown command from {0}.", arg.User.Name);
					await Client.Disconnect ();
					Client.Dispose ();
				});

				cgb.PrivateOnly ().UseGlobalWhitelist ().CreateCommand ("disable")
					.Alias ("lock")
					.Description ("Disables the bots ping functionality.")
					.Do (async (arg) => {
					LastPing = DateTime.MaxValue;
					await arg.Channel.SendMessage ("Server pings disabled.");
				});

				cgb.CreateGroup ("defaultchannel", (ccgb) => {
					ccgb.PublicOnly ().UseGlobalWhitelist ().CreateCommand ("set")
						.Description ("Sets the default channel for alerts to your current channel.")
						.Do (async (arg) => {
						if (arg.Server == null) {
							await arg.User.SendMessage ("The default channel must be public.");
							return;
						}
						DefaultChannel = arg.Channel;
						await arg.User.SendMessage (string.Format ("Default channel for alerts set to #{0}.", arg.Channel.Name));
					});

					ccgb.PrivateOnly ().UseGlobalWhitelist ().CreateCommand ("get")
						.Description ("Gets the default channel.")
						.Do (async (arg) => {

						if (DefaultChannel == null) {
							await arg.Channel.SendMessage ("There is currently no default channel set.");
							return;
						}

						await arg.Channel.SendMessage (string.Format ("The default channel is #{0}.", DefaultChannel.Name, DefaultChannel.Server.Name));
					});
				});

				cgb.CreateGroup ("cooldown", ccgb => {
					ccgb.UseGlobalWhitelist ().CreateCommand ("status")
						.Alias ("check")
						.Description ("Returns the current cooldown status.")
						.Do (async (arg) => {
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

					ccgb.UseGlobalWhitelist ().CreateCommand ("reset")
						.Description ("Resets the cooldown.")
						.Do (async (arg) => {
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

					ccgb.UseGlobalWhitelist ().PrivateOnly ().CreateCommand ("set")
						.Parameter ("cooldown", ParameterType.Required) 
						.Description ("Sets the cooldown in between invocations in milliseconds.")
						.Do (async (arg) => {
						int cooldownMs;
						if (!int.TryParse (arg.Args [0], out cooldownMs)) {
							await arg.Channel.SendMessage ("Error parsing cooldown.");
							return;
						}

						Delay = TimeSpan.FromMilliseconds (cooldownMs);
						await arg.Channel.SendMessage (string.Format ("Cooldown set to {0} seconds.", Delay.TotalSeconds));
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

		private void SetDefaultIfNeeded (CommandEventArgs arg)
		{
			// TODO a bit of a hack
			// Set the default channel, if not already set, to the first non-private channel a status command is received from
			if (DefaultChannel == null && arg.Server != null) {
				DefaultChannel = arg.Channel;
				Console.WriteLine ("Default channel set to #{0}.", DefaultChannel.Name);
			}
		}

		public string MinecraftAddress;
		public short MinecraftPort = 25565;
		public TimeSpan Delay = TimeSpan.FromSeconds (30);
		public ulong? ServerID;

		/// <summary>
		/// The default channel, used for timed status checks.
		/// </summary>
		public Channel DefaultChannel = null;

		private List<User> NotifyOnUptime = new List<User> ();
		private bool lastPingSuccess = true;
		private System.Threading.Timer StatusCheckTimer;

		private MineLib.Network.Modern.BaseClients.ServerInfo ErrorServerInfo {
			get {
				return new MineLib.Network.Modern.BaseClients.ServerInfo () {
					Description = null,
					Players = new MineLib.Network.Modern.BaseClients.Players () {
						Max = -1,
						Online = -1
					}
				};
			}
		}

		private void OnStatusTimer (object userState)
		{
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
			CheckServerStatus (DefaultChannel, null);
		}

		protected async Task<TResult> TaskWithTimeout<TResult> (Task<TResult> longRunning, TResult defaultValue, int timeout = 1250)
		{
			
			if (await Task.WhenAny (longRunning, Task.Delay (timeout)) == longRunning) {
				// task completed within timeout
				try {
					return await longRunning;
				} catch {
					// TODO less stupid way of doing this
					return defaultValue;
				}
			} else { 
				// timeout logic
				// TODO cancel properly
				return defaultValue;
			}
		}

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

			LastPing = DateTime.Now;

			long ping = await PingAddress (MinecraftAddress);
			MineLib.Network.Modern.BaseClients.ServerInfo servInfo = ErrorServerInfo;
			if (ping >= 0) {
				servInfo = await TaskWithTimeout (GetServerInfo (), ErrorServerInfo);
			}


			if (ping == -1 || servInfo.Equals (default(MineLib.Network.Modern.BaseClients.ServerInfo)) || servInfo.Players.Online < 0) {
				lastPingSuccess = false;
				if (alertOnFail != null) {
					await channel.SendMessage ("I cannot reach the Minecraft server; it's probably offline. You can do /minecraft alert to be alerted when the server shows up as online.");
				}
			} else {
				lastPingSuccess = true;
				if (NotifyOnUptime.Count > 0) {
					StringBuilder uptimeMsg = new StringBuilder ();

					foreach (var user in NotifyOnUptime) {
						uptimeMsg.Append ("<@").Append (user.Id).Append ("> ");
					}

					uptimeMsg.Append ("the Minecraft server is back online.");

					NotifyOnUptime.Clear ();

					await channel.SendMessage (uptimeMsg.ToString ());
				}

				await channel.SendMessage (string.Format ("The Minecraft server currently has {0} out of {2} player{1} online, and I can reach it with a ping of {3} ms. Join it at {4}{5}.", servInfo.Players.Online, servInfo.Players.Max == 1 ? string.Empty : "s", servInfo.Players.Max, ping, MinecraftAddress, MinecraftPort == 25565 ? string.Empty : ':' + MinecraftPort.ToString ()));
				if (servInfo.Players.Online > 0) {
					foreach (var player in servInfo.Players.Sample) {
						await channel.SendMessage (string.Format ("{0} is online on the Minecraft.", player.Name));
					}
					if (servInfo.Players.Sample.Count != servInfo.Players.Online) {
						// TODO indicate without being stupid or spammy that other players are online
					}
				}
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

		private Task<MineLib.Network.Modern.BaseClients.ServerInfo> GetServerInfo ()
		{
			return Task.Run (
				() => {
					try {
						MineLib.Network.Modern.BaseClients.ServerInfoParser client = new MineLib.Network.Modern.BaseClients.ServerInfoParser ();
						client.ServerHost = MinecraftAddress;
						client.ServerPort = MinecraftPort;
						client.Mode = NetworkMode.Modern;

						// TODO hardcoded protocol version
						var serverInfo = client.GetServerInfo (MinecraftAddress, MinecraftPort, 5);

						client.Dispose ();

						return serverInfo;
					} catch {
						return ErrorServerInfo;
					}
				}
			);
		}

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
			Client.ExecuteAndWait (() => Client.Connect (token));
		}
	}
}
