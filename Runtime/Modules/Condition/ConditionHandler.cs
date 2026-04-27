using System;

namespace Utility.Configurators
{
    public abstract class ConditionHandler<TData> : IConditionHandler where TData : class
    {
        protected TData Data { get; private set; }

        private Action _onChanged;

        public void SetData(object data) => Data = data as TData;

        public abstract bool IsMet();

        public void AddListener(Action onChanged)
        {
            var wasEmpty = _onChanged == null;
            _onChanged += onChanged;

            if (wasEmpty)
                OnFirstListenerAdded();
        }

        public void RemoveListener(Action onChanged)
        {
            _onChanged -= onChanged;

            if (_onChanged == null)
                OnLastListenerRemoved();
        }

        protected void NotifyChanged() => _onChanged?.Invoke();

        protected virtual void OnFirstListenerAdded() { }
        protected virtual void OnLastListenerRemoved() { }
    }
}