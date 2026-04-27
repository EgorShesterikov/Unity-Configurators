using System;
using UnityEngine;

namespace Utility.Configurators
{
    public interface IConfiguratorManager
    {
        IDisposable ApplyModifications<TContext>(ModificationProcessor<TContext> processor, TContext context, Component lifetimeOwner = null);
        IDisposable SubscribeConditions(ConditionProcessor processor, Action<bool> onChanged, Component lifetimeOwner = null);

        IDisposable ResolveModifications<TContext>(ModificationProcessor<TContext> processor);
        IDisposable ResolveConditions(ConditionProcessor processor);
    }
}
