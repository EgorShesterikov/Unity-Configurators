using System;
using System.Collections.Generic;
using UnityEngine;

namespace Utility.Configurators
{
    [Serializable]
    [ConfiguratorCategory("Composite")]
    public class Not : CompositeCondition
    {
        [SerializeReference, ConfiguratorSelector] public ICondition Condition;

        public override bool IsMet() => !Condition?.IsMet() ?? true;

        public override IEnumerable<ICondition> GetConditions() { yield return Condition; }
    }
}