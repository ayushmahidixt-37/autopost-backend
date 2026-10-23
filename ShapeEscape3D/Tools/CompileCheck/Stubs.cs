// Compile-check only: APIs that exist in Unity's Android/iOS player assemblies but not in the
// platform-neutral reference assemblies used by check.sh. Never copied into the Unity project.
namespace UnityEngine
{
    public static class Handheld
    {
        public static void Vibrate() { }
    }
}
