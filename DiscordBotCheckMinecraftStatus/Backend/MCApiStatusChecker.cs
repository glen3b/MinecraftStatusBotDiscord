using System;
using System.Threading.Tasks;
using System.Net;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using RestSharp.Extensions.MonoHttp;
using Discord.Logging;

namespace DiscordBotCheckMinecraftStatus
{
	public class MCApiStatusChecker : IMinecraftStatusProvider
	{
		public MCApiStatusChecker (LogManager log)
		{
			Log = log;
		}

		private string Endpoint = "http://mcapi.us/server/status";

		private LogManager Log;

		public async Task<IMinecraftServerStatus> GetStatus (string hostname, short port = 25565)
		{
			try {
				UriBuilder target = new UriBuilder (Endpoint);

				target.Query = "ip=" + hostname + "&port=" + port;

				WebClient client = new WebClient ();

				JObject json = null;

				try {
					Log.Debug ("MCApi.us Backend", "Querying backend URL " + target.ToString ());
					json = JObject.Parse (await client.DownloadStringTaskAsync (target.Uri));
				} catch (Exception ex) {
					Log.Error ("MCApi.us Backend", "Error getting and parsing server status information", ex);
					return null;
				}

				if ((string)json ["status"] != "success") {
					Log.Warning ("MCApi.us Backend", "Backend returned abnormal status code '" + ((string)json ["status"]) + "'");
					return null;
				}

				if ((string)json ["error"] != string.Empty) {
					return null;
				}

				MCApiServerStatus status = new MCApiServerStatus ();

				if (!(bool)json ["online"]) {
					status.IsOnline = false;
					return status;
				}

				status.IsOnline = true;
				status.OnlinePlayerCount = (int)json ["players"] ["now"];
				status.MaxPlayerCount = (int)json ["players"] ["max"];

				return status;

			} catch(Exception ex) {
				Log.Error ("MCApi.us Backend", "Error querying backend for server status", ex);
				return null;
			}
		}

		/// <summary>
		/// A server status implementation without a player sample. Setters not exposed through interface, they should only be set here.
		/// </summary>
		class MCApiServerStatus : IMinecraftServerStatus
		{
			public MCApiServerStatus ()
			{
				PlayerSample = System.Linq.Enumerable.Empty<string> ();
				IsOnline = true;
			}

			public int OnlinePlayerCount {
				get;
				set;
			}

			public int MaxPlayerCount {
				get;
				set;
			}

			/// <summary>
			/// Not implemented: this backend does not implement this property.
			/// </summary>
			/// <value>The player sample.</value>
			public IEnumerable<string> PlayerSample {
				get;
				set;
			}

			public bool IsOnline{ get; set; }
		}

		public async Task<IMinecraftServerStatus> GetStatus (IMinecraftServer server)
		{
			return await GetStatus (server.Hostname, server.Port);
		}
	}
}

