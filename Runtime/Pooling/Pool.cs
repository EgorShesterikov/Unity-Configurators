using System;
using System.Collections.Generic;

namespace Utility.Configurators.Pooling
{
    internal class Pool<T>
    {
        public int FreeCount => _freeObjects.Count;
        public int OccupiedCount => _occupiedObjects.Count;

        public IReadOnlyCollection<T> Free => _freeObjects;
        public IReadOnlyCollection<T> Occupied => _occupiedObjects;

        private readonly Func<T> _factory;
        private readonly Action<T> _actionOnGet;
        private readonly Action<T> _actionOnRelease;
        private readonly Action<T> _actionOnDiscard;

        private readonly List<T> _freeObjects = new();
        private readonly HashSet<T> _occupiedObjects = new();

        public Pool(Func<T> factory, Action<T> actionOnGet = null, Action<T> actionOnRelease = null, Action<T> actionOnDiscard = null)
        {
            _factory = factory;
            _actionOnGet = actionOnGet;
            _actionOnRelease = actionOnRelease;
            _actionOnDiscard = actionOnDiscard;
        }

        public void Prewarm(int count)
        {
            for (var i = 0; i < count; i++)
            {
                var instance = _factory.Invoke();
                _actionOnRelease?.Invoke(instance);
                _freeObjects.Add(instance);
            }
        }

        public T Get()
        {
            T instance;

            if (_freeObjects.Count == 0)
            {
                instance = _factory.Invoke();
            }
            else
            {
                var lastIndex = _freeObjects.Count - 1;
                instance = _freeObjects[lastIndex];
                _freeObjects.RemoveAt(lastIndex);
            }

            _actionOnGet?.Invoke(instance);
            _occupiedObjects.Add(instance);

            return instance;
        }

        public void Release(T instance)
        {
            if (_occupiedObjects.Remove(instance))
            {
                _actionOnRelease?.Invoke(instance);
                _freeObjects.Add(instance);
            }
        }

        public void Discard(T instance)
        {
            if (_occupiedObjects.Remove(instance))
            {
                _actionOnDiscard?.Invoke(instance);
                return;
            }

            if (_freeObjects.Remove(instance))
                _actionOnDiscard?.Invoke(instance);
        }

        public void DiscardAll()
        {
            if (_actionOnDiscard != null)
            {
                foreach (var instance in _freeObjects)
                    _actionOnDiscard.Invoke(instance);

                foreach (var instance in _occupiedObjects)
                    _actionOnDiscard.Invoke(instance);
            }

            _freeObjects.Clear();
            _occupiedObjects.Clear();
        }
    }
}
