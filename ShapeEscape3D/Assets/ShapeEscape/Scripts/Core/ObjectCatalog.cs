using System.Collections.Generic;

namespace ShapeEscape.Core
{
    /// <summary>
    /// Built-in everyday-object templates. Every object is authored from analytic primitives in a roughly
    /// 2-unit-tall space (Y up, +Z faces the camera by default). Add new objects here (or via a custom
    /// catalog) — the generator, collection and reveal systems pick them up automatically.
    /// </summary>
    public static class ObjectCatalog
    {
        /// <summary>The ten main-campaign objects in level order.</summary>
        public static readonly string[] CampaignOrder =
        {
            "coffee_mug", "water_bottle", "table_lamp", "chair", "alarm_clock",
            "headphones", "backpack", "camera", "bicycle", "washing_machine"
        };

        /// <summary>Collectibles only obtainable from the daily challenge.</summary>
        public static readonly string[] DailyExclusives =
        {
            "teapot", "toy_robot", "retro_radio", "desk_fan", "laptop", "toolbox"
        };

        static Dictionary<string, ObjectTemplate> _byId;
        static List<ObjectTemplate> _all;

        public static IList<ObjectTemplate> All
        {
            get { EnsureBuilt(); return _all; }
        }

        public static ObjectTemplate Get(string id)
        {
            EnsureBuilt();
            ObjectTemplate t;
            return id != null && _byId.TryGetValue(id, out t) ? t : null;
        }

        public static void Register(ObjectTemplate template)
        {
            EnsureBuilt();
            _byId[template.id] = template;
            _all.RemoveAll(t => t.id == template.id);
            _all.Add(template);
        }

        static readonly object BuildLock = new object();

        static void EnsureBuilt()
        {
            if (_all != null) return;
            lock (BuildLock)
            {
                if (_all != null) return;
                var list = new List<ObjectTemplate>
                {
                    CoffeeMug(), WaterBottle(), TableLamp(), Chair(), AlarmClock(),
                    Headphones(), Backpack(), Camera(), Bicycle(), WashingMachine(),
                    Teapot(), ToyRobot(), RetroRadio(), DeskFan(), Laptop(), Toolbox()
                };
                var map = new Dictionary<string, ObjectTemplate>();
                foreach (var t in list) map[t.id] = t;
                _byId = map;
                _all = list;
            }
        }

        static Float3 V(float x, float y, float z) { return new Float3(x, y, z); }

        const int X = 0, Y = 1, Z = 2;

        static ObjectTemplate Make(string id, string name, ObjectCategory cat, bool daily, string[] palette, params ShapePrimitive[] prims)
        {
            return new ObjectTemplate
            {
                id = id,
                displayName = name,
                category = cat,
                dailyExclusive = daily,
                palette = palette,
                primitives = new List<ShapePrimitive>(prims)
            };
        }

        // ------------------------------------------------------------------ campaign objects

        static ObjectTemplate CoffeeMug()
        {
            return Make("coffee_mug", "Coffee Mug", ObjectCategory.Kitchen, false,
                new[] { "#FF7A6B", "#FFB4A2", "#6B4226", "#FFF1E6" },
                ShapePrimitive.Torus(V(0.8f, 0.05f, 0), Z, 0.42f, 0.15f, 1, -95f, 95f),
                ShapePrimitive.Cylinder(V(0, 0.74f, 0), Y, 0.78f, 0.06f, 3).Hollow(0.64f),
                ShapePrimitive.Cylinder(V(0, 0, 0), Y, 0.75f, 0.8f, 0).Hollow(0.667f, 0.875f),
                ShapePrimitive.Cylinder(V(0, 0.2f, 0), Y, 0.5f, 0.8f, 0).Carve(),
                ShapePrimitive.Cylinder(V(0, 0.5f, 0), Y, 0.56f, 0.03f, 2).RevealOnly());
        }

        static ObjectTemplate WaterBottle()
        {
            return Make("water_bottle", "Water Bottle", ObjectCategory.Kitchen, false,
                new[] { "#6EC6FF", "#3A5BA0", "#FFFFFF" },
                ShapePrimitive.Cylinder(V(0, 1.27f, 0), Y, 0.27f, 0.14f, 1),
                ShapePrimitive.Cylinder(V(0, -0.2f, 0), Y, 0.52f, 0.28f, 2),
                ShapePrimitive.Cylinder(V(0, -0.2f, 0), Y, 0.5f, 0.85f, 0),
                ShapePrimitive.Frustum(V(0, 0.8f, 0), Y, 0.5f, 0.24f, 0.16f, 0),
                ShapePrimitive.Cylinder(V(0, 1.05f, 0), Y, 0.22f, 0.1f, 0));
        }

        static ObjectTemplate TableLamp()
        {
            return Make("table_lamp", "Table Lamp", ObjectCategory.LivingRoom, false,
                new[] { "#A1887F", "#E0E0E0", "#FFD166", "#FFF3B0" },
                ShapePrimitive.Frustum(V(0, 0.6f, 0), Y, 0.82f, 0.45f, 0.42f, 2).Hollow(0.8f),
                ShapePrimitive.Frustum(V(0, 0.52f, 0), Y, 0.66f, 0.33f, 0.42f, 2).Carve(),
                ShapePrimitive.Cylinder(V(0, -1.0f, 0), Y, 0.58f, 0.1f, 0),
                ShapePrimitive.Cylinder(V(0, -0.35f, 0), Y, 0.13f, 0.6f, 1),
                ShapePrimitive.Sphere(V(0, 0.32f, 0), 0.2f, 3).RevealOnly());
        }

        static ObjectTemplate Chair()
        {
            return Make("chair", "Chair", ObjectCategory.LivingRoom, false,
                new[] { "#4ECDC4", "#2B7A78", "#C7F9F1" },
                ShapePrimitive.Box(V(0, -0.1f, 0), V(0.75f, 0.11f, 0.72f), 0),
                ShapePrimitive.Box(V(0, 0.75f, -0.62f), V(0.75f, 0.75f, 0.11f), 0),
                ShapePrimitive.Box(V(0.6f, -0.72f, 0.57f), V(0.11f, 0.52f, 0.11f), 1),
                ShapePrimitive.Box(V(-0.6f, -0.72f, 0.57f), V(0.11f, 0.52f, 0.11f), 1),
                ShapePrimitive.Box(V(0.6f, -0.72f, -0.57f), V(0.11f, 0.52f, 0.11f), 1),
                ShapePrimitive.Box(V(-0.6f, -0.72f, -0.57f), V(0.11f, 0.52f, 0.11f), 1),
                ShapePrimitive.Box(V(0, 0.02f, 0.05f), V(0.68f, 0.04f, 0.62f), 2).RevealOnly());
        }

        static ObjectTemplate AlarmClock()
        {
            return Make("alarm_clock", "Alarm Clock", ObjectCategory.Bedroom, false,
                new[] { "#FF8FAB", "#FFFFFF", "#FFD166", "#8D99AE", "#2B2D42" },
                ShapePrimitive.Sphere(V(0.52f, 0.82f, 0), 0.3f, 2),
                ShapePrimitive.Sphere(V(-0.52f, 0.82f, 0), 0.3f, 2),
                ShapePrimitive.Box(V(0, 1.0f, 0), V(0.08f, 0.14f, 0.08f), 3),
                ShapePrimitive.Cylinder(V(0, 0, 0), Z, 0.82f, 0.36f, 0),
                ShapePrimitive.Box(V(0.45f, -0.88f, 0), V(0.12f, 0.14f, 0.14f), 3),
                ShapePrimitive.Box(V(-0.45f, -0.88f, 0), V(0.12f, 0.14f, 0.14f), 3),
                ShapePrimitive.Cylinder(V(0, 0, 0.37f), Z, 0.68f, 0.02f, 1).RevealOnly(),
                ShapePrimitive.Box(V(0, 0.2f, 0.4f), V(0.035f, 0.26f, 0.02f), 4).RevealOnly(),
                ShapePrimitive.Box(V(0.15f, 0, 0.41f), V(0.18f, 0.03f, 0.02f), 4).RevealOnly());
        }

