using System;
using UnityEngine;

namespace Utility.Configurators
{
    [Serializable]
    public abstract class ConditionData<THandler> : ICondition, IHandlerBinder
        where THandler : class, IConditionHandler
    {
        private THandler _handler;

        Type IHandlerBinder.HandlerType => typeof(THandler);
        object IHandlerBinder.GetHandler() => _handler;

        void IHandlerBinder.BindHandler(object handler)
        {
            if (_handler != null)
            {
                Debug.LogError($"[Configurator] {GetType().Name}: handler is already bound. " +
                               "Resolve was probably called twice without disposing the previous binding.");
                return;
            }

            _handler = (THandler)handler;
            _handler.SetData(this);
        }

        void IHandlerBinder.UnbindHandler()
        {
            if (_handler == null)
                return;
            
            _handler.SetData(null);
            _handler = null;
        }

        public bool IsMet() => _handler.IsMet();
        public void AddListener(Action onChanged) => _handler.AddListener(onChanged);
        public void RemoveListener(Action onChanged) => _handler.RemoveListener(onChanged);
    }
}
