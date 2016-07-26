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

namespace DiscordBotCheckMinecraftStatus
{
	class Program
	{
		public static void Main (string[] args)
		{
			Console.WriteLine ("Discord Minecraft Checker Bot");
			Console.WriteLine ("Copyright (C) 2016 Glen Husman");
			Console.WriteLine ("Licensed under the GNU General Public License v3");

			// https://discordapp.com/oauth2/authorize?client_id=207342659868557312&scope=bot&permissions=68608
			new Program (ConfigurationManager.AppSettings ["AdminUserID"], ConfigurationManager.AppSettings ["MinecraftAddress"]).Execute (ConfigurationManager.AppSettings ["BotToken"]);
		}

		public Program (string adminId, string minecraftAddress)
		{
			Client = new DiscordClient ();
			Client.Ready += (object sender, EventArgs e) => {
				Console.WriteLine ("Signed into Discord under bot username {0}", Client.CurrentUser.Name);
			};
//			Client.MessageReceived += (object sender, MessageEventArgs e) => {
//				Console.WriteLine ("Received message in channel {0} from {1}: {2}", e.Channel.Name, e.User.Name, e.Message.Text);
//			};
			ulong adminIDNum = UInt64.Parse (adminId);

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
			});

			service.CreateGroup ("botadmin", cgb => {
				cgb.PrivateOnly ().UseGlobalWhitelist ().CreateCommand ("hello")
					.Alias ("helloworld", "ping")
					.Description ("Pings the bot.")
					.Do (async (arg) => {
					await arg.Channel.SendMessage ("Hello world!");
				});

				cgb.PrivateOnly().UseGlobalWhitelist().CreateCommand("disable")
					.Alias("lock")
					.Description("Disables the bots ping functionality.")
					.Do(async (arg) => {
						LastPing = DateTime.MaxValue;
						await arg.Channel.SendMessage("Server pings disabled.");
					});

				cgb.CreateGroup ("cooldown", ccgb => {
					ccgb.UseGlobalWhitelist ().CreateCommand ("status")
						.Alias ("check")
						.Description ("Returns the current cooldown status.")
						.Do (async (arg) => {
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
						LastPing = DateTime.MinValue;
						await arg.Channel.SendMessage ("Cooldown reset.");
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

		public string MinecraftAddress;
		public short MinecraftPort = 25565;
		public TimeSpan Delay = TimeSpan.FromSeconds (30);

		private async void CheckServerStatus (CommandEventArgs args)
		{
			if (DateTime.Now - LastPing < Delay) {
				if (LastPing == DateTime.MaxValue) {
					// Large value
					await args.User.SendMessage("My ping capabilities have been disabled. Please contact an admin to turn me back on");
				} else {
					await args.User.SendMessage ("I only ping the Minecraft server once every " + ((int)Delay.TotalSeconds) + " seconds at most. Try again later.");
				}
				return;
			}

			LastPing = DateTime.Now;

			var servInfo = await GetServerInfo ();

			if (servInfo.Equals (default(MineLib.Network.Modern.BaseClients.ServerInfo)) || servInfo.Players.Online < 0) {
				await args.Channel.SendMessage ("I cannot reach the Minecraft server.");
			} else {
				await args.Channel.SendMessage (string.Format ("The Minecraft server is up! It currently has {0} player{1} online.", servInfo.Players.Online, servInfo.Players.Online == 1 ? string.Empty : "s"));
				if (servInfo.Players.Online > 0) {
					foreach (var player in servInfo.Players.Sample) {
						await args.Channel.SendMessage (string.Format ("{0} is online on the Minecraft.", player.Name));
					}
					if (servInfo.Players.Sample.Count != servInfo.Players.Online) {
						// TODO indicate without being stupid or spammy that other players are online
					}
				}
			}
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
						return new MineLib.Network.Modern.BaseClients.ServerInfo () {
							Description = null,
							Players = new MineLib.Network.Modern.BaseClients.Players () {
								Max = -1,
								Online = -1
							}
						};
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
