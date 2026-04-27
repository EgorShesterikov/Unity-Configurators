using System;

namespace Utility.Configurators
{
    [Serializable]
    public abstract class Condition : ICondition
    {
        private Action _onChanged;
        
        public abstract bool IsMet();
        
        public void AddListener(Action onChanged) => _onChanged += onChanged;
        public void RemoveListener(Action onChanged) => _onChanged -= onChanged;

        protected void NotifyChanged() => _onChanged?.Invoke();
    }
}