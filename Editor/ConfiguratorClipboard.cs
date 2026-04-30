#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Utility.Configurators
{
    // Clipboards + (de)serialization wrapper for managed reference graphs.
    internal static class ConfiguratorClipboard
    {
        public static string Json { get; private set; }
        public static Type ValueType { get; private set; }

        public static bool HasValue => !string.IsNullOrEmpty(Json) && ValueType != null;

        public static void StoreValue(object value)
        {
            if (value == null) { Json = null; ValueType = null; return; }
            Json = Serialize(value);
            ValueType = value.GetType();
        }

        public sealed class ListClipboardData
        {
            public readonly List<string> Entries = new();
            public Type ElementBaseType;
        }

        public static ListClipboardData List { get; private set; }

        public static void StoreList(ListClipboardData list) => List = list;

        // EditorJsonUtility preserves [SerializeReference] type info via "rid" / "$type".
        private sealed class Wrapper : ScriptableObject
        {
            [SerializeReference] public object Value;
        }

        // Reused instance — avoids CreateInstance/DestroyImmediate per Copy/Paste call.
        private static Wrapper _wrapper;

        private static Wrapper GetWrapper()
        {
            if (_wrapper != null) return _wrapper;
            _wrapper = ScriptableObject.CreateInstance<Wrapper>();
            _wrapper.hideFlags = HideFlags.HideAndDontSave;
            return _wrapper;
        }

        public static string Serialize(object value)
        {
            if (value == null) return null;
            var w = GetWrapper();
            w.Value = value;
            try { return EditorJsonUtility.ToJson(w); }
            finally { w.Value = null; }
        }

        public static object Deserialize(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            var w = GetWrapper();
            w.Value = null;
            EditorJsonUtility.FromJsonOverwrite(json, w);
            var result = w.Value;
            w.Value = null;
            return result;
        }
    }
}
#endif
