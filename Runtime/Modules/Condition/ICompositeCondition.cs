using System.Collections.Generic;

namespace Utility.Configurators
{
    public interface ICompositeCondition : ICondition
    {
        IEnumerable<ICondition> GetConditions();
    }
}