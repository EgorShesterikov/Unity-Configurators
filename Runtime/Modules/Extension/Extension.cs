using System;

namespace Utility.Configurators
{
    [Serializable]
    public abstract class Extension<T> : IExtension
    {
        public abstract T Value { get; }

        public static implicit operator T(Extension<T> extension) => extension.Value;
    }
}
