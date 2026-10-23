using System.Collections.Generic;
using UnityEngine;

namespace ShapeEscape
{
    /// <summary>Ordered list of levels. Place one at Resources/ShapeEscape/LevelDatabase to override the built-in campaign.</summary>
    [CreateAssetMenu(menuName = "Shape Escape/Level Database", fileName = "LevelDatabase")]
    public sealed class LevelDatabase : ScriptableObject
    {
        public List<LevelDefinition> levels = new List<LevelDefinition>();
    }
}
