using System;

namespace Utility.Configurators
{
    [Serializable]
    public abstract class Instruction : IInstruction
    {
        public abstract void Apply();
    }
}