        static ObjectTemplate Headphones()
        {
            return Make("headphones", "Headphones", ObjectCategory.Electronics, false,
                new[] { "#7B61FF", "#3D3B8E", "#C9C3FF" },
                ShapePrimitive.Cylinder(V(0.74f, -0.35f, 0), X, 0.32f, 0.09f, 2),
                ShapePrimitive.Cylinder(V(-0.74f, -0.35f, 0), X, 0.32f, 0.09f, 2),
                ShapePrimitive.Cylinder(V(0.95f, -0.35f, 0), X, 0.4f, 0.17f, 1),
                ShapePrimitive.Cylinder(V(-0.95f, -0.35f, 0), X, 0.4f, 0.17f, 1),
                ShapePrimitive.Torus(V(0, 0.2f, 0), Z, 0.92f, 0.14f, 0, 12f, 168f),
                ShapePrimitive.Box(V(0.92f, 0.18f, 0), V(0.08f, 0.22f, 0.09f), 0),
                ShapePrimitive.Box(V(-0.92f, 0.18f, 0), V(0.08f, 0.22f, 0.09f), 0));
        }

        static ObjectTemplate Backpack()
        {
            return Make("backpack", "Backpack", ObjectCategory.Outdoor, false,
                new[] { "#FF9F1C", "#FFBF69", "#8C4A00" },
                ShapePrimitive.Box(V(0, -0.35f, 0.5f), V(0.5f, 0.38f, 0.14f), 1),
                ShapePrimitive.Box(V(0.38f, -0.05f, -0.5f), V(0.1f, 0.8f, 0.09f), 2),
                ShapePrimitive.Box(V(-0.38f, -0.05f, -0.5f), V(0.1f, 0.8f, 0.09f), 2),
                ShapePrimitive.Torus(V(0, 1.12f, 0), Z, 0.22f, 0.08f, 2, 0f, 180f),
                ShapePrimitive.Box(V(0, -0.1f, 0), V(0.7f, 0.85f, 0.42f), 0),
                ShapePrimitive.Ellipsoid(V(0, 0.72f, 0), V(0.7f, 0.42f, 0.42f), 0));
        }

        static ObjectTemplate Camera()
        {
            return Make("camera", "Camera", ObjectCategory.Electronics, false,
                new[] { "#4A4E69", "#22223B", "#9AD1D4", "#F25F5C", "#C9ADA7" },
                ShapePrimitive.Cylinder(V(0.65f, 0.68f, 0), Y, 0.13f, 0.08f, 3),
                ShapePrimitive.Cylinder(V(0.1f, -0.02f, 0.62f), Z, 0.44f, 0.3f, 1),
                ShapePrimitive.Box(V(-0.45f, 0.7f, 0), V(0.32f, 0.16f, 0.3f), 0),
                ShapePrimitive.Box(V(0, 0, 0), V(1.0f, 0.6f, 0.4f), 0),
                ShapePrimitive.Box(V(-0.82f, 0, 0.42f), V(0.16f, 0.5f, 0.06f), 4),
                ShapePrimitive.Cylinder(V(0.1f, -0.02f, 0.93f), Z, 0.3f, 0.02f, 2).RevealOnly(),
                ShapePrimitive.Box(V(0.65f, 0.42f, 0.41f), V(0.14f, 0.08f, 0.02f), 2).RevealOnly());
        }

        static ObjectTemplate Bicycle()
        {
            var rear = V(-1.1f, -0.4f, 0);
            var front = V(1.1f, -0.4f, 0);
            var bracket = V(-0.15f, -0.4f, 0);
            var seat = V(-0.4f, 0.48f, 0);
            var head = V(0.72f, 0.5f, 0);
            return Make("bicycle", "Bicycle", ObjectCategory.Outdoor, false,
                new[] { "#2D3142", "#EF476F", "#FFD166", "#BFC0C0" },
                ShapePrimitive.Torus(rear, Z, 0.62f, 0.13f, 0),
                ShapePrimitive.Torus(front, Z, 0.62f, 0.13f, 0),
                ShapePrimitive.Box(V(-0.42f, 0.66f, 0), V(0.22f, 0.07f, 0.12f), 2),
                ShapePrimitive.Capsule(V(0.62f, 0.78f, -0.32f), V(0.62f, 0.78f, 0.32f), 0.09f, 0),
                ShapePrimitive.Capsule(rear, bracket, 0.12f, 1),
                ShapePrimitive.Capsule(rear, seat, 0.12f, 1),
                ShapePrimitive.Capsule(seat, bracket, 0.12f, 1),
                ShapePrimitive.Capsule(seat, head, 0.12f, 1),
                ShapePrimitive.Capsule(head, bracket, 0.12f, 1),
                ShapePrimitive.Capsule(head, front, 0.12f, 1),
                ShapePrimitive.Capsule(head, V(0.62f, 0.78f, 0), 0.1f, 1),
                ShapePrimitive.Cylinder(rear, Z, 0.1f, 0.08f, 3).RevealOnly(),
                ShapePrimitive.Cylinder(front, Z, 0.1f, 0.08f, 3).RevealOnly(),
                ShapePrimitive.Cylinder(bracket, Z, 0.16f, 0.1f, 3).RevealOnly());
        }

