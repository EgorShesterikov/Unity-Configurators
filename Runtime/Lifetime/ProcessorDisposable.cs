using System;
using System.Collections.Generic;
using UnityEngine;

namespace Utility.Configurators
{
    public sealed class ProcessorDisposable : IDisposable
    {
        private readonly List<Action> _releaseActions = new();
        private bool _disposed;

        internal void Register(Action release)
        {
            if (release == null)
                return;

            if (_disposed)
            {
                try { release(); }
                catch (Exception e) { Debug.LogException(e); }
                return;
            }

            _releaseActions.Add(release);
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            
            _disposed = true;

            for (int i = _releaseActions.Count - 1; i >= 0; i--)
            {
                try { _releaseActions[i]?.Invoke(); }
                catch (Exception e) { Debug.LogException(e); }
            }

            _releaseActions.Clear();
        }
    }
}
