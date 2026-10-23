using UnityEngine;

namespace ShapeEscape
{
    /// <summary>
    /// Subtle haptics. On Android 8+ it uses VibrationEffect with amplitude control for soft "ticks";
    /// older devices fall back to short plain vibrations. Respects the Vibration setting.
    /// The Handheld.Vibrate reference also makes Unity add the VIBRATE permission to the manifest.
    /// </summary>
    public sealed class HapticsManager : MonoBehaviour
    {
        GameManager _game;
#if UNITY_ANDROID && !UNITY_EDITOR
        AndroidJavaObject _vibrator;
        AndroidJavaClass _effectClass;
        int _sdk;
#endif

        public void Initialize(GameManager game)
        {
            _game = game;
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var version = new AndroidJavaClass("android.os.Build$VERSION")) _sdk = version.GetStatic<int>("SDK_INT");
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                    _vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
                if (_sdk >= 26) _effectClass = new AndroidJavaClass("android.os.VibrationEffect");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[Haptics] Vibrator unavailable: " + e.Message);
                _vibrator = null;
            }
#endif
        }

        bool Enabled { get { return _game != null && _game.Save.Data.settings.vibrationOn; } }

        public void Light() { Vibrate(12, 60); }
        public void Medium() { Vibrate(22, 140); }
        public void Success() { Vibrate(45, 180); }

        void Vibrate(long milliseconds, int amplitude)
        {
            if (!Enabled) return;
#if UNITY_ANDROID && !UNITY_EDITOR
            if (_vibrator == null) return;
            try
            {
                if (_sdk >= 26 && _effectClass != null)
                {
                    using (var effect = _effectClass.CallStatic<AndroidJavaObject>("createOneShot", milliseconds, Mathf.Clamp(amplitude, 1, 255)))
                        _vibrator.Call("vibrate", effect);
                }
                else
                {
                    _vibrator.Call("vibrate", milliseconds);
                }
            }
            catch (System.Exception)
            {
                Handheld.Vibrate();
            }
#elif UNITY_IOS && !UNITY_EDITOR
            if (amplitude > 150) Handheld.Vibrate();
#endif
        }
    }
}
