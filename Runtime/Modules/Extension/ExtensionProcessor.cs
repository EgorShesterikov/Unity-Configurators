using System;
using System.Collections.Generic;
using UnityEngine;

namespace Utility.Configurators
{
    [Serializable]
    public class ExtensionProcessor
    {
        [SerializeReference, ConfiguratorSelector] public List<IExtension> Extensions;

        public bool TryGetExtension<TExtension>(out TExtension extension) where TExtension : IExtension
        {
            extension = default;

            if (Extensions == null || Extensions.Count == 0)
                return false;

            int matches = 0;
            
            foreach (var ex in Extensions)
            {
                if (ex is TExtension typed)
                {
                    if (matches == 0)
                        extension = typed;
                    
                    matches++;
                }
            }

            if (matches > 1)
            {
                Debug.LogWarning($"[ExtensionProcessor] {matches} extensions of type " +
                                 $"{typeof(TExtension).Name} found; first one returned. " +
                                 "Use GetExtensions to enumerate all matches.");
            }

            return matches > 0;
        }

        public IEnumerable<TExtension> GetExtensions<TExtension>() where TExtension : IExtension
        {
            if (Extensions == null)
                yield break;

            foreach (var ex in Extensions)
                if (ex is TExtension typed)
                    yield return typed;
        }
    }
}