        static ObjectTemplate WashingMachine()
        {
            return Make("washing_machine", "Washing Machine", ObjectCategory.Garage, false,
                new[] { "#E9F1F7", "#A8DADC", "#457B9D", "#BDE0FE", "#E63946" },
                ShapePrimitive.Torus(V(0, -0.15f, 0.72f), Z, 0.5f, 0.18f, 2),
                ShapePrimitive.Box(V(0, 0.82f, 0.74f), V(0.85f, 0.16f, 0.08f), 1),
                ShapePrimitive.Box(V(0, 0, 0), V(0.85f, 1.0f, 0.75f), 0),
                ShapePrimitive.Cylinder(V(0, -0.15f, 0.78f), Z, 0.42f, 0.03f, 3).RevealOnly(),
                ShapePrimitive.Cylinder(V(0.55f, 0.82f, 0.83f), Z, 0.1f, 0.04f, 4).RevealOnly(),
                ShapePrimitive.Box(V(-0.35f, 0.82f, 0.83f), V(0.3f, 0.07f, 0.02f), 2).RevealOnly());
        }

        // ------------------------------------------------------------------ daily-exclusive objects

        static ObjectTemplate Teapot()
        {
            return Make("teapot", "Teapot", ObjectCategory.Kitchen, true,
                new[] { "#F4A261", "#E76F51" },
                ShapePrimitive.Sphere(V(0, 0.95f, 0), 0.13f, 1),
                ShapePrimitive.Frustum(V(0, 0.7f, 0), Y, 0.48f, 0.3f, 0.12f, 1),
                ShapePrimitive.Torus(V(-0.9f, 0.05f, 0), Z, 0.38f, 0.11f, 1, 90f, 270f),
                ShapePrimitive.Capsule(V(0.75f, -0.1f, 0), V(1.3f, 0.45f, 0), 0.14f, 0),
                ShapePrimitive.Ellipsoid(V(0, 0, 0), V(0.9f, 0.7f, 0.9f), 0));
        }

        static ObjectTemplate ToyRobot()
        {
            return Make("toy_robot", "Toy Robot", ObjectCategory.Toys, true,
                new[] { "#90BE6D", "#F9C74F", "#F94144", "#577590" },
                ShapePrimitive.Sphere(V(0, 1.36f, 0), 0.12f, 2),
                ShapePrimitive.Capsule(V(0, 1.05f, 0), V(0, 1.3f, 0), 0.06f, 3),
                ShapePrimitive.Box(V(0, 0.75f, 0), V(0.42f, 0.32f, 0.35f), 1),
                ShapePrimitive.Box(V(0.82f, -0.05f, 0), V(0.16f, 0.45f, 0.16f), 1),
                ShapePrimitive.Box(V(-0.82f, -0.05f, 0), V(0.16f, 0.45f, 0.16f), 1),
                ShapePrimitive.Box(V(0, -0.1f, 0), V(0.6f, 0.55f, 0.4f), 0),
                ShapePrimitive.Box(V(0.28f, -0.95f, 0), V(0.18f, 0.32f, 0.2f), 3),
                ShapePrimitive.Box(V(-0.28f, -0.95f, 0), V(0.18f, 0.32f, 0.2f), 3),
                ShapePrimitive.Sphere(V(0.17f, 0.8f, 0.36f), 0.08f, 2).RevealOnly(),
                ShapePrimitive.Sphere(V(-0.17f, 0.8f, 0.36f), 0.08f, 2).RevealOnly());
        }

