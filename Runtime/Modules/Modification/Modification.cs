using System;

namespace Utility.Configurators
{
    [Serializable]
    public abstract class Modification<TContext> : IModification<TContext>
    {
        public abstract void Apply(TContext context);
    }
}