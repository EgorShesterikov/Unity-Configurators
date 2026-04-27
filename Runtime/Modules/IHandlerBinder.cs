using System;

namespace Utility.Configurators
{
    public interface IHandlerBinder
    {
        Type HandlerType { get; }
        void BindHandler(object handler);
        object GetHandler();
        void UnbindHandler();
    }
}