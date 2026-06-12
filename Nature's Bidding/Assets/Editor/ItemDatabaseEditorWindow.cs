#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class ItemDatabaseEditorWindow : EditorWindow
{
    private enum SortMode { None, Type, Id, Name }
    private enum SortDirection { Ascending, Descending }

    private List<StatusEffectorSO> _effectors = new();
    private Vector2 _scrollPos;
    private string _searchFilter = "";
    private bool _isDirty = false;
    private SortMode _sortMode = SortMode.Type;
    private SortDirection _sortDirection = SortDirection.Ascending;

    // Column widths
    private const float COL_TYPE = 120f;
    private const float COL_ASSET = 150f;
    private const float COL_ID = 200f;
    private const float COL_NAME = 200f;
    private const float COL_DESC = 300f;
    private const float COL_STATUS = 60f;

    [MenuItem("Game/Item Database")]
    public static void Open()
    {
        var window = GetWindow<ItemDatabaseEditorWindow>("Item Database");
        window.minSize = new Vector2(1000, 400);
        window.Refresh();
    }

    private void Refresh()
    {
        _effectors = AssetDatabase.FindAssets($"t:{typeof(StatusEffectorSO).Name}")
            .Select(guid => AssetDatabase.LoadAssetAtPath<StatusEffectorSO>(
                AssetDatabase.GUIDToAssetPath(guid)))
            .Where(so => so != null)
            .ToList();
    }

    private string GetTypeName(StatusEffectorSO so)
    {
        if (so is CompositeStatusEffectorSO) return "Composite";
        if (so is ComplexNumericalStatusEffectorSO) return "Complex";
        if (so is BasicNumericalStatusEffectorSO) return "Basic";
        return "Unknown";
    }

    private int GetTypeOrder(StatusEffectorSO so)
    {
        if (so is CompositeStatusEffectorSO) return 0;
        if (so is ComplexNumericalStatusEffectorSO) return 1;
        if (so is BasicNumericalStatusEffectorSO) return 2;
        return 3;
    }

    private List<StatusEffectorSO> GetSorted(List<StatusEffectorSO> source)
    {
        IOrderedEnumerable<StatusEffectorSO> sorted = _sortMode switch
        {
            SortMode.Type => _sortDirection == SortDirection.Ascending
                ? source.OrderBy(GetTypeOrder).ThenBy(e => e.Id)
                : source.OrderByDescending(GetTypeOrder).ThenBy(e => e.Id),

            SortMode.Id => _sortDirection == SortDirection.Ascending
                ? source.OrderBy(e => e.Id)
                : source.OrderByDescending(e => e.Id),

            SortMode.Name => _sortDirection == SortDirection.Ascending
                ? source.OrderBy(e => e.Title)
                : source.OrderByDescending(e => e.Title),

            _ => source.OrderBy(e => e.name)
        };

        return sorted.ToList();
    }

    private void OnGUI()
    {
        DrawToolbar();
        DrawHeader();
        DrawRows();
        DrawFooter();
    }

    private void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

        if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(70)))
            Refresh();

        if (GUILayout.Button("Save All", EditorStyles.toolbarButton, GUILayout.Width(70)))
            SaveAll();

        GUILayout.Space(10);
        EditorGUILayout.LabelField("Search:", GUILayout.Width(50));
        _searchFilter = EditorGUILayout.TextField(_searchFilter, EditorStyles.toolbarSearchField);

        if (GUILayout.Button("✕", EditorStyles.toolbarButton, GUILayout.Width(20)))
            _searchFilter = "";

        EditorGUILayout.EndHorizontal();
    }

    private void DrawSortableHeader(string label, SortMode mode, float width)
    {
        string display = label;
        if (_sortMode == mode)
            display += _sortDirection == SortDirection.Ascending ? " ▲" : " ▼";

        if (GUILayout.Button(display, EditorStyles.toolbarButton, GUILayout.Width(width)))
        {
            if (_sortMode == mode)
                _sortDirection = _sortDirection == SortDirection.Ascending
                    ? SortDirection.Descending
                    : SortDirection.Ascending;
            else
            {
                _sortMode = mode;
                _sortDirection = SortDirection.Ascending;
            }
        }
    }

    private void DrawHeader()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

        DrawSortableHeader("Type", SortMode.Type, COL_TYPE);
        GUILayout.Label("Asset", EditorStyles.toolbarButton, GUILayout.Width(COL_ASSET));
        DrawSortableHeader("ID", SortMode.Id, COL_ID);
        DrawSortableHeader("Title", SortMode.Name, COL_NAME);
        GUILayout.Label("Description", EditorStyles.toolbarButton, GUILayout.Width(COL_DESC));
        GUILayout.Label("", EditorStyles.toolbarButton, GUILayout.Width(COL_STATUS));

        EditorGUILayout.EndHorizontal();
    }

    private void DrawRows()
    {
        var filtered = string.IsNullOrEmpty(_searchFilter)
            ? _effectors
            : _effectors.Where(e =>
                (e.Id?.Contains(_searchFilter, System.StringComparison.OrdinalIgnoreCase) ?? false) ||
                (e.Title?.Contains(_searchFilter, System.StringComparison.OrdinalIgnoreCase) ?? false) ||
                e.name.Contains(_searchFilter, System.StringComparison.OrdinalIgnoreCase) ||
                GetTypeName(e).Contains(_searchFilter, System.StringComparison.OrdinalIgnoreCase)
            ).ToList();

        var sorted = GetSorted(filtered);

        var idCounts = sorted
            .GroupBy(e => e.Id)
            .ToDictionary(g => g.Key, g => g.Count());

        _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

        // Draw type group headers when sorting by type
        string lastType = null;

        for (int i = 0; i < sorted.Count; i++)
        {
            var effector = sorted[i];
            string typeName = GetTypeName(effector);

            // Group header when sorted by type
            if (_sortMode == SortMode.Type && typeName != lastType)
            {
                lastType = typeName;
                var groupRect = EditorGUILayout.BeginHorizontal();
                EditorGUI.DrawRect(groupRect, new Color(0.15f, 0.15f, 0.3f));
                GUILayout.Label(typeName, EditorStyles.boldLabel);
                EditorGUILayout.EndHorizontal();
            }

            var serialized = new SerializedObject(effector);

            bool hasDuplicateId = !string.IsNullOrEmpty(effector.Id) &&
                                   idCounts.TryGetValue(effector.Id, out int count) &&
                                   count > 1;
            bool hasMissingId = string.IsNullOrEmpty(effector.Id);

            var rowColor = i % 2 == 0
                ? new Color(0.18f, 0.18f, 0.18f)
                : new Color(0.22f, 0.22f, 0.22f);

            if (hasDuplicateId || hasMissingId)
                rowColor = new Color(0.4f, 0.1f, 0.1f);

            var rect = EditorGUILayout.BeginHorizontal();
            EditorGUI.DrawRect(rect, rowColor);

            // Type badge
            var typeColor = typeName switch
            {
                "Composite" => new Color(0.6f, 0.3f, 0.8f),
                "Complex" => new Color(0.3f, 0.6f, 0.8f),
                "Basic" => new Color(0.3f, 0.8f, 0.4f),
                _ => Color.gray
            };

            var badgeStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                normal = { textColor = typeColor },
                fontStyle = FontStyle.Bold
            };
            GUILayout.Label(typeName, badgeStyle, GUILayout.Width(COL_TYPE));

            // Asset name — click to ping
            if (GUILayout.Button(effector.name, EditorStyles.label, GUILayout.Width(COL_ASSET)))
            {
                EditorGUIUtility.PingObject(effector);
                Selection.activeObject = effector;
            }

            // ID
            EditorGUI.BeginChangeCheck();
            var idProp = serialized.FindProperty("_id");
            if (idProp != null)
                EditorGUILayout.PropertyField(idProp, GUIContent.none, GUILayout.Width(COL_ID));
            if (EditorGUI.EndChangeCheck())
            {
                serialized.ApplyModifiedProperties();
                _isDirty = true;
            }

            // Name
            EditorGUI.BeginChangeCheck();
            var nameProp = serialized.FindProperty("Title");
            if (nameProp != null)
                EditorGUILayout.PropertyField(nameProp, GUIContent.none, GUILayout.Width(COL_NAME));
            else
                GUILayout.Space(COL_NAME);
            if (EditorGUI.EndChangeCheck())
            {
                serialized.ApplyModifiedProperties();
                _isDirty = true;
            }

            // Stat field
            EditorGUI.BeginChangeCheck();
            var statProp = serialized.FindProperty("Stat");
            if (statProp != null)
            {
                EditorGUILayout.PropertyField(statProp, GUIContent.none, GUILayout.Width(200));

                if (EditorGUI.EndChangeCheck())
                {
                    serialized.ApplyModifiedProperties();
                    _isDirty = true;
                }
            }
            else
            {
                GUILayout.Space(200);
            }
            
            // Value field
            EditorGUI.BeginChangeCheck();
            var valueProp = serialized.FindProperty("Value");
            if (valueProp != null)
            {
                EditorGUILayout.PropertyField(valueProp, GUIContent.none, GUILayout.Width(200));

                if (EditorGUI.EndChangeCheck())
                {
                    serialized.ApplyModifiedProperties();
                    _isDirty = true;
                }
            }
            else
            {
                GUILayout.Space(200);
            }

            // Description
            EditorGUI.BeginChangeCheck();
            var descProp = serialized.FindProperty("Description");
            if (descProp != null)
                EditorGUILayout.PropertyField(descProp, GUIContent.none, GUILayout.Width(COL_DESC));
            else
                GUILayout.Space(COL_DESC);
            if (EditorGUI.EndChangeCheck())
            {
                serialized.ApplyModifiedProperties();
                _isDirty = true;
            }


        

            // Status
            if (hasMissingId)
                EditorGUILayout.LabelField("⚠ No ID", EditorStyles.miniLabel, GUILayout.Width(COL_STATUS));
            else if (hasDuplicateId)
                EditorGUILayout.LabelField("⚠ Dupe", EditorStyles.miniLabel, GUILayout.Width(COL_STATUS));
            else
                GUILayout.Space(COL_STATUS);

            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.EndScrollView();
    }

    private void DrawFooter()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

        int dupeCount = _effectors
            .GroupBy(e => e.Id)
            .Count(g => g.Count() > 1 && !string.IsNullOrEmpty(g.Key));

        int missingCount = _effectors.Count(e => string.IsNullOrEmpty(e.Id));

        int basicCount = _effectors.Count(e => e is BasicNumericalStatusEffectorSO);
        int complexCount = _effectors.Count(e => e is ComplexNumericalStatusEffectorSO);
        int compositeCount = _effectors.Count(e => e is CompositeStatusEffectorSO);

        string status = $"Total: {_effectors.Count} " +
                        $"(Basic: {basicCount} | Complex: {complexCount} | Composite: {compositeCount})";

        if (dupeCount > 0) status += $" | ⚠ {dupeCount} duplicate ID(s)";
        if (missingCount > 0) status += $" | ⚠ {missingCount} missing ID(s)";

        var statusStyle = new GUIStyle(EditorStyles.miniLabel)
        {
            normal =
            {
                textColor = (dupeCount > 0 || missingCount > 0) ? Color.red : Color.green
            }
        };

        EditorGUILayout.LabelField(status, statusStyle);
        GUILayout.FlexibleSpace();

        if (_isDirty)
        {
            var dirtyStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                normal = { textColor = Color.yellow }
            };
            EditorGUILayout.LabelField("● Unsaved changes", dirtyStyle);
        }

        EditorGUILayout.EndHorizontal();
    }

    private void SaveAll()
    {
        foreach (var effector in _effectors)
            EditorUtility.SetDirty(effector);
        AssetDatabase.SaveAssets();
        _isDirty = false;
        Debug.Log("[ItemDatabase] All effectors saved.");
    }
}
#endif