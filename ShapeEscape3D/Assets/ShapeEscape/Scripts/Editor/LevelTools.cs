using System.IO;
using System.Text;
using ShapeEscape.Core;
using UnityEditor;
using UnityEngine;

namespace ShapeEscape.EditorTools
{
    /// <summary>Level pipeline tools: export the campaign to ScriptableObjects and validate every level.</summary>
    public static class LevelTools
    {
        const string LevelsDir = ShapeEscapeSetup.ResourcesDir + "/Levels";
        const string DatabasePath = ShapeEscapeSetup.ResourcesDir + "/LevelDatabase.asset";

        [MenuItem("Shape Escape/3. Export Campaign To Assets", priority = 3)]
        public static void ExportCampaign()
        {
            if (!Directory.Exists(LevelsDir)) Directory.CreateDirectory(LevelsDir);
            AssetDatabase.Refresh();

            var db = AssetDatabase.LoadAssetAtPath<LevelDatabase>(DatabasePath);
            if (db == null)
            {
                db = ScriptableObject.CreateInstance<LevelDatabase>();
                AssetDatabase.CreateAsset(db, DatabasePath);
            }
            db.levels.Clear();
            foreach (var spec in LevelCatalog.Campaign)
            {
                string path = LevelsDir + "/Level_" + spec.number.ToString("00") + "_" + spec.objectId + ".asset";
                var existing = AssetDatabase.LoadAssetAtPath<LevelDefinition>(path);
                if (existing == null)
                {
                    existing = LevelDefinition.FromSpec(spec);
                    AssetDatabase.CreateAsset(existing, path);
                }
                db.levels.Add(existing);
            }
            EditorUtility.SetDirty(db);
            AssetDatabase.SaveAssets();
            Debug.Log("[Shape Escape] Exported " + db.levels.Count + " levels to " + LevelsDir + ". Existing assets were kept (designer edits preserved).");
        }

        [MenuItem("Shape Escape/Validate All Levels", priority = 21)]
        public static void ValidateAll()
        {
            var sb = new StringBuilder("Shape Escape level validation\n");
            int failures = 0;
            int count = LevelProvider.LevelCount;
            for (int n = 1; n <= count; n++)
            {
                var spec = LevelProvider.GetSpec(n);
                EditorUtility.DisplayProgressBar("Validating levels", "Level " + n, n / (float)count);
                var handcrafted = LevelProvider.GetHandcrafted(spec);
                ValidationReport report;
                int pieces;
                if (handcrafted != null)
                {
                    report = PuzzleValidator.Validate(handcrafted);
                    pieces = handcrafted.pieces.Count;
                }
                else
                {
                    GenerationReport gen;
                    var def = PuzzleGenerator.Generate(spec.ToRequest(), out gen);
                    report = gen.validation;
                    pieces = def.pieces.Count;
                }
                var tier = TierInfo.Get(spec.tier);
                bool countOk = pieces >= tier.minPieces && pieces <= tier.maxPieces;
                if (!report.IsValid || (!countOk && handcrafted == null)) failures++;
                sb.AppendLine(string.Format("L{0:00} {1,-16} {2,-8} {3} {4}{5}", n, spec.objectId, spec.tier, handcrafted != null ? "[hand]" : "[proc]",
                    report, countOk ? "" : " PIECE COUNT OUT OF TIER RANGE"));
            }
            EditorUtility.ClearProgressBar();
            sb.AppendLine(failures == 0 ? "All levels valid." : failures + " problem(s) found.");
            if (failures == 0) Debug.Log(sb.ToString());
            else Debug.LogError(sb.ToString());
        }
    }

    [CustomEditor(typeof(LevelDefinition))]
    public sealed class LevelDefinitionEditor : Editor
    {
        string _report;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var level = (LevelDefinition)target;
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Tools", EditorStyles.boldLabel);

            if (ObjectCatalog.Get(level.objectId) == null)
                EditorGUILayout.HelpBox("Unknown object id. Valid ids: " + string.Join(", ", System.Linq.Enumerable.ToArray(System.Linq.Enumerable.Select(ObjectCatalog.All, t => t.id))), MessageType.Error);

            if (GUILayout.Button("Validate"))
            {
                if (level.handcrafted)
                    _report = PuzzleValidator.Validate(level.layout).ToString();
                else
                {
                    GenerationReport gen;
                    var def = PuzzleGenerator.Generate(level.ToSpec().ToRequest(), out gen);
                    _report = "Generated " + def.pieces.Count + " pieces, " + gen;
                }
            }

            if (GUILayout.Button("Bake procedural layout into handcrafted layout"))
            {
                Undo.RecordObject(level, "Bake level layout");
                level.layout = PuzzleGenerator.Generate(level.ToSpec().ToRequest());
                level.handcrafted = true;
                EditorUtility.SetDirty(level);
                _report = "Baked " + level.layout.pieces.Count + " pieces. Edit directions/mechanics, then Validate.";
            }

            if (!string.IsNullOrEmpty(_report))
                EditorGUILayout.HelpBox(_report, _report.StartsWith("INVALID") || _report.Contains("INVALID") ? MessageType.Error : MessageType.Info);
        }
    }
}
