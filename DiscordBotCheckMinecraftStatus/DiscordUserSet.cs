using System;
using System.Collections.Generic;
using System.Linq;
using Discord;

namespace DiscordBotCheckMinecraftStatus
{
	static class SetUserExtensions{

		public static UserData GetUserSignature(this IUserServerCache cache, User usr){
			return new UserData (cache.GetEffectiveServer(usr, usr.Server), usr.Id);
		}

		public static User GetUser(this DiscordClient client, UserData user){
			return client.GetServer (user.ServerID).GetUser (user.UserID);
		}

		public struct UserData{
			public ulong ServerID;
			public ulong UserID;

			public UserData(ulong server, ulong user){
				ServerID = server;
				UserID = user;
			}

			public override int GetHashCode ()
			{
				return ServerID.GetHashCode() ^ UserID.GetHashCode();
			}

			public override bool Equals (object obj)
			{
				if (!(obj is UserData)) {
					return false;
				}

				UserData other = (UserData)obj;

				return other.ServerID == ServerID && other.UserID == UserID;
			}
		}
	}

	/// <summary>
	/// An ISet implementation specialized for Discord users.
	/// Stores users by ID instead of by a reference to an object.
	/// </summary>
	public class DiscordUserSet : ISet<User>
	{
		public DiscordUserSet (DiscordClient client, IUserServerCache cache)
		{
			_client = client;
			_cache = cache;
		}

		private DiscordClient _client;
		private IUserServerCache _cache;
		private ISet<DiscordBotCheckMinecraftStatus.SetUserExtensions.UserData> _backingIdSet = new HashSet<DiscordBotCheckMinecraftStatus.SetUserExtensions.UserData>();

		public bool Add (User item)
		{
			return _backingIdSet.Add (_cache.GetUserSignature(item));
		}

		public void UnionWith (IEnumerable<User> other)
		{
			_backingIdSet.UnionWith (other.Select ((u) => _cache.GetUserSignature(u)));
		}

		public void IntersectWith (IEnumerable<User> other)
		{
			_backingIdSet.IntersectWith (other.Select ((u) => _cache.GetUserSignature(u)));
		}

		public void ExceptWith (IEnumerable<User> other)
		{
			_backingIdSet.ExceptWith (other.Select ((u) => _cache.GetUserSignature(u)));
		}

		public void SymmetricExceptWith (IEnumerable<User> other)
		{
			_backingIdSet.SymmetricExceptWith (other.Select ((u) => _cache.GetUserSignature(u)));
		}

		public bool IsSubsetOf (IEnumerable<User> other)
		{
			return _backingIdSet.IsSubsetOf (other.Select ((u) => _cache.GetUserSignature(u)));
		}

		public bool IsSupersetOf (IEnumerable<User> other)
		{
			return _backingIdSet.IsSupersetOf (other.Select ((u) => _cache.GetUserSignature(u)));
		}

		public bool IsProperSupersetOf (IEnumerable<User> other)
		{
			return _backingIdSet.IsProperSupersetOf (other.Select ((u) => _cache.GetUserSignature(u)));
		}

		public bool IsProperSubsetOf (IEnumerable<User> other)
		{
			return _backingIdSet.IsProperSubsetOf (other.Select ((u) => _cache.GetUserSignature(u)));
		}

		public bool Overlaps (IEnumerable<User> other)
		{
			return _backingIdSet.Overlaps (other.Select ((u) => _cache.GetUserSignature(u)));
		}

		public bool SetEquals (IEnumerable<User> other)
		{
			return _backingIdSet.SetEquals (other.Select ((u) => _cache.GetUserSignature(u)));
		}

		void ICollection<User>.Add (User item)
		{
			((ICollection<DiscordBotCheckMinecraftStatus.SetUserExtensions.UserData>)_backingIdSet).Add (_cache.GetUserSignature(item));
		}

		public void Clear ()
		{
			_backingIdSet.Clear ();
		}

		public bool Contains (User item)
		{
			return _backingIdSet.Contains (_cache.GetUserSignature(item));
		}

		public void CopyTo (User[] array, int arrayIndex)
		{
			// TODO dont convert to a list
			_backingIdSet.Select ((i) => _client.GetUser (i)).ToList ().CopyTo (array, arrayIndex);
		}

		public bool Remove (User item)
		{
			return _backingIdSet.Remove (_cache.GetUserSignature(item));
		}

		public int Count {
			get {
				return _backingIdSet.Count;
			}
		}

		public bool IsReadOnly {
			get {
				return _backingIdSet.IsReadOnly;
			}
		}

		public IEnumerator<User> GetEnumerator ()
		{
			return _backingIdSet.Select ((u) => _client.GetUser (u)).GetEnumerator ();
		}

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator ()
		{
			return this.GetEnumerator ();
		}
	}
}

