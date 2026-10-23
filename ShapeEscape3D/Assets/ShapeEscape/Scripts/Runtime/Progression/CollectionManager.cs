using System;
using System.Collections.Generic;
using ShapeEscape.Core;
using UnityEngine;

namespace ShapeEscape
{
    /// <summary>
    /// The Object Collection: every solved object is added to a themed room (Kitchen, Living Room, ...).
    /// Campaign objects track which of their 3 difficulty variations were solved; daily-exclusive objects
    /// can only be collected from the daily challenge.
    /// </summary>
    public sealed class CollectionManager : MonoBehaviour
    {
        public event Action<string> ObjectUnlocked;

        GameManager _game;

        public static readonly ObjectCategory[] Rooms =
        {
            ObjectCategory.Kitchen, ObjectCategory.LivingRoom, ObjectCategory.Bedroom, ObjectCategory.Office,
            ObjectCategory.Garage, ObjectCategory.Electronics, ObjectCategory.Toys, ObjectCategory.Outdoor
        };

        public void Initialize(GameManager game)
        {
            _game = game;
        }

        public bool IsUnlocked(string objectId)
        {
            return _game.Save.Data.GetObject(objectId) != null;
        }

        public int VariantsCompleted(string objectId)
        {
            var r = _game.Save.Data.GetObject(objectId);
            if (r == null) return 0;
            int n = 0;
            for (int i = 0; i < 3; i++) if ((r.variantsMask & (1 << i)) != 0) n++;
            return n;
        }

        /// <summary>Marks an object (and difficulty variation) as collected. Returns true if the object is new.</summary>
        public bool Unlock(string objectId, int variant)
        {
            var data = _game.Save.Data;
            var r = data.GetObject(objectId);
            bool isNew = r == null;
            if (isNew)
            {
                r = new SaveData.ObjectRecord { id = objectId, unlockedUtcTicks = DateTime.UtcNow.Ticks };
                data.objects.Add(r);
            }
            r.variantsMask |= 1 << Mathf.Clamp(variant, 0, 2);
            _game.Save.MarkDirty();
            if (isNew)
            {
                _game.Analytics.Log(AnalyticsEvents.CollectionProgress, "object", objectId, "unlocked", UnlockedCount, "total", TotalCount);
                if (ObjectUnlocked != null) ObjectUnlocked(objectId);
            }
            return isNew;
        }

        public int UnlockedCount { get { return _game.Save.Data.objects.Count; } }
        public int TotalCount { get { return ObjectCatalog.All.Count; } }

        public List<ObjectTemplate> ObjectsIn(ObjectCategory room)
        {
            var list = new List<ObjectTemplate>();
            foreach (var t in ObjectCatalog.All) if (t.category == room) list.Add(t);
            return list;
        }

        public int UnlockedIn(ObjectCategory room)
        {
            int n = 0;
            foreach (var t in ObjectsIn(room)) if (IsUnlocked(t.id)) n++;
            return n;
        }

        /// <summary>Pastel gradient (top, bottom) for each themed room.</summary>
        public static void RoomColors(ObjectCategory room, out Color top, out Color bottom)
        {
            switch (room)
            {
                case ObjectCategory.Kitchen: top = ColorUtil.Hex("#FFE8D6"); bottom = ColorUtil.Hex("#FFD3B6"); break;
                case ObjectCategory.LivingRoom: top = ColorUtil.Hex("#E3F2FD"); bottom = ColorUtil.Hex("#C8E6C9"); break;
                case ObjectCategory.Bedroom: top = ColorUtil.Hex("#EDE7F6"); bottom = ColorUtil.Hex("#F8BBD0"); break;
                case ObjectCategory.Office: top = ColorUtil.Hex("#E0F7FA"); bottom = ColorUtil.Hex("#D1C4E9"); break;
                case ObjectCategory.Garage: top = ColorUtil.Hex("#ECEFF1"); bottom = ColorUtil.Hex("#CFD8DC"); break;
                case ObjectCategory.Electronics: top = ColorUtil.Hex("#E8EAF6"); bottom = ColorUtil.Hex("#B3E5FC"); break;
                case ObjectCategory.Toys: top = ColorUtil.Hex("#FFF9C4"); bottom = ColorUtil.Hex("#FFCCBC"); break;
                default: top = ColorUtil.Hex("#E0F2F1"); bottom = ColorUtil.Hex("#DCEDC8"); break;
            }
        }
    }
}
