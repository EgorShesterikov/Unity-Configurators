using System;
using UnityEngine;

namespace Utility.Configurators
{
    [Serializable]
    public abstract class InstructionData<THandler> : Instruction, IHandlerBinder
        where THandler : class, IInstructionHandler
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

        public override void Apply()
        {
            _handler.Apply();
        }
    }
}
