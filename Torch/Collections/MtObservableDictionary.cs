using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Threading;
using Torch.Utils;

namespace Torch.Collections
{
    /// <summary>
    /// Multithread safe observable dictionary backed by a hash table. Unlike
    /// <see cref="MtObservableSortedDictionary{TK,TV}"/> keys are unordered, lookups are O(1),
    /// and overwriting the value of an existing key is supported.
    /// </summary>
    /// <typeparam name="TK">Key type</typeparam>
    /// <typeparam name="TV">Value type</typeparam>
    public class MtObservableDictionary<TK, TV> :
        MtObservableCollectionBase<KeyValuePair<TK, TV>>, IDictionary<TK, TV>
    {
        private readonly Dictionary<TK, TV> _backing;

        protected override ReaderWriterLockSlim Lock { get; }

        /// <summary>
        /// Creates an empty observable dictionary
        /// </summary>
        public MtObservableDictionary(IEqualityComparer<TK> comparer = null)
        {
            _backing = new Dictionary<TK, TV>(comparer ?? EqualityComparer<TK>.Default);
            Lock = new ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion);
        }

        /// <inheritdoc/>
        protected override List<KeyValuePair<TK, TV>> Snapshot(List<KeyValuePair<TK, TV>> old)
        {
            if (old == null)
                return new List<KeyValuePair<TK, TV>>(_backing);
            old.Clear();
            foreach (KeyValuePair<TK, TV> kv in _backing)
                old.Add(kv);
            return old;
        }

        // Hash order isn't stable, so mutations are published as a reset. The base class
        // coalesces queued events into a single reset before dispatch anyway.
        private void RaiseChanged()
        {
            MarkSnapshotsDirty();
            OnPropertyChanged(nameof(Count));
            OnPropertyChanged("Item[]");
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }

        /// <inheritdoc/>
        public override int Count
        {
            get
            {
                using (Lock.ReadUsing())
                    return _backing.Count;
            }
        }

        /// <inheritdoc/>
        public override bool IsReadOnly => false;

        /// <inheritdoc/>
        public TV this[TK key]
        {
            get
            {
                using (Lock.ReadUsing())
                    return _backing[key];
            }
            set
            {
                using (Lock.WriteUsing())
                {
                    _backing[key] = value;
                    RaiseChanged();
                }
            }
        }

        /// <inheritdoc/>
        public void Add(TK key, TV value)
        {
            using (Lock.WriteUsing())
            {
                _backing.Add(key, value);
                RaiseChanged();
            }
        }

        /// <inheritdoc/>
        public bool Remove(TK key)
        {
            using (Lock.WriteUsing())
            {
                if (!_backing.Remove(key))
                    return false;
                RaiseChanged();
                return true;
            }
        }

        /// <inheritdoc/>
        public bool ContainsKey(TK key)
        {
            using (Lock.ReadUsing())
                return _backing.ContainsKey(key);
        }

        /// <inheritdoc/>
        public bool TryGetValue(TK key, out TV value)
        {
            using (Lock.ReadUsing())
                return _backing.TryGetValue(key, out value);
        }

        /// <inheritdoc/>
        public override void Clear()
        {
            using (Lock.WriteUsing())
            {
                _backing.Clear();
                RaiseChanged();
            }
        }

        /// <inheritdoc/>
        public override void Add(KeyValuePair<TK, TV> item) => Add(item.Key, item.Value);

        /// <inheritdoc/>
        public override bool Remove(KeyValuePair<TK, TV> item) => Remove(item.Key);

        /// <inheritdoc/>
        public override bool Contains(KeyValuePair<TK, TV> item)
            => TryGetValue(item.Key, out TV val) && EqualityComparer<TV>.Default.Equals(val, item.Value);

        /// <inheritdoc/>
        public override void CopyTo(KeyValuePair<TK, TV>[] array, int arrayIndex)
        {
            using (Lock.ReadUsing())
                ((ICollection<KeyValuePair<TK, TV>>) _backing).CopyTo(array, arrayIndex);
        }

        /// <inheritdoc/>
        public override void CopyTo(Array array, int index)
        {
            using (Lock.ReadUsing())
                foreach (KeyValuePair<TK, TV> kv in _backing)
                    array.SetValue(kv, index++);
        }

        // Copies, so callers can't observe the backing store outside the lock.
        /// <inheritdoc/>
        public ICollection<TK> Keys
        {
            get
            {
                using (Lock.ReadUsing())
                    return new List<TK>(_backing.Keys);
            }
        }

        /// <inheritdoc/>
        public ICollection<TV> Values
        {
            get
            {
                using (Lock.ReadUsing())
                    return new List<TV>(_backing.Values);
            }
        }
    }
}
