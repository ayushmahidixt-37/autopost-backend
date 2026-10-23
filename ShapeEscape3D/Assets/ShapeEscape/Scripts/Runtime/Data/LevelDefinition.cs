using System.Collections.Generic;
using ShapeEscape.Core;
using UnityEngine;

namespace ShapeEscape
{
    /// <summary>
    /// Designer-editable level configuration. A level is either procedural (object + tier + seed + mechanics,
    /// regenerated deterministically at runtime) or handcrafted (an explicit, validated piece layout).
    /// Create via Assets > Create > Shape Escape > Level Definition, or export the built-in campaign with
    /// "Shape Escape > Export Campaign To Assets".
    /// </summary>
    [CreateAssetMenu(menuName = "Shape Escape/Level Definition", fileName = "Level_")]
    public sealed class LevelDefinition : ScriptableObject
    {
        public int number = 1;
        [Tooltip("Object template id, e.g. coffee_mug (see ObjectCatalog).")]
        public string objectId = "coffee_mug";
        [Range(0, 2)] public int variant;
        public DifficultyTier tier = DifficultyTier.Beginner;
        [Tooltip("Desired piece count (clamped into the tier range). 0 = middle of range.")]
        public int targetPieces;
        public int seed = 1;
        public MechanicPlan mechanics = new MechanicPlan();
        public ArrowKind introduces = ArrowKind.Normal;

        [Header("Handcrafted layout (optional)")]
        [Tooltip("When enabled the explicit layout below is used instead of procedural generation.")]
        public bool handcrafted;
        public PuzzleDefinition layout = new PuzzleDefinition();

        public LevelSpec ToSpec()
        {
            return new LevelSpec
            {
                number = number,
                objectId = objectId,
                variant = variant,
                tier = tier,
                targetPieces = targetPieces,
                seed = seed,
                mechanics = mechanics != null ? mechanics.Clone() : new MechanicPlan(),
                introduces = introduces
            };
        }

        public static LevelDefinition FromSpec(LevelSpec spec)
        {
            var d = CreateInstance<LevelDefinition>();
            d.number = spec.number;
            d.objectId = spec.objectId;
            d.variant = spec.variant;
            d.tier = spec.tier;
            d.targetPieces = spec.targetPieces;
            d.seed = spec.seed;
            d.mechanics = spec.mechanics != null ? spec.mechanics.Clone() : new MechanicPlan();
            d.introduces = spec.introduces;
            d.name = "Level_" + spec.number.ToString("00") + "_" + spec.objectId;
            return d;
        }
    }

    /// <summary>Resolves level specs and handcrafted layouts from the database asset or the built-in catalog.</summary>
    public static class LevelProvider
    {
        public const string DatabaseResource = "ShapeEscape/LevelDatabase";
        static LevelDatabase _db;
        static bool _loaded;

        static LevelDatabase Database
        {
            get
            {
                if (!_loaded)
                {
                    _db = Resources.Load<LevelDatabase>(DatabaseResource);
                    _loaded = true;
                }
                return _db;
            }
        }

        public static int LevelCount
        {
            get
            {
                var db = Database;
                return db != null && db.levels.Count > 0 ? db.levels.Count : LevelCatalog.CampaignLevelCount;
            }
        }

        public static LevelSpec GetSpec(int number)
        {
            var db = Database;
            if (db != null && number >= 1 && number <= db.levels.Count && db.levels[number - 1] != null)
            {
                var spec = db.levels[number - 1].ToSpec();
                spec.number = number;
                return spec;
            }
            return LevelCatalog.Get(number);
        }

        /// <summary>Returns a handcrafted layout for the level, or null for procedural levels.</summary>
        public static PuzzleDefinition GetHandcrafted(LevelSpec spec)
        {
            if (spec == null || spec.isDaily) return null;
            var db = Database;
            if (db == null || spec.number < 1 || spec.number > db.levels.Count) return null;
            var def = db.levels[spec.number - 1];
            if (def == null || !def.handcrafted || def.layout == null || def.layout.pieces.Count == 0) return null;
            var layout = def.layout.Clone();
            if (string.IsNullOrEmpty(layout.objectId)) layout.objectId = def.objectId;
            return layout;
        }
    }
}
