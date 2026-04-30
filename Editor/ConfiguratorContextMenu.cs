#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Utility.Configurators
{
    // Right-click menu for [ConfiguratorSelector] fields.
    //   ShowDirectMenu — drawer caught the click on its selector button.
    //   AppendContextMenuItems — Unity's contextualPropertyMenu (foldout / list / wrapper).
    internal static class ConfiguratorContextMenu
    {
        [InitializeOnLoadMethod]
        private static void Register()
        {
            EditorApplication.contextualPropertyMenu -= AppendContextMenuItems;
            EditorApplication.contextualPropertyMenu += AppendContextMenuItems;
        }

        public static void ShowDirectMenu(SerializedProperty property)
        {
            var prop = property.Copy();

            bool hasValue = prop.managedReferenceValue != null;
            bool clipFits = ConfiguratorClipboard.HasValue
                && ConfiguratorPropertyUtils.IsAssignable(prop, ConfiguratorClipboard.ValueType);
            bool isInArray = ConfiguratorPropertyUtils.TryGetParentArray(prop, out _, out _);

            var menu = new GenericMenu();
            AddItem(menu, "Copy", hasValue, () => CopyValue(prop));
            AddItem(menu, "Paste", clipFits, () => PasteValue(prop));
            AddItem(menu, "Duplicate", hasValue && isInArray, () => DuplicateValue(prop));

            if (hasValue)
            {
                menu.AddSeparator("");
                menu.AddItem(new GUIContent("Set to None"), false, () => SetNone(prop));
            }

            menu.ShowAsContext();
        }

        private static void AppendContextMenuItems(GenericMenu menu, SerializedProperty property)
        {
            if (property == null) return;

            if (property.propertyType == SerializedPropertyType.ManagedReference)
            {
                if (!ConfiguratorPropertyUtils.HasConfiguratorSelectorAttribute(property)) return;

                var prop = property.Copy();
                bool hasValue = prop.managedReferenceValue != null;
                bool clipFits = ConfiguratorClipboard.HasValue
                    && ConfiguratorPropertyUtils.IsAssignable(prop, ConfiguratorClipboard.ValueType);
                bool isInArray = ConfiguratorPropertyUtils.TryGetParentArray(prop, out _, out _);

                menu.AddSeparator("");
                AddItem(menu, "Configurator/Copy", hasValue, () => CopyValue(prop));
                AddItem(menu, "Configurator/Paste", clipFits, () => PasteValue(prop));
                AddItem(menu, "Configurator/Duplicate", hasValue && isInArray, () => DuplicateValue(prop));
                if (hasValue)
                    menu.AddItem(new GUIContent("Configurator/Set to None"), false, () => SetNone(prop));
                return;
            }

            if (property.isArray
                && ConfiguratorPropertyUtils.IsManagedReferenceArray(property)
                && ConfiguratorPropertyUtils.HasConfiguratorSelectorAttribute(property))
            {
                AppendListMenuItems(menu, property.Copy());
                return;
            }

            // Generic processor wrapper — forward to its single [ConfiguratorSelector] list.
            if (property.propertyType == SerializedPropertyType.Generic
                && ConfiguratorPropertyUtils.TryFindConfiguratorListChild(property, out var listChild))
            {
                AppendListMenuItems(menu, listChild);
            }
        }

        private static void AppendListMenuItems(GenericMenu menu, SerializedProperty arrayProp)
        {
            bool hasItems = arrayProp.arraySize > 0;
            var listClip = ConfiguratorClipboard.List;
            bool hasClip = listClip != null && listClip.Entries.Count > 0;
            bool clipFits = hasClip
                && ConfiguratorPropertyUtils.IsListPasteCompatible(arrayProp, listClip.ElementBaseType);

            menu.AddSeparator("");
            AddItem(menu, "Configurator/Copy List", hasItems, () => CopyList(arrayProp));
            AddItem(menu, "Configurator/Paste List/Replace", clipFits, () => PasteList(arrayProp, replace: true));
            AddItem(menu, "Configurator/Paste List/Append", clipFits, () => PasteList(arrayProp, replace: false));
            if (hasItems)
                menu.AddItem(new GUIContent("Configurator/Clear List"), false, () => ClearList(arrayProp));
        }

        private static void AddItem(GenericMenu menu, string label, bool enabled, GenericMenu.MenuFunction action)
        {
            var content = new GUIContent(label);
            if (enabled) menu.AddItem(content, false, action);
            else menu.AddDisabledItem(content);
        }

        private static void CopyValue(SerializedProperty property)
        {
            ConfiguratorClipboard.StoreValue(property.managedReferenceValue);
        }

        private static void PasteValue(SerializedProperty property)
        {
            if (!ConfiguratorClipboard.HasValue) return;
            if (!ConfiguratorPropertyUtils.IsAssignable(property, ConfiguratorClipboard.ValueType))
            {
                Debug.LogWarning($"[ConfiguratorSelector] Cannot paste '{ConfiguratorClipboard.ValueType.Name}' " +
                                 $"into '{ConfiguratorPropertyUtils.GetBaseType(property).Name}'.");
                return;
            }

            var clone = ConfiguratorClipboard.Deserialize(ConfiguratorClipboard.Json);
            if (clone == null) return;

            var so = property.serializedObject;
            so.Update();
            property.managedReferenceValue = clone;
            property.isExpanded = true;
            so.ApplyModifiedProperties();
        }

        private static void DuplicateValue(SerializedProperty property)
        {
            if (!ConfiguratorPropertyUtils.TryGetParentArray(property, out var array, out var index)) return;

            var value = property.managedReferenceValue;
            if (value == null) return;

            string json = ConfiguratorClipboard.Serialize(value);
            var copy = ConfiguratorClipboard.Deserialize(json);
            if (copy == null) return;

            var so = property.serializedObject;
            so.Update();

            array.InsertArrayElementAtIndex(index + 1);
            // Insert leaves managed references null — assign the deep clone explicitly.
            var inserted = array.GetArrayElementAtIndex(index + 1);
            inserted.managedReferenceValue = copy;
            inserted.isExpanded = property.isExpanded;

            so.ApplyModifiedProperties();
        }

        private static void SetNone(SerializedProperty property)
        {
            var so = property.serializedObject;
            so.Update();
            property.managedReferenceValue = null;
            property.isExpanded = false;
            so.ApplyModifiedProperties();
        }

        private static void CopyList(SerializedProperty arrayProp)
        {
            var clip = new ConfiguratorClipboard.ListClipboardData
            {
                ElementBaseType = ConfiguratorPropertyUtils.GetArrayElementBaseType(arrayProp)
            };

            for (int i = 0; i < arrayProp.arraySize; i++)
            {
                var elem = arrayProp.GetArrayElementAtIndex(i);
                var value = elem.managedReferenceValue;
                if (value == null) continue;
                clip.Entries.Add(ConfiguratorClipboard.Serialize(value));
            }

            ConfiguratorClipboard.StoreList(clip);
        }

        private static void PasteList(SerializedProperty arrayProp, bool replace)
        {
            var clip = ConfiguratorClipboard.List;
            if (clip == null || clip.Entries.Count == 0) return;
            if (!ConfiguratorPropertyUtils.IsListPasteCompatible(arrayProp, clip.ElementBaseType))
            {
                Debug.LogWarning($"[ConfiguratorSelector] Cannot paste list of '{clip.ElementBaseType?.Name}' " +
                                 $"into '{ConfiguratorPropertyUtils.GetArrayElementBaseType(arrayProp)?.Name}'.");
                return;
            }

            var so = arrayProp.serializedObject;
            so.Update();

            if (replace) arrayProp.ClearArray();

            foreach (var json in clip.Entries)
            {
                var clone = ConfiguratorClipboard.Deserialize(json);
                if (clone == null) continue;

                arrayProp.InsertArrayElementAtIndex(arrayProp.arraySize);
                var inserted = arrayProp.GetArrayElementAtIndex(arrayProp.arraySize - 1);
                inserted.managedReferenceValue = clone;
            }

            so.ApplyModifiedProperties();
        }

        private static void ClearList(SerializedProperty arrayProp)
        {
            var so = arrayProp.serializedObject;
            so.Update();
            arrayProp.ClearArray();
            so.ApplyModifiedProperties();
        }
    }
}
#endif
