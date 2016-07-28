using System;
using System.Collections.Generic;
using System.Linq;
using Discord;

namespace DiscordBotCheckMinecraftStatus
{
	static class SetUserExtensions{

		public static UserData GetSignature(this User usr){
			return new UserData (usr.Server.Id, usr.Id);
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
		public DiscordUserSet (DiscordClient client)
		{
			_client = client;
		}

		private DiscordClient _client;
		private ISet<DiscordBotCheckMinecraftStatus.SetUserExtensions.UserData> _backingIdSet = new HashSet<DiscordBotCheckMinecraftStatus.SetUserExtensions.UserData>();

		public bool Add (User item)
		{
			return _backingIdSet.Add (item.GetSignature());
		}

		public void UnionWith (IEnumerable<User> other)
		{
			_backingIdSet.UnionWith (other.Select ((u) => u.GetSignature()));
		}

		public void IntersectWith (IEnumerable<User> other)
		{
			_backingIdSet.IntersectWith (other.Select ((u) => u.GetSignature()));
		}

		public void ExceptWith (IEnumerable<User> other)
		{
			_backingIdSet.ExceptWith (other.Select ((u) => u.GetSignature()));
		}

		public void SymmetricExceptWith (IEnumerable<User> other)
		{
			_backingIdSet.SymmetricExceptWith (other.Select ((u) => u.GetSignature()));
		}

		public bool IsSubsetOf (IEnumerable<User> other)
		{
			return _backingIdSet.IsSubsetOf (other.Select ((u) => u.GetSignature()));
		}

		public bool IsSupersetOf (IEnumerable<User> other)
		{
			return _backingIdSet.IsSupersetOf (other.Select ((u) => u.GetSignature()));
		}

		public bool IsProperSupersetOf (IEnumerable<User> other)
		{
			return _backingIdSet.IsProperSupersetOf (other.Select ((u) => u.GetSignature()));
		}

		public bool IsProperSubsetOf (IEnumerable<User> other)
		{
			return _backingIdSet.IsProperSubsetOf (other.Select ((u) => u.GetSignature()));
		}

		public bool Overlaps (IEnumerable<User> other)
		{
			return _backingIdSet.Overlaps (other.Select ((u) => u.GetSignature()));
		}

		public bool SetEquals (IEnumerable<User> other)
		{
			return _backingIdSet.SetEquals (other.Select ((u) => u.GetSignature()));
		}

		void ICollection<User>.Add (User item)
		{
			((ICollection<DiscordBotCheckMinecraftStatus.SetUserExtensions.UserData>)_backingIdSet).Add (item.GetSignature());
		}

		public void Clear ()
		{
			_backingIdSet.Clear ();
		}

		public bool Contains (User item)
		{
			return _backingIdSet.Contains (item.GetSignature());
		}

		public void CopyTo (User[] array, int arrayIndex)
		{
			// TODO dont convert to a list
			_backingIdSet.Select ((i) => _client.GetUser (i)).ToList ().CopyTo (array, arrayIndex);
		}

		public bool Remove (User item)
		{
			return _backingIdSet.Remove (item.GetSignature ());
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

