using UnityEngine;

namespace ShapeEscape
{
    /// <summary>
    /// Device-aware performance profile. "Auto" picks Smooth (60 FPS, soft shadows, full effects) on capable
    /// devices and Battery Saver (30 FPS, no realtime shadows, fewer particles, capped piece count) on low-end
    /// ones. Players can override it in Settings.
    /// </summary>
    public sealed class QualityManager : MonoBehaviour
    {
        public enum Mode { Auto = 0, Smooth = 1, Battery = 2 }

        GameManager _game;

        public bool IsLowEndDevice { get; private set; }
        public bool LowProfileActive { get; private set; }
        /// <summary>Maximum generated piece count (0 = unlimited). Never below a tier's minimum.</summary>
        public int PieceCap { get { return LowProfileActive ? 90 : 0; } }

        public void Initialize(GameManager game)
        {
            _game = game;
            IsLowEndDevice = DetectLowEnd();
            QualitySettings.vSyncCount = 0;
            Screen.sleepTimeout = SleepTimeout.SystemSetting;
            Apply();
        }

        static bool DetectLowEnd()
        {
            if (Application.isEditor) return false;
            return SystemInfo.systemMemorySize > 0 && SystemInfo.systemMemorySize < 3000
                   || SystemInfo.processorCount <= 4
                   || SystemInfo.graphicsMemorySize > 0 && SystemInfo.graphicsMemorySize < 512;
        }

        public Mode CurrentMode
        {
            get { return (Mode)Mathf.Clamp(_game.Save.Data.settings.qualityMode, 0, 2); }
        }

        public void SetMode(Mode mode)
        {
            _game.Save.Data.settings.qualityMode = (int)mode;
            _game.Save.MarkDirty();
            Apply();
        }

        public void Apply()
        {
            var mode = CurrentMode;
            LowProfileActive = mode == Mode.Battery || (mode == Mode.Auto && IsLowEndDevice);
            Application.targetFrameRate = LowProfileActive ? 30 : 60;
            if (_game.Environment != null) _game.Environment.SetShadowsEnabled(!LowProfileActive);
            if (_game.Vfx != null) _game.Vfx.SetQuality(LowProfileActive ? 0.5f : 1f);
            QualitySettings.antiAliasing = LowProfileActive ? 0 : 2;
        }

        void OnApplicationFocus(bool focus)
        {
            // Some Android devices reset the frame rate after returning from background.
            if (focus && _game != null) Application.targetFrameRate = LowProfileActive ? 30 : 60;
        }
    }
}
