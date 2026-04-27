using System;

namespace Utility.Configurators
{
    public interface ICondition
    {
        bool IsMet();
        void AddListener(Action onChanged);
        void RemoveListener(Action onChanged);
    }
}