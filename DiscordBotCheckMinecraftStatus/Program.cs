using DiscordSharp;
using System;
using System.Configuration;
using DiscordSharp.Objects;
using DiscordSharp.Events;

namespace DiscordBotCheckMinecraftStatus
{
	class MainClass
	{
		public static void Main (string[] args)
		{
			Console.WriteLine ("Discord Minecraft Checker Bot Starting");
			Console.WriteLine ("Copyright (C) 2016 Glen Husman");
			Console.WriteLine ("Licensed under the GNU General Public License v3");

			// https://discordapp.com/oauth2/authorize?client_id=207342659868557312&scope=bot&permissions=68608

			// I'm not very competent when it comes to threading
			System.Threading.AutoResetEvent sync = new System.Threading.AutoResetEvent (false);

			DiscordClient client = new DiscordClient (ConfigurationManager.AppSettings ["BotToken"], true, true);

			client.Connected += (object sender, DiscordConnectEventArgs e) => {
				Console.WriteLine ("Connected as bot user {0}", e.User.Username);
				sync.Set ();
			};

			client.MessageReceived += OnMessageReceive;

			client.EnableVerboseLogging = true;

			client.Connect ();
			sync.WaitOne ();

			Console.WriteLine ("[Debug] Main Thread Resuming");

			// client.AcceptInvite (ConfigurationManager.AppSettings ["ServerID"]);

//			bool ignoreKeyPresses = true;
//
//			// Allow escaping
//			Console.CancelKeyPress += (object sender, ConsoleCancelEventArgs e) => {
//				ignoreKeyPresses = false;
//				e.Cancel = true;
//			};
//
//			// Do nothing
//			while (ignoreKeyPresses) {
//				Console.ReadKey (true);
//			}
//
//			Console.WriteLine ("Exiting...");
//			client.Dispose ();

			Console.ReadLine ();
		}

		public static void OnMessageReceive (object sender, DiscordMessageEventArgs e)
		{
			// DiscordClient client = (DiscordClient)sender;
			if (e.MessageType == DiscordMessageType.PRIVATE) {
				Console.WriteLine ("Received private message from {0} with text: \"{1}\"", e.Author.Username, e.MessageText);
			}
		}
	}
}
