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

		public async Task<IServerStatus> GetStatus (string hostname, short port = 25565)
		{
			try {
				UriBuilder target = new UriBuilder (Endpoint);

				target.Query = "ip=" + hostname + "&port=" + port;

				WebClient client = new WebClient ();

				JObject json = null;

				try {
					Log.Debug("MCApi.us Backend", "Querying backend URL " + target.ToString());
					json = JObject.Parse (await client.DownloadStringTaskAsync (target.Uri));
				} catch (Exception ex) {
					Log.Error ("MCApi.us Backend", "Error getting and parsing server status information.", ex);
				}

				if ((string)json ["status"] != "success") {
					Log.Warning ("MCApi.us Backend", "Backend returned abnormal status code '"+((string)json["status"]) + ",' result may be erroneous.");
					return null;
				}

				if ((string)json ["error"] != string.Empty) {
					return null;
				}

				MCApiServerStatus status = new MCApiServerStatus ();

				if (!(bool)json ["online"]) {
					status.OnlinePlayerCount = -1;
					status.MaxPlayerCount = -1;
					return status;
				}

				status.OnlinePlayerCount = (int)json ["players"] ["now"];
				status.MaxPlayerCount = (int)json ["players"] ["max"];

				return status;

			} catch {
				return null;
			}
		}

		/// <summary>
		/// A server status implementation without a player sample. Setters not exposed through interface, they should only be set here.
		/// </summary>
		class MCApiServerStatus : IServerStatus
		{
			public MCApiServerStatus ()
			{
				PlayerSample = System.Linq.Enumerable.Empty<string> ();
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
		}

		public async Task<IServerStatus> GetStatus (IMinecraftServer server)
		{
			return GetStatus (server.Hostname, server.Port);
		}
	}
}

