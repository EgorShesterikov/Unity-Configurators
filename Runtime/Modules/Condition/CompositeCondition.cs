using System;
using System.Collections.Generic;

namespace Utility.Configurators
{
    [Serializable]
    public abstract class CompositeCondition : ICompositeCondition
    {
        private Action _onChanged;
        
        private int  _externalListenerCount;
        private bool _innerSubscribed;

        public abstract bool IsMet();
        public abstract IEnumerable<ICondition> GetConditions();

        public void AddListener(Action onChanged)
        {
            if (onChanged == null)
                return;

            _onChanged += onChanged;
            _externalListenerCount++;

            if (_innerSubscribed)
                return;
            
            _innerSubscribed = true;

            foreach (var cond in GetConditions())
                cond?.AddListener(OnInnerChanged);
        }

        public void RemoveListener(Action onChanged)
        {
            if (onChanged == null)
                return;

            _onChanged -= onChanged;
            _externalListenerCount--;

            if (_externalListenerCount > 0)
                return;
            
            _externalListenerCount = 0;

            if (!_innerSubscribed)
                return;
            
            _innerSubscribed = false;

            foreach (var cond in GetConditions())
                cond?.RemoveListener(OnInnerChanged);
        }

        private void OnInnerChanged() => _onChanged?.Invoke();
    }
}