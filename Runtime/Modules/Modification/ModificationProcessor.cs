using System;
using System.Collections.Generic;
using UnityEngine;

namespace Utility.Configurators
{
    [Serializable]
    public class ModificationProcessor<TContext>
    {
        [SerializeReference, ConfiguratorSelector] public List<IModification<TContext>> Modifications;

        public void Apply(TContext context)
        {
            if (Modifications == null || Modifications.Count == 0)
                return;

            foreach (var mod in Modifications)
            {
                if (mod == null) 
                    continue;

                try { mod.Apply(context); }
                catch (Exception e) { Debug.LogException(e); }
            }
        }
    }
}