        static ObjectTemplate RetroRadio()
        {
            return Make("retro_radio", "Retro Radio", ObjectCategory.LivingRoom, true,
                new[] { "#E9C46A", "#264653", "#E76F51" },
                ShapePrimitive.Cylinder(V(-0.45f, -0.05f, 0.42f), Z, 0.4f, 0.06f, 1),
                ShapePrimitive.Cylinder(V(0.55f, 0.1f, 0.42f), Z, 0.22f, 0.06f, 2),
                ShapePrimitive.Torus(V(0, 0.62f, 0), Z, 0.55f, 0.08f, 1, 0f, 180f),
                ShapePrimitive.Capsule(V(0.8f, 0.6f, 0), V(1.2f, 1.35f, 0), 0.06f, 1),
                ShapePrimitive.Box(V(0, 0, 0), V(1.1f, 0.65f, 0.4f), 0),
                ShapePrimitive.Box(V(0.75f, -0.72f, 0), V(0.12f, 0.08f, 0.2f), 1),
                ShapePrimitive.Box(V(-0.75f, -0.72f, 0), V(0.12f, 0.08f, 0.2f), 1));
        }

        static ObjectTemplate DeskFan()
        {
            return Make("desk_fan", "Desk Fan", ObjectCategory.Office, true,
                new[] { "#6D597A", "#B56576", "#EAAC8B" },
                ShapePrimitive.Sphere(V(0, 0.35f, 0.02f), 0.13f, 1),
                ShapePrimitive.Ellipsoid(V(0.38f, 0.35f, 0), V(0.3f, 0.12f, 0.05f), 2),
                ShapePrimitive.Ellipsoid(V(-0.38f, 0.35f, 0), V(0.3f, 0.12f, 0.05f), 2),
                ShapePrimitive.Ellipsoid(V(0, 0.73f, 0), V(0.12f, 0.3f, 0.05f), 2),
                ShapePrimitive.Ellipsoid(V(0, -0.03f, 0), V(0.12f, 0.3f, 0.05f), 2),
                ShapePrimitive.Torus(V(0, 0.35f, 0), Z, 0.78f, 0.09f, 1),
                ShapePrimitive.Cylinder(V(0, 0.35f, -0.28f), Z, 0.24f, 0.2f, 0),
                ShapePrimitive.Capsule(V(0, -1.0f, -0.2f), V(0, 0.15f, -0.25f), 0.1f, 0),
                ShapePrimitive.Cylinder(V(0, -1.05f, -0.2f), Y, 0.55f, 0.09f, 0));
        }

        static ObjectTemplate Laptop()
        {
            return Make("laptop", "Laptop", ObjectCategory.Electronics, true,
                new[] { "#ADB5BD", "#495057", "#90E0EF", "#6C757D" },
                ShapePrimitive.Box(V(0, -0.4f, 0), V(1.0f, 0.1f, 0.7f), 0),
                ShapePrimitive.Box(V(0, 0.28f, -0.66f), V(1.0f, 0.66f, 0.08f), 1),
                ShapePrimitive.Box(V(0, 0.3f, -0.57f), V(0.9f, 0.56f, 0.01f), 2).RevealOnly(),
                ShapePrimitive.Box(V(0, -0.29f, 0.0f), V(0.85f, 0.01f, 0.4f), 3).RevealOnly(),
                ShapePrimitive.Box(V(0, -0.29f, 0.52f), V(0.25f, 0.01f, 0.12f), 3).RevealOnly());
        }

        static ObjectTemplate Toolbox()
        {
            return Make("toolbox", "Toolbox", ObjectCategory.Garage, true,
                new[] { "#E63946", "#A4161A", "#6C757D", "#FFD166" },
                ShapePrimitive.Torus(V(0, 0.58f, 0), Z, 0.36f, 0.08f, 2, 0f, 180f),
                ShapePrimitive.Box(V(0, 0.5f, 0), V(1.02f, 0.09f, 0.52f), 1),
                ShapePrimitive.Box(V(0, -0.05f, 0), V(1.0f, 0.5f, 0.5f), 0),
                ShapePrimitive.Box(V(0.6f, 0.3f, 0.53f), V(0.1f, 0.08f, 0.03f), 3).RevealOnly(),
                ShapePrimitive.Box(V(-0.6f, 0.3f, 0.53f), V(0.1f, 0.08f, 0.03f), 3).RevealOnly());
        }

        public static string CategoryDisplayName(ObjectCategory c)
        {
            switch (c)
            {
                case ObjectCategory.Kitchen: return "Kitchen";
                case ObjectCategory.LivingRoom: return "Living Room";
                case ObjectCategory.Bedroom: return "Bedroom";
                case ObjectCategory.Office: return "Office";
                case ObjectCategory.Garage: return "Garage";
                case ObjectCategory.Electronics: return "Electronics";
                case ObjectCategory.Toys: return "Toys";
                default: return "Outdoor";
            }
        }
    }
}
