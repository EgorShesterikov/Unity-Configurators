using System;
using UnityEngine;

namespace Utility.Configurators
{
    [AttributeUsage(AttributeTargets.Field)]
    public class ConfiguratorSelectorAttribute : PropertyAttribute { }
}