using System;
using System.Collections.Generic;
using UnityEngine;

namespace Utility.Configurators
{
    [Serializable]
    public class InstructionProcessor
    {
        [SerializeReference, ConfiguratorSelector] public List<IInstruction> Instructions;

        public void Apply()
        {
            if (Instructions == null || Instructions.Count == 0)
                return;

            foreach (var instruction in Instructions)
            {
                if (instruction == null)
                    continue;

                try { instruction.Apply(); }
                catch (Exception e) { Debug.LogException(e); }
            }
        }
    }
}
