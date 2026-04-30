#if UNITY_EDITOR
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Utility.Configurators
{
    [CustomPropertyDrawer(typeof(ConfiguratorSelectorAttribute))]
    public class ConfiguratorSelectorDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.ManagedReference)
            {
                EditorGUI.HelpBox(position, "[ConfiguratorSelector] works only with [SerializeReference]", MessageType.Error);
                return;
            }

            bool hasValue = property.managedReferenceValue != null;
            var line = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);

            const float FoldoutW = 4f;

            // Catch right-click before GUI.Button consumes mouse events.
            var ev = Event.current;
            if (line.Contains(ev.mousePosition) &&
                (ev.type == EventType.ContextClick ||
                 (ev.type == EventType.MouseDown && ev.button == 1)))
            {
                ConfiguratorContextMenu.ShowDirectMenu(property);
                ev.Use();
                return;
            }

            if (hasValue)
                property.isExpanded = EditorGUI.Foldout(
                    new Rect(line.x, line.y, FoldoutW, line.height), property.isExpanded, GUIContent.none, true);

            var btnRect = new Rect(line.x + FoldoutW, line.y, line.width - FoldoutW, line.height);

            if (GUI.Button(btnRect, GetCurrentLabel(property), EditorStyles.popup))
                ConfiguratorSelectorWindow.Show(btnRect, property, ConfiguratorPropertyUtils.GetEntries(property));

            if (hasValue && property.isExpanded)
            {
                EditorGUI.indentLevel++;
                float yOff = EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
                DrawChildren(new Rect(position.x, position.y + yOff, position.width, position.height - yOff), property);
                EditorGUI.indentLevel--;
            }
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.ManagedReference)
                return EditorGUIUtility.singleLineHeight;

            float h = EditorGUIUtility.singleLineHeight;

            if (property.managedReferenceValue != null && property.isExpanded)
            {
                var child = property.Copy();
                var end = property.GetEndProperty();
                if (child.NextVisible(true))
                    while (!SerializedProperty.EqualContents(child, end))
                    {
                        h += EditorGUI.GetPropertyHeight(child, true) + EditorGUIUtility.standardVerticalSpacing;
                        if (!child.NextVisible(false)) break;
                    }
            }

            return h;
        }

        private static string GetCurrentLabel(SerializedProperty property)
        {
            if (property.managedReferenceValue == null) return "None";
            var t = property.managedReferenceValue.GetType();
            var cat = t.GetCustomAttribute<ConfiguratorCategoryAttribute>();
            return cat != null && ConfiguratorSelectorWindow.ShowCategoryInLabel
                ? $"{cat.Category}/{t.Name}"
                : t.Name;
        }

        private static void DrawChildren(Rect rect, SerializedProperty property)
        {
            var child = property.Copy();
            var end = property.GetEndProperty();
            float y = rect.y;
            if (!child.NextVisible(true)) return;
            while (!SerializedProperty.EqualContents(child, end))
            {
                float h = EditorGUI.GetPropertyHeight(child, true);
                EditorGUI.PropertyField(new Rect(rect.x, y, rect.width, h), child, true);
                y += h + EditorGUIUtility.standardVerticalSpacing;
                if (!child.NextVisible(false)) break;
            }
        }
    }
}
#endif
