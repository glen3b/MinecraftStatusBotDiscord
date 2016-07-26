using System;
using System.Configuration;
using Discord;

namespace DiscordBotMinecraftChat
{
	class Program
	{
		public static void Main (string[] args)
		{
			Console.WriteLine ("THIS BOT IS NONFUNCTIONAL");
			Console.WriteLine ("PLEASE REMOVE THIS CODE TO RUN");
			return;

			Console.WriteLine ("Discord Minecraft Chat Synchronization Bot");
			Console.WriteLine ("Copyright (C) 2016 Glen Husman");
			Console.WriteLine ("Licensed under the GNU General Public License v3");

			new Program (ConfigurationManager.AppSettings["ChannelID"]).Execute (ConfigurationManager.AppSettings ["BotToken"]);
		}

		System.Threading.AutoResetEvent sync = new System.Threading.AutoResetEvent(false);

		public Program (string channelId)
		{
			Client = new DiscordClient ();
			Client.Ready += (object sender, EventArgs e) => {
				Console.WriteLine ("Signed into Discord under bot username {0}", Client.CurrentUser.Name);
				System.Threading.Thread.Sleep(1000);

				// TODO this does not work
				Channel = Client.GetChannel (ulong.Parse (channelId));
				sync.Set();
			};
			Client.MessageReceived += OnMessageReceive;

		}

		private void OnMessageReceive (object sender, MessageEventArgs message)
		{
			if (message.User.IsBot) {
				// Ignore bot messages
				return;
			}

			// TODO send message to Minecraft
		}

		public DiscordClient Client;
		public Channel Channel;

		public void Execute (string token)
		{
			Client.Connect (token);

			sync.WaitOne ();

			while(true){
				// TODO don't use log file parsing, use something better
				string input = Console.ReadLine();

				// TODO parse input to extract the message instead of printing it - reformat for Discord instead of log
				Channel.SendMessage(input);
			}
		}
	}
}
