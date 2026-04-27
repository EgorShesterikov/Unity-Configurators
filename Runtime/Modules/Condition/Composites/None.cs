using System;
using System.Collections.Generic;
using UnityEngine;

namespace Utility.Configurators
{
    [Serializable]
    [ConfiguratorCategory("Composite")]
    public class None : CompositeCondition
    {
        [SerializeReference, ConfiguratorSelector]
        public List<ICondition> Conditions;

        public override bool IsMet()
        {
            if (Conditions == null || Conditions.Count == 0)
                return true;

            foreach (var cond in Conditions)
                if (cond?.IsMet() ?? false)
                    return false;

            return true;
        }

        public override IEnumerable<ICondition> GetConditions() => Conditions;
    }
}