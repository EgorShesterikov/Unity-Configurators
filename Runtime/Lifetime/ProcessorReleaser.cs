using System;
using System.Collections.Generic;
using UnityEngine;

namespace Utility.Configurators
{
    [DisallowMultipleComponent]
    [AddComponentMenu("")]
    public sealed class ProcessorReleaser : MonoBehaviour
    {
        private readonly List<IDisposable> _bindings = new();
        private bool _isDestroyed;

        public void Add(IDisposable disposable)
        {
            if (disposable == null)
                return;

            if (_isDestroyed)
            {
                try { disposable.Dispose(); }
                catch (Exception e) { Debug.LogException(e); }
                return;
            }

            _bindings.Add(disposable);
        }

        private void OnDestroy()
        {
            _isDestroyed = true;

            for (int i = _bindings.Count - 1; i >= 0; i--)
            {
                try { _bindings[i]?.Dispose(); }
                catch (Exception e) { Debug.LogException(e); }
            }

            _bindings.Clear();
        }
    }
}
