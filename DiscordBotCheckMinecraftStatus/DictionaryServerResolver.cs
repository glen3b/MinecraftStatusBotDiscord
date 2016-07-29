using System;
using System.Collections.Generic;

namespace DiscordBotCheckMinecraftStatus
{
	public class DictionaryServerResolver : IServerResolver
	{
		public DictionaryServerResolver (Discord.DiscordClient client)
		{
			Client = client;
		}

		protected Discord.DiscordClient Client;

		public void AddServer (Discord.Server voice, IMinecraftServer minecraft)
		{
			if (_data.ContainsKey (voice.Id)) {
				throw new InvalidOperationException ("This resolver already contains the specified server.");
			}

			_data.Add (voice.Id, new BasicServerInformation (Program.GetDefaultChannel (voice), minecraft));
		}

		IDictionary<ulong, BasicServerInformation> _data = new Dictionary<ulong, BasicServerInformation> ();

		public IServerInformation this [Discord.Server voice] {
			get {
				BasicServerInformation info = null;
				if (!_data.TryGetValue (voice.Id, out info)) {
					return null;
				}

				return info;
			}
		}

		public int Count {
			get {
				return _data.Count;
			}
		}

		public IEnumerator<IServerInformation> GetEnumerator ()
		{
			return _data.Values.GetEnumerator ();
		}

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator ()
		{
			return ((System.Collections.IEnumerable)_data.Values).GetEnumerator ();
		}

		class BasicServerInformation : IServerInformation
		{
			public IMinecraftServer Minecraft {
				get;
				protected set;
			}

			public DateTime LastPing {
				get;
				set;
			}

			private Discord.DiscordClient _client;

			public BasicServerInformation (Discord.Channel defaultChannel, IMinecraftServer server)
			{
				_client = defaultChannel.Client;
				UptimeSubscribers = new DiscordUserSet (Program.UserServerCache);
				LastPing = DateTime.MinValue;
				Minecraft = server;
				DefaultChannel = defaultChannel;
			}

			public ISet<Discord.User> UptimeSubscribers {
				get;
				protected set;
			}

			public Discord.Channel DefaultChannel {
				get {
					return _client.GetChannel (_channelId);
				}
				protected set {
					_channelId = value.Id;
				}
			}

			private ulong _channelId;

		}
	}
}

