using System;
using System.Collections.Generic;

namespace Utility.Configurators.Pooling
{
    internal class MultiPool<TKey, TValue>
    {
        private readonly Dictionary<TKey, Pool<TValue>> _pools = new();

        public bool HasFactory(TKey key) => _pools.ContainsKey(key);

        public void RegisterFactory(TKey key, Func<TValue> factory, Action<TValue> actionOnGet = null, Action<TValue> actionOnRelease = null)
        {
            if (factory == null)
                throw new ArgumentNullException(nameof(factory));

            _pools.TryAdd(key, new Pool<TValue>(factory, actionOnGet, actionOnRelease));
        }

        public void Prewarm(TKey key, int count)
        {
            if (_pools.TryGetValue(key, out var pool))
                pool.Prewarm(count);
        }

        public TValue Get(TKey key)
        {
            if (_pools.TryGetValue(key, out var pool))
                return pool.Get();

            throw new KeyNotFoundException($"[MultiPool] Cannot get object: factory for key '{key}' is not registered!");
        }

        public void Release(TKey key, TValue instance)
        {
            if (_pools.TryGetValue(key, out var pool))
                pool.Release(instance);
        }

        public void Discard(TKey key, TValue instance)
        {
            if (_pools.TryGetValue(key, out var pool))
                pool.Discard(instance);
        }

        public void DiscardAll(TKey key)
        {
            if (_pools.TryGetValue(key, out var pool))
                pool.DiscardAll();
        }
    }
}
