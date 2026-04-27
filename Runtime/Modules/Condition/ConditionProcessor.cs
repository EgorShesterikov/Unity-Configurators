using System;
using System.Collections.Generic;
using UnityEngine;

namespace Utility.Configurators
{
    [Serializable]
    public class ConditionProcessor
    {
        [SerializeReference, ConfiguratorSelector] public List<ICondition> Conditions;

        private Action<bool> _onChanged;
        private bool _initialized;

        public void Initialize(Action<bool> onChanged)
        {
            if (_initialized)
            {
                Debug.LogWarning("[ConditionProcessor] Already initialized. Dispose before re-initializing.");
                return;
            }
            
            _initialized = true;
            _onChanged = onChanged;

            if (Conditions is { Count: > 0 })
                foreach (var condition in Conditions)
                    condition?.AddListener(OnConditionChanged);

            try { _onChanged?.Invoke(IsMet()); }
            catch (Exception e) { Debug.LogException(e); }
        }

        public void Dispose()
        {
            if (!_initialized)
                return;
            
            _initialized = false;

            if (Conditions is { Count: > 0 })
                foreach (var condition in Conditions)
                    condition?.RemoveListener(OnConditionChanged);

            _onChanged = null;
        }

        private void OnConditionChanged()
        {
            try { _onChanged?.Invoke(IsMet()); }
            catch (Exception e) { Debug.LogException(e); }
        }

        public bool IsMet()
        {
            if (Conditions == null || Conditions.Count == 0)
                return true;
            
            foreach (var condition in Conditions)
                if (!condition?.IsMet() ?? false)
                    return false;

            return true;
        }
    }
}
