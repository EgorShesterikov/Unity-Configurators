using System;
using System.Collections.Generic;
using UnityEngine;
using Utility.Configurators.Pooling;
using Zenject;

namespace Utility.Configurators
{
    public sealed class ConfiguratorManager : IConfiguratorManager
    {
        [Inject] private readonly IInstantiator _instantiator;

        private readonly MultiPool<Type, IConditionHandler> _conditionHandlerPool = new();
        private readonly MultiPool<Type, IModificationHandler> _modificationHandlerPool = new();

        private readonly Dictionary<object, IDisposable> _activeBindings = new();

        public IDisposable ApplyModifications<TContext>(ModificationProcessor<TContext> processor, TContext context, Component lifetimeOwner = null)
        {
            if (processor == null)
                return EmptyDisposable.Instance;

            var binding = ResolveModifications(processor);

            BindLifetime(binding, lifetimeOwner);
            processor.Apply(context);

            return binding;
        }

        public IDisposable SubscribeConditions(ConditionProcessor processor, Action<bool> onChanged, Component lifetimeOwner = null)
        {
            if (processor == null)
                return EmptyDisposable.Instance;

            var resolveBinding = ResolveConditions(processor);
            processor.Initialize(onChanged);

            var combined = new ProcessorDisposable();

            combined.Register(resolveBinding.Dispose);
            combined.Register(processor.Dispose);

            BindLifetime(combined, lifetimeOwner);
            return combined;
        }

        public IDisposable ResolveModifications<TContext>(ModificationProcessor<TContext> processor)
        {
            if (processor == null)
                return EmptyDisposable.Instance;

            if (_activeBindings.ContainsKey(processor))
            {
                Debug.LogWarning($"[ConfiguratorManager] ModificationProcessor<{typeof(TContext).Name}> is already resolved. " +
                                 "Dispose the previous binding before resolving again.");
                return EmptyDisposable.Instance;
            }

            var disposable = new ProcessorDisposable();
            _activeBindings[processor] = disposable;

            var modifications = processor.Modifications;
            
            if (modifications != null)
                foreach (var mod in modifications)
                    if (mod is IHandlerBinder binder)
                        BindHandler(_modificationHandlerPool, binder);

            disposable.Register(() => UnregisterModification(processor));
            return disposable;
        }

        public IDisposable ResolveConditions(ConditionProcessor processor)
        {
            if (processor == null)
                return EmptyDisposable.Instance;

            if (_activeBindings.ContainsKey(processor))
            {
                Debug.LogWarning($"[ConfiguratorManager] ConditionProcessor is already resolved. " +
                                 "Dispose the previous binding before resolving again.");
                return EmptyDisposable.Instance;
            }

            var disposable = new ProcessorDisposable();
            _activeBindings[processor] = disposable;

            var visited = UnityEngine.Pool.HashSetPool<ICondition>.Get();
            
            try
            {
                var conditions = processor.Conditions;
                if (conditions != null)
                    foreach (var cond in conditions)
                        BindConditionRecursive(cond, visited);
            }
            finally
            {
                UnityEngine.Pool.HashSetPool<ICondition>.Release(visited);
            }

            disposable.Register(() => UnregisterCondition(processor));
            return disposable;
        }

        private static void BindLifetime(IDisposable disposable, Component owner)
        {
            if (disposable == null || owner == null)
                return;

            if (!owner.TryGetComponent(out ProcessorReleaser releaser))
                releaser = owner.gameObject.AddComponent<ProcessorReleaser>();

            releaser.Add(disposable);
        }

        private void UnregisterModification<TContext>(ModificationProcessor<TContext> processor)
        {
            _activeBindings.Remove(processor);

            var modifications = processor.Modifications;
            
            if (modifications == null)
                return;

            foreach (var mod in modifications)
                if (mod is IHandlerBinder binder)
                    ReleaseHandler(_modificationHandlerPool, binder);
        }

        private void UnregisterCondition(ConditionProcessor processor)
        {
            _activeBindings.Remove(processor);

            var conditions = processor.Conditions;
            
            if (conditions == null)
                return;

            var visited = UnityEngine.Pool.HashSetPool<ICondition>.Get();
            
            try
            {
                foreach (var cond in conditions)
                    ReleaseConditionRecursive(cond, visited);
            }
            finally
            {
                UnityEngine.Pool.HashSetPool<ICondition>.Release(visited);
            }
        }

        private void BindConditionRecursive(ICondition cond, HashSet<ICondition> visited)
        {
            if (cond == null)
                return;
            
            if (!visited.Add(cond))
            {
                Debug.LogError($"[ConfiguratorManager] Cycle detected while resolving conditions: " +
                               $"{cond.GetType().Name} is referenced more than once in the same composite tree.");
                return;
            }

            if (cond is IHandlerBinder binder)
                BindHandler(_conditionHandlerPool, binder);

            if (cond is ICompositeCondition composite)
                foreach (var inner in composite.GetConditions())
                    BindConditionRecursive(inner, visited);
        }

        private void ReleaseConditionRecursive(ICondition cond, HashSet<ICondition> visited)
        {
            if (cond == null)
                return;
            
            if (!visited.Add(cond))
                return;

            if (cond is IHandlerBinder binder)
                ReleaseHandler(_conditionHandlerPool, binder);

            if (cond is ICompositeCondition composite)
                foreach (var inner in composite.GetConditions())
                    ReleaseConditionRecursive(inner, visited);
        }

        private void BindHandler<T>(MultiPool<Type, T> pool, IHandlerBinder binder) where T : class
        {
            var type = binder.HandlerType;
            
            if (type == null)
            {
                Debug.LogError($"[ConfiguratorManager] {binder.GetType().Name} returned null HandlerType.");
                return;
            }

            T handler;
            
            try
            {
                handler = GetOrCreate(pool, type);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ConfiguratorManager] Failed to instantiate handler {type.Name}: {ex.Message}");
                throw;
            }

            if (handler == null)
            {
                Debug.LogError($"[ConfiguratorManager] Handler {type.Name} resolved to null.");
                return;
            }

            binder.BindHandler(handler);
        }

        private void ReleaseHandler<T>(MultiPool<Type, T> pool, IHandlerBinder binder) where T : class
        {
            if (binder.GetHandler() is T handler)
            {
                binder.UnbindHandler();
                pool.Release(binder.HandlerType, handler);
            }
        }

        private T GetOrCreate<T>(MultiPool<Type, T> pool, Type type) where T : class
        {
            if (!pool.HasFactory(type))
                pool.RegisterFactory(type, () => (T)_instantiator.Instantiate(type));

            return pool.Get(type);
        }
    }
    
    internal sealed class EmptyDisposable : IDisposable
    {
        public static readonly EmptyDisposable Instance = new();
        private EmptyDisposable() { }
        public void Dispose() { }
    }
}
