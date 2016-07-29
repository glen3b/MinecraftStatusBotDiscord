using System;
using System.Collections.Generic;
using System.Linq;
using Discord;

namespace DiscordBotCheckMinecraftStatus
{
	/// <summary>
	/// An ISet implementation specialized for Discord users.
	/// Stores users by ID instead of by a reference to an object.
	/// </summary>
	public class DiscordUserSet : ISet<User>
	{
		public DiscordUserSet (IUserServerCache cache)
		{
			_cache = cache;
		}

		private IUserServerCache _cache;
		private ISet<ulong> _backingIdSet = new HashSet<ulong>();

		public bool Add (User item)
		{
			return _backingIdSet.Add (item.Id);
		}

		public void UnionWith (IEnumerable<User> other)
		{
			_backingIdSet.UnionWith (other.Select ((u) => u.Id));
		}

		public void IntersectWith (IEnumerable<User> other)
		{
			_backingIdSet.IntersectWith (other.Select ((u) => u.Id));
		}

		public void ExceptWith (IEnumerable<User> other)
		{
			_backingIdSet.ExceptWith (other.Select ((u) => u.Id));
		}

		public void SymmetricExceptWith (IEnumerable<User> other)
		{
			_backingIdSet.SymmetricExceptWith (other.Select ((u) => u.Id));
		}

		public bool IsSubsetOf (IEnumerable<User> other)
		{
			return _backingIdSet.IsSubsetOf (other.Select ((u) => u.Id));
		}

		public bool IsSupersetOf (IEnumerable<User> other)
		{
			return _backingIdSet.IsSupersetOf (other.Select ((u) => u.Id));
		}

		public bool IsProperSupersetOf (IEnumerable<User> other)
		{
			return _backingIdSet.IsProperSupersetOf (other.Select ((u) => u.Id));
		}

		public bool IsProperSubsetOf (IEnumerable<User> other)
		{
			return _backingIdSet.IsProperSubsetOf (other.Select ((u) => u.Id));
		}

		public bool Overlaps (IEnumerable<User> other)
		{
			return _backingIdSet.Overlaps (other.Select ((u) => u.Id));
		}

		public bool SetEquals (IEnumerable<User> other)
		{
			return _backingIdSet.SetEquals (other.Select ((u) => u.Id));
		}

		void ICollection<User>.Add (User item)
		{
			((ICollection<ulong>)_backingIdSet).Add (item.Id);
		}

		public void Clear ()
		{
			_backingIdSet.Clear ();
		}

		public bool Contains (User item)
		{
			return _backingIdSet.Contains (item.Id);
		}

		public void CopyTo (User[] array, int arrayIndex)
		{
			// TODO dont convert to a list
			_backingIdSet.Select ((i) => _cache.GetEffectiveServer(i, null)?.GetUser(i)).ToList ().CopyTo (array, arrayIndex);
		}

		public bool Remove (User item)
		{
			return _backingIdSet.Remove (item.Id);
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
			return _backingIdSet.Select ((u) => _cache.GetEffectiveServer(u, null)?.GetUser(u)).GetEnumerator ();
		}

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator ()
		{
			return this.GetEnumerator ();
		}
	}
}

