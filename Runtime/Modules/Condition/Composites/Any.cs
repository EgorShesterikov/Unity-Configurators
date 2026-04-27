using System;
using System.Collections.Generic;
using UnityEngine;

namespace Utility.Configurators
{
    [Serializable]
    [ConfiguratorCategory("Composite")]
    public class Any : CompositeCondition
    {
        [SerializeReference, ConfiguratorSelector] public List<ICondition> Conditions;

        public override bool IsMet()
        {
            if (Conditions == null || Conditions.Count == 0)
                return false;

            foreach (var cond in Conditions)
                if (cond?.IsMet() ?? false)
                    return true;

            return false;
        }

        public override IEnumerable<ICondition> GetConditions() => Conditions;
    }
}