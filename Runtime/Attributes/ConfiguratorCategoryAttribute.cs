using System;

namespace Utility.Configurators
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public class ConfiguratorCategoryAttribute : Attribute
    {
        public string Category { get; }
        public ConfiguratorCategoryAttribute(string category) => Category = category;
    }
}