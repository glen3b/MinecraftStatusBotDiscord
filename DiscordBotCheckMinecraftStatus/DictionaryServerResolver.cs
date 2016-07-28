using System;
using System.Collections.Generic;

namespace DiscordBotCheckMinecraftStatus
{
	public class DictionaryServerResolver : IServerResolver
	{
		public DictionaryServerResolver ()
		{
		}

		public void AddServer (Discord.Server voice, IMinecraftServer minecraft)
		{
			if (_data.ContainsKey (voice)) {
				throw new InvalidOperationException ("This resolver already contains the specified server.");
			}

			_data.Add (voice, new BasicServerInformation (Program.GetDefaultChannel (voice), minecraft));
		}

		IDictionary<Discord.Server, BasicServerInformation> _data = new Dictionary<Discord.Server, BasicServerInformation>();

		public IServerInformation this [Discord.Server voice] {
			get {
				BasicServerInformation info = null;
				if (!_data.TryGetValue (voice, out info)) {
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

		class BasicServerInformation : IServerInformation{
			public IMinecraftServer Minecraft {
				get;
				protected set;
			}

			public DateTime LastPing {
				get;
				set;
			}

			public BasicServerInformation() : this(null, null){

			}

			public BasicServerInformation(Discord.Channel defaultChannel, IMinecraftServer server){
				UptimeSubscribers = new HashSet<Discord.User>();
				LastPing = DateTime.MinValue;
				Minecraft = server;
			}

			public ISet<Discord.User> UptimeSubscribers {
				get;
				protected set;
			}

			public Discord.Channel DefaultChannel {
				get;
				protected set;
			}

		}
	}
}

