using System;

namespace Utility.Configurators
{
    public interface IConditionHandler
    {
        void SetData(object data);
        bool IsMet();
        void AddListener(Action onChanged);
        void RemoveListener(Action onChanged);
    }
}