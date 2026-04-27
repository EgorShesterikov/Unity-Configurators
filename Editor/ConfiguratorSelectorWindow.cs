#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Utility.Configurators
{
    [CustomPropertyDrawer(typeof(ConfiguratorSelectorAttribute))]
    public class ConfiguratorSelectorDrawer : PropertyDrawer
    {
        private static readonly Dictionary<Type, TypeEntry[]> _typeCache = new();
        private const string MANAGED_REF_PREFIX = "managedReference<";

        public class TypeEntry
        {
            public Type   Type;
            public string Name;
            public string FullPath;
            public string FullPathLower;
            public string Category;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.ManagedReference)
            {
                EditorGUI.HelpBox(position, "[ConfiguratorSelector] works only with [SerializeReference]", MessageType.Error);
                return;
            }

            bool hasValue = property.managedReferenceValue != null;
            var  line     = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            
            const float FOLDOUT_W = 4f;

            if (hasValue)
                property.isExpanded = EditorGUI.Foldout(
                    new Rect(line.x, line.y, FOLDOUT_W, line.height), property.isExpanded, GUIContent.none, true);
            
            var btnRect = new Rect(line.x + FOLDOUT_W, line.y, line.width - FOLDOUT_W, line.height);
            if (GUI.Button(btnRect, GetCurrentLabel(property), EditorStyles.popup))
                ConfiguratorSelectorWindow.Show(btnRect, property, GetEntries(property));

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
                var end   = property.GetEndProperty();
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
            var t   = property.managedReferenceValue.GetType();
            var cat = t.GetCustomAttribute<ConfiguratorCategoryAttribute>();
            return cat != null && ConfiguratorSelectorWindow.ShowCategoryInLabel
                ? $"{cat.Category}/{t.Name}"
                : t.Name;
        }

        internal static TypeEntry[] GetEntries(SerializedProperty property)
        {
            var baseType = GetBaseType(property);
            if (_typeCache.TryGetValue(baseType, out var cached)) return cached;

            var result = new List<TypeEntry>();
            var asms   = AppDomain.CurrentDomain.GetAssemblies();
            for (int ai = 0; ai < asms.Length; ai++)
            {
                var types = SafeGetTypes(asms[ai]);
                for (int ti = 0; ti < types.Length; ti++)
                {
                    var t = types[ti];
                    if (t.IsAbstract || t.IsInterface || !baseType.IsAssignableFrom(t)) continue;
                    var    cat      = t.GetCustomAttribute<ConfiguratorCategoryAttribute>();
                    string catS     = cat?.Category ?? "";
                    string fullPath = string.IsNullOrEmpty(catS) ? t.Name : $"{catS}/{t.Name}";
                    result.Add(new TypeEntry
                    {
                        Type          = t,
                        Name          = t.Name,
                        FullPath      = fullPath,
                        FullPathLower = fullPath.ToLowerInvariant(),
                        Category      = catS
                    });
                }
            }
            result.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.Ordinal));

            var arr = result.ToArray();
            _typeCache[baseType] = arr;
            return arr;
        }

        private static Type GetBaseType(SerializedProperty property)
        {
            var fieldTypename = property.managedReferenceFieldTypename;
            int spaceIdx      = fieldTypename.IndexOf(' ');
            if (spaceIdx > 0)
            {
                string asmName  = fieldTypename.Substring(0, spaceIdx);
                string typeName = fieldTypename.Substring(spaceIdx + 1);
                var    asms     = AppDomain.CurrentDomain.GetAssemblies();
                for (int i = 0; i < asms.Length; i++)
                {
                    if (asms[i].GetName().Name != asmName) continue;
                    var t = asms[i].GetType(typeName);
                    if (t != null) return t;
                    break;
                }
            }

            var refType = property.type;
            if (refType.StartsWith(MANAGED_REF_PREFIX))
            {
                var typeName = refType.Substring(MANAGED_REF_PREFIX.Length,
                    refType.Length - MANAGED_REF_PREFIX.Length - 1);
                var asms = AppDomain.CurrentDomain.GetAssemblies();
                for (int ai = 0; ai < asms.Length; ai++)
                {
                    var types = SafeGetTypes(asms[ai]);
                    for (int ti = 0; ti < types.Length; ti++)
                        if (types[ti].Name == typeName) return types[ti];
                }
            }

            return typeof(object);
        }

        private static void DrawChildren(Rect rect, SerializedProperty property)
        {
            var   child = property.Copy();
            var   end   = property.GetEndProperty();
            float y     = rect.y;
            if (!child.NextVisible(true)) return;
            while (!SerializedProperty.EqualContents(child, end))
            {
                float h = EditorGUI.GetPropertyHeight(child, true);
                EditorGUI.PropertyField(new Rect(rect.x, y, rect.width, h), child, true);
                y += h + EditorGUIUtility.standardVerticalSpacing;
                if (!child.NextVisible(false)) break;
            }
        }

        private static Type[] SafeGetTypes(Assembly a)
        {
            try { return a.GetTypes(); }
            catch (ReflectionTypeLoadException ex)
            {
                int loaderCount = ex.LoaderExceptions?.Length ?? 0;
                UnityEngine.Debug.LogWarning(
                    $"[ConfiguratorSelector] Couldn't fully load types from assembly '{a.GetName().Name}'. " +
                    $"Loader exceptions: {loaderCount}. Returning the partial set so the dropdown still works.");
                if (ex.Types == null) return Array.Empty<Type>();
                int kept = 0;
                for (int i = 0; i < ex.Types.Length; i++)
                    if (ex.Types[i] != null) kept++;
                if (kept == 0) return Array.Empty<Type>();
                var result = new Type[kept];
                int idx = 0;
                for (int i = 0; i < ex.Types.Length; i++)
                    if (ex.Types[i] != null) result[idx++] = ex.Types[i];
                return result;
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning(
                    $"[ConfiguratorSelector] Couldn't load types from assembly '{a.GetName().Name}': {ex.Message}");
                return Array.Empty<Type>();
            }
        }
    }

    public class ConfiguratorSelectorWindow : EditorWindow
    {
        private const float TOOLBAR_H = 22f;
        private const float ROW_H     = 18f;
        private const float INDENT    = 10f;
        private const float TEXT_X    = 18f;

        private const string PREF_W             = "ConfiguratorSelectorWindow_W";
        private const string PREF_H             = "ConfiguratorSelectorWindow_H";
        private const string PREF_SHOW_CATEGORY = "ConfiguratorSelectorWindow_ShowCategory";
        private const float  DEF_W  = 280f;
        private const float  DEF_H  = 350f;
        internal const float MIN_W  = 160f;
        internal const float MIN_H  = 120f;
        private const float  MAX_W  = 800f;
        private const float  MAX_H  = 800f;

        internal static float SavedW
        {
            get => EditorPrefs.GetFloat(PREF_W, DEF_W);
            set => EditorPrefs.SetFloat(PREF_W, Mathf.Clamp(value, MIN_W, MAX_W));
        }
        internal static float SavedH
        {
            get => EditorPrefs.GetFloat(PREF_H, DEF_H);
            set => EditorPrefs.SetFloat(PREF_H, Mathf.Clamp(value, MIN_H, MAX_H));
        }
        internal static bool ShowCategoryInLabel
        {
            get => EditorPrefs.GetBool(PREF_SHOW_CATEGORY, true);
            set => EditorPrefs.SetBool(PREF_SHOW_CATEGORY, value);
        }

        private SerializedProperty                     _property;
        private ConfiguratorSelectorDrawer.TypeEntry[] _entries;
        private string  _search        = "";
        private int     _hoveredIndex  = -1;
        private Vector2 _scroll;
        private bool    _doFocusSearch = true;
        private float   _contentH;
        private float   _contentW;
        private Rect    _anchorScreen;

        private readonly HashSet<string> _collapsed = new();
        private readonly List<RowItem>   _rows      = new();
        private readonly Dictionary<string, (List<ConfiguratorSelectorDrawer.TypeEntry> items, int depth)> _pendingDirectItems = new();

        private static readonly GUIContent s_tempContent = new();

        private struct RowItem
        {
            public bool   IsHeader;
            public bool   IsNone;
            public string Label;
            public string CategoryPath;
            public ConfiguratorSelectorDrawer.TypeEntry Entry;
            public int    Depth;
        }

        private static GUIStyle   s_toolbarSearch;
        private static GUIStyle   s_toolbarSearchCancel;
        private static GUIStyle   s_toolbarSearchCancelEmpty;
        private static GUIStyle   s_selectionRect;
        private static GUIStyle   s_pr_disabledLabel;
        private static GUIStyle   s_greyBorder;
        private static GUIContent s_gearContent;
        private static Color      s_borderColor;
        private static Color      s_separatorColor;
        private static Color      s_headerLineColor;
        private static Color      s_currentColor;
        private static bool       s_stylesReady;

        private static void EnsureStyles()
        {
            if (s_stylesReady) return;
            s_stylesReady = true;

            s_toolbarSearch = GUI.skin.FindStyle("ToolbarSearchTextField")
                           ?? GUI.skin.FindStyle("ToolbarSeachTextField")
                           ?? EditorStyles.toolbarSearchField;

            s_toolbarSearchCancel = GUI.skin.FindStyle("ToolbarSearchCancelButton")
                                 ?? GUI.skin.FindStyle("ToolbarSeachCancelButton")
                                 ?? GUIStyle.none;

            s_toolbarSearchCancelEmpty = GUI.skin.FindStyle("ToolbarSearchCancelButtonEmpty")
                                      ?? GUI.skin.FindStyle("ToolbarSeachCancelButtonEmpty")
                                      ?? GUIStyle.none;

            s_selectionRect    = GUI.skin.FindStyle("SelectionRect") ?? GUI.skin.box;
            s_pr_disabledLabel = GUI.skin.FindStyle("PR DisabledLabel") ?? EditorStyles.centeredGreyMiniLabel;
            s_greyBorder       = GUI.skin.FindStyle("grey_border") ?? GUIStyle.none;
            s_gearContent      = EditorGUIUtility.IconContent("d_Settings") ?? new GUIContent("=");

            s_borderColor     = new Color(0.10f, 0.10f, 0.10f, 1.00f);
            s_separatorColor  = new Color(0.00f, 0.00f, 0.00f, 0.30f);
            s_currentColor    = new Color(1.00f, 1.00f, 1.00f, 0.08f);
            s_headerLineColor = EditorGUIUtility.isProSkin
                ? new Color(1f, 1f, 1f, 0.10f)
                : new Color(0f, 0f, 0f, 0.15f);
        }

        public static void Show(Rect btnRect, SerializedProperty property, ConfiguratorSelectorDrawer.TypeEntry[] entries)
        {
            var win            = CreateInstance<ConfiguratorSelectorWindow>();
            win._property      = property;
            win._entries       = entries;
            win.RebuildRows();

            float w            = SavedW;
            float h            = SavedH;
            var   screen       = GUIUtility.GUIToScreenPoint(new Vector2(btnRect.x, btnRect.yMax));
            win._anchorScreen  = new Rect(screen.x, screen.y, w, 0f);
            win.wantsMouseMove = true;
            win.ShowAsDropDown(win._anchorScreen, new Vector2(w, h));
        }

        private void OnGUI()
        {
            if (_property == null) { Close(); return; }

            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)
            { Close(); Event.current.Use(); return; }

            EnsureStyles();
            DrawToolbar();
            DrawItems();

            if (Event.current.type == EventType.Repaint)
            {
                float w = position.width, h = position.height;
                s_greyBorder.Draw(new Rect(0, 0, w, h), false, false, false, false);
                EditorGUI.DrawRect(new Rect(0,     0,     w,  1), s_borderColor);
                EditorGUI.DrawRect(new Rect(0,     h - 1, w,  1), s_borderColor);
                EditorGUI.DrawRect(new Rect(0,     0,     1,  h), s_borderColor);
                EditorGUI.DrawRect(new Rect(w - 1, 0,     1,  h), s_borderColor);
            }

            if (Event.current.type == EventType.MouseMove)
                Repaint();
        }

        private void DrawToolbar()
        {
            GUI.Box(new Rect(0, 0, position.width, TOOLBAR_H), GUIContent.none, EditorStyles.toolbar);

            const float GEAR_W = 20f;
            float cancelW = Mathf.Max(s_toolbarSearchCancel.fixedWidth, 14f);
            var   gear    = new Rect(position.width - GEAR_W - 2f, 2f, GEAR_W, TOOLBAR_H - 4f);
            var   cancel  = new Rect(gear.x - cancelW, 2f, cancelW, TOOLBAR_H - 4f);
            var   field   = new Rect(4f, 2f, cancel.x - 6f, TOOLBAR_H - 4f);

            if (GUI.Button(gear, s_gearContent, EditorStyles.iconButton))
            {
                var screenPos = GUIUtility.GUIToScreenPoint(new Vector2(gear.x, gear.yMax));
                ConfiguratorSelectorSettingsPopup.Show(new Rect(screenPos, Vector2.zero), this);
            }

            GUI.SetNextControlName("ConfiguratorSearch");
            EditorGUI.BeginChangeCheck();
            string next = GUI.TextField(field, _search, s_toolbarSearch);
            if (EditorGUI.EndChangeCheck())
            {
                _search       = next;
                _hoveredIndex = -1;
                _scroll       = Vector2.zero;
                RebuildRows();
                Repaint();
            }

            bool empty = string.IsNullOrEmpty(_search);
            if (GUI.Button(cancel, GUIContent.none, empty ? s_toolbarSearchCancelEmpty : s_toolbarSearchCancel) && !empty)
            {
                _search       = "";
                _hoveredIndex = -1;
                _scroll       = Vector2.zero;
                RebuildRows();
                GUI.FocusControl("ConfiguratorSearch");
                Repaint();
            }

            if (_doFocusSearch && Event.current.type == EventType.Repaint)
            {
                _doFocusSearch = false;
                EditorApplication.delayCall += () =>
                {
                    if (this && focusedWindow == this) GUI.FocusControl("ConfiguratorSearch");
                };
            }
        }

        private void DrawItems()
        {
            float listY = TOOLBAR_H + 1f;
            const float BORDER = 1f;
            float listH = position.height - listY - BORDER;
            float viewW = position.width  - BORDER;

            float vertScrollW  = GUI.skin.verticalScrollbar.fixedWidth   + 2f;
            float horizScrollH = GUI.skin.horizontalScrollbar.fixedHeight + 2f;

            bool  needV  = _contentH > listH;
            float availW = viewW - (needV ? vertScrollW : 0f);
            bool  needH  = _contentW > availW;
            float availH = listH - (needH ? horizScrollH : 0f);
            needV  = _contentH > availH;
            availW = viewW - (needV ? vertScrollW : 0f);

            float contentW = Mathf.Max(_contentW, availW);
            float contentH = Mathf.Max(_contentH, availH);

            EditorGUI.DrawRect(new Rect(0, listY - 1, position.width, 1), s_separatorColor);

            _scroll = GUI.BeginScrollView(
                new Rect(0, listY, viewW, listH),
                _scroll,
                new Rect(0, listY, contentW, contentH),
                needH ? GUI.skin.horizontalScrollbar : GUIStyle.none,
                needV ? GUI.skin.verticalScrollbar   : GUIStyle.none);

            int   clicked     = -1;
            float y           = listY;
            var   mp          = Event.current.mousePosition;
            Type  currentType = _property.managedReferenceValue?.GetType();

            for (int i = 0; i < _rows.Count; i++)
            {
                var row = _rows[i];
                DrawRow(ref row, i, new Rect(0, y, contentW, ROW_H), mp, currentType, ref clicked);
                y += ROW_H;
            }

            GUI.EndScrollView();

            if (clicked >= 0) SelectRow(_rows[clicked]);
            HandleKeyboard();
        }

        private void DrawRow(ref RowItem row, int index, Rect r, Vector2 mp, Type currentType, ref int clicked)
        {
            if (row.IsHeader)
            {
                EditorGUI.DrawRect(new Rect(r.x, r.y, r.width, 1f), s_headerLineColor);

                float ix        = r.x + 6f + row.Depth * INDENT;
                bool  collapsed = _collapsed.Contains(row.CategoryPath);
                EditorGUI.LabelField(new Rect(ix,       r.y + 1f, 14f,                r.height - 1f), collapsed ? "►" : "▼", s_pr_disabledLabel);
                EditorGUI.LabelField(new Rect(ix + 14f, r.y + 1f, r.width - ix - 14f, r.height - 1f), row.Label, s_pr_disabledLabel);

                if (r.Contains(mp) && Event.current.type == EventType.MouseDown)
                {
                    if (collapsed) _collapsed.Remove(row.CategoryPath);
                    else           _collapsed.Add(row.CategoryPath);
                    RebuildRows();
                    Event.current.Use();
                    Repaint();
                }
                return;
            }

            bool inR     = r.Contains(mp);
            bool current = !row.IsNone && currentType != null && currentType == row.Entry?.Type;

            if (inR) _hoveredIndex = index;

            if (Event.current.type == EventType.Repaint)
            {
                if (inR)          s_selectionRect.Draw(r, false, false, true, true);
                else if (current) EditorGUI.DrawRect(r, s_currentColor);
            }

            EditorGUI.LabelField(
                new Rect(r.x + TEXT_X + row.Depth * INDENT, r.y, r.width - TEXT_X - row.Depth * INDENT - 2f, r.height),
                row.Label, (inR || current) ? EditorStyles.whiteLabel : EditorStyles.label);

            if (inR && Event.current.type == EventType.MouseDown)
            { clicked = index; Event.current.Use(); }
        }

        internal void ApplySettings(float w, float h)
        {
            SavedW = w;
            SavedH = h;
            ResizeWindow(w, h);
        }

        internal void ResetSettings()
        {
            SavedW = DEF_W;
            SavedH = DEF_H;
            ResizeWindow(DEF_W, DEF_H);
        }

        private void ResizeWindow(float w, float h)
        {
            var anchor    = new Rect(_anchorScreen.x, _anchorScreen.y, w, 0f);
            var property  = _property;
            var entries   = _entries;
            var search    = _search;
            var collapsed = new HashSet<string>(_collapsed);

            EditorApplication.delayCall += () =>
            {
                Close();
                var win           = CreateInstance<ConfiguratorSelectorWindow>();
                win._property     = property;
                win._entries      = entries;
                win._search       = search;
                win._anchorScreen = anchor;
                win._collapsed.UnionWith(collapsed);
                win.wantsMouseMove = true;
                win.RebuildRows();
                win.ShowAsDropDown(anchor, new Vector2(w, h));
            };
        }

        private void HandleKeyboard()
        {
            if (Event.current.type != EventType.KeyDown) return;

            int count    = _rows.Count;
            int cur      = -1;
            int selCount = 0;

            for (int i = 0; i < count; i++)
            {
                if (_rows[i].IsHeader) continue;
                if (i == _hoveredIndex) cur = selCount;
                selCount++;
            }

            if (selCount == 0) return;

            int nextSel;
            switch (Event.current.keyCode)
            {
                case KeyCode.DownArrow:   nextSel = Mathf.Clamp(cur + 1, 0, selCount - 1); break;
                case KeyCode.UpArrow:     nextSel = cur < 0 ? selCount - 1 : Mathf.Clamp(cur - 1, 0, selCount - 1); break;
                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                    if (_hoveredIndex >= 0 && _hoveredIndex < count) SelectRow(_rows[_hoveredIndex]);
                    Event.current.Use();
                    return;
                default: return;
            }

            int found = 0;
            for (int i = 0; i < count; i++)
            {
                if (_rows[i].IsHeader) continue;
                if (found == nextSel) { _hoveredIndex = i; break; }
                found++;
            }

            EnsureVisible(_hoveredIndex);
            Event.current.Use();
            Repaint();
        }

        private void EnsureVisible(int index)
        {
            float y     = index * ROW_H;
            float listH = position.height - TOOLBAR_H - 1f;
            _scroll.y   = Mathf.Clamp(_scroll.y, y - listH + ROW_H, y);
        }

        private void SelectRow(RowItem row)
        {
            if (row.IsHeader) return;
            _property.serializedObject.Update();
            _property.managedReferenceValue = row.IsNone ? null : Activator.CreateInstance(row.Entry.Type);
            _property.isExpanded            = !row.IsNone;
            _property.serializedObject.ApplyModifiedProperties();
            Close();
        }

        private void RebuildRows()
        {
            _rows.Clear();
            _rows.Add(new RowItem { IsNone = true, Label = "None" });

            if (_entries == null) { _contentH = ROW_H; _contentW = 0f; return; }

            if (!string.IsNullOrWhiteSpace(_search))
            {
                string q = _search.ToLowerInvariant();
                foreach (var e in _entries)
                    if (e.FullPathLower.Contains(q))
                        _rows.Add(new RowItem { Entry = e, Label = e.Name });
            }
            else
            {
                var byCat = new SortedDictionary<string, List<ConfiguratorSelectorDrawer.TypeEntry>>(StringComparer.Ordinal);
                var noCat = new List<ConfiguratorSelectorDrawer.TypeEntry>();

                foreach (var e in _entries)
                {
                    if (string.IsNullOrEmpty(e.Category)) noCat.Add(e);
                    else
                    {
                        if (!byCat.TryGetValue(e.Category, out var list))
                            byCat[e.Category] = list = new List<ConfiguratorSelectorDrawer.TypeEntry>();
                        list.Add(e);
                    }
                }

                foreach (var (cat, list) in byCat)
                {
                    string[] segs            = cat.Split('/');
                    bool     parentCollapsed = false;

                    for (int d = 0; d < segs.Length; d++)
                    {
                        if (parentCollapsed) break;
                        string segPath = d == 0 ? segs[0] : cat.Substring(0, IndexOfNthSlash(cat, d + 1));

                        bool dup = _rows.Exists(r => r.IsHeader && r.CategoryPath == segPath);
                        if (!dup)
                            _rows.Add(new RowItem { IsHeader = true, Label = segs[d], Depth = d, CategoryPath = segPath });

                        if (_collapsed.Contains(segPath)) parentCollapsed = true;
                    }

                    if (!parentCollapsed && !_collapsed.Contains(cat))
                    {
                        bool hasSubcategories = false;
                        foreach (var otherCat in byCat.Keys)
                            if (otherCat != cat && otherCat.StartsWith(cat + "/"))
                            { hasSubcategories = true; break; }

                        if (hasSubcategories)
                        {
                            _pendingDirectItems[cat] = (list, segs.Length);
                        }
                        else
                        {
                            foreach (var e in list)
                                _rows.Add(new RowItem { Entry = e, Label = e.Name, Depth = segs.Length });

                            string parentCat = segs.Length > 1 ? cat.Substring(0, cat.LastIndexOf('/')) : null;
                            if (parentCat != null && _pendingDirectItems.TryGetValue(parentCat, out var pending))
                            {
                                foreach (var e in pending.items)
                                    _rows.Add(new RowItem { Entry = e, Label = e.Name, Depth = pending.depth });
                                _pendingDirectItems.Remove(parentCat);
                            }
                        }
                    }
                }

                foreach (var (_, pending) in _pendingDirectItems)
                    foreach (var e in pending.items)
                        _rows.Add(new RowItem { Entry = e, Label = e.Name, Depth = pending.depth });
                _pendingDirectItems.Clear();

                foreach (var e in noCat)
                    _rows.Add(new RowItem { Entry = e, Label = e.Name, Depth = 0 });
            }

            _contentH = 0f;
            _contentW = 0f;
            for (int i = 0; i < _rows.Count; i++)
            {
                _contentH += ROW_H;
                s_tempContent.text = _rows[i].Label;
                float textX = _rows[i].IsHeader ? 6f + _rows[i].Depth * INDENT + 14f : TEXT_X + _rows[i].Depth * INDENT;
                float w     = textX + EditorStyles.label.CalcSize(s_tempContent).x + 4f;
                if (w > _contentW) _contentW = w;
            }
        }

        private static int IndexOfNthSlash(string s, int n)
        {
            int count = 0;
            for (int i = 0; i < s.Length; i++)
                if (s[i] == '/' && ++count == n) return i;
            return s.Length;
        }
    }

    internal class ConfiguratorSelectorSettingsPopup : EditorWindow
    {
        private ConfiguratorSelectorWindow _owner;
        private float _w;
        private float _h;

        private static readonly Color s_borderColor = new Color(0.1f, 0.1f, 0.1f, 1f);

        internal static void Show(Rect screenPos, ConfiguratorSelectorWindow owner)
        {
            var win    = CreateInstance<ConfiguratorSelectorSettingsPopup>();
            win._owner = owner;
            win._w     = ConfiguratorSelectorWindow.SavedW;
            win._h     = ConfiguratorSelectorWindow.SavedH;
            win.ShowAsDropDown(screenPos, new Vector2(180f, 116f));
        }

        private void OnGUI()
        {
            const int PAD = 8;
            GUILayout.BeginArea(new Rect(PAD, PAD, position.width - PAD * 2, position.height - PAD * 2));

            EditorGUIUtility.labelWidth = 52f;
            _w = Mathf.Max(ConfiguratorSelectorWindow.MIN_W, EditorGUILayout.FloatField("Width",  _w));
            _h = Mathf.Max(ConfiguratorSelectorWindow.MIN_H, EditorGUILayout.FloatField("Height", _h));

            GUILayout.Space(4f);
            EditorGUIUtility.labelWidth = 120f;
            ConfiguratorSelectorWindow.ShowCategoryInLabel = EditorGUILayout.Toggle(
                "Category in Label", ConfiguratorSelectorWindow.ShowCategoryInLabel);

            GUILayout.Space(6f);
            EditorGUIUtility.labelWidth = 52f;
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Apply", EditorStyles.miniButton)) { _owner?.ApplySettings(_w, _h); Close(); }
            if (GUILayout.Button("Reset", EditorStyles.miniButton)) { _owner?.ResetSettings();        Close(); }
            EditorGUILayout.EndHorizontal();

            GUILayout.EndArea();

            if (Event.current.type == EventType.Repaint)
            {
                float w = position.width, h = position.height;
                EditorGUI.DrawRect(new Rect(0,     0,     w, 1), s_borderColor);
                EditorGUI.DrawRect(new Rect(0,     h - 1, w, 1), s_borderColor);
                EditorGUI.DrawRect(new Rect(0,     0,     1, h), s_borderColor);
                EditorGUI.DrawRect(new Rect(w - 1, 0,     1, h), s_borderColor);
            }
        }
    }
}
#endif