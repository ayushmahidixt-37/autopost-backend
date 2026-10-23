using System;
using System.IO;
using UnityEngine;

namespace ShapeEscape
{
    /// <summary>Optional cloud backup (e.g. Google Play Games Saved Games). The game works fully offline without it.</summary>
    public interface ICloudSaveProvider
    {
        bool IsAvailable { get; }
        void Push(string json, Action<bool> done);
        void Pull(Action<bool, string> done);
    }

    /// <summary>Default provider: no cloud. Swap in a Play Games implementation via SaveManager.SetCloudProvider.</summary>
    public sealed class NullCloudSaveProvider : ICloudSaveProvider
    {
        public bool IsAvailable { get { return false; } }
        public void Push(string json, Action<bool> done) { if (done != null) done(false); }
        public void Pull(Action<bool, string> done) { if (done != null) done(false, null); }
    }

    /// <summary>
    /// Local JSON save with atomic writes (temp file + replace) and a rolling backup, so a crash or power loss
    /// mid-write can never corrupt progress. Saves are batched (dirty flag + short delay) and forced on pause/quit.
    /// </summary>
    public sealed class SaveManager : MonoBehaviour
    {
        const string FileName = "shape_escape_save.json";
        const float AutoSaveDelay = 2f;

        public SaveData Data { get; private set; }
        public event Action Loaded;

        ICloudSaveProvider _cloud = new NullCloudSaveProvider();
        bool _dirty;
        float _dirtyTime;

        string PathMain { get { return Path.Combine(Application.persistentDataPath, FileName); } }
        string PathBackup { get { return PathMain + ".bak"; } }
        string PathTemp { get { return PathMain + ".tmp"; } }

        public void Initialize()
        {
            Data = TryRead(PathMain) ?? TryRead(PathBackup);
            bool fresh = Data == null;
            if (fresh) Data = new SaveData();
            Data.Sanitize();
            if (Data.firstLaunchUtcTicks == 0) Data.firstLaunchUtcTicks = DateTime.UtcNow.Ticks;
            Data.sessions++;
            SaveNow();
            if (Loaded != null) Loaded();
        }

        static SaveData TryRead(string path)
        {
            try
            {
                if (!File.Exists(path)) return null;
                var json = File.ReadAllText(path);
                if (string.IsNullOrEmpty(json)) return null;
                var data = JsonUtility.FromJson<SaveData>(json);
                return data;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[SaveManager] Could not read " + path + ": " + e.Message);
                return null;
            }
        }

        public void MarkDirty()
        {
            if (!_dirty) _dirtyTime = Time.unscaledTime;
            _dirty = true;
            Data.progressStamp++;
        }

        void Update()
        {
            if (_dirty && Time.unscaledTime - _dirtyTime > AutoSaveDelay) SaveNow();
        }

        public void SaveNow()
        {
            _dirty = false;
            try
            {
                Data.lastSaveUtcTicks = DateTime.UtcNow.Ticks;
                var json = JsonUtility.ToJson(Data);
                File.WriteAllText(PathTemp, json);
                if (File.Exists(PathMain))
                {
                    File.Copy(PathMain, PathBackup, true);
                    File.Delete(PathMain);
                }
                File.Move(PathTemp, PathMain);
                if (_cloud.IsAvailable) _cloud.Push(json, null);
            }
            catch (Exception e)
            {
                Debug.LogError("[SaveManager] Save failed: " + e.Message);
            }
        }

        void OnApplicationPause(bool paused)
        {
            if (paused && Data != null) SaveNow();
        }

        void OnApplicationQuit()
        {
            if (Data != null) SaveNow();
        }

        public void SetCloudProvider(ICloudSaveProvider provider)
        {
            _cloud = provider ?? new NullCloudSaveProvider();
        }

        /// <summary>Pulls the cloud copy and keeps whichever has more progress.</summary>
        public void SyncFromCloud(Action<bool> done)
        {
            if (!_cloud.IsAvailable)
            {
                if (done != null) done(false);
                return;
            }
            _cloud.Pull((ok, json) =>
            {
                bool replaced = false;
                if (ok && !string.IsNullOrEmpty(json))
                {
                    try
                    {
                        var remote = JsonUtility.FromJson<SaveData>(json);
                        if (remote != null && IsAhead(remote, Data))
                        {
                            remote.Sanitize();
                            Data = remote;
                            SaveNow();
                            replaced = true;
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning("[SaveManager] Cloud data rejected: " + e.Message);
                    }
                }
                if (done != null) done(replaced);
            });
        }

        static bool IsAhead(SaveData a, SaveData b)
        {
            if (a.highestUnlockedLevel != b.highestUnlockedLevel) return a.highestUnlockedLevel > b.highestUnlockedLevel;
            if (a.objects.Count != b.objects.Count) return a.objects.Count > b.objects.Count;
            return a.progressStamp > b.progressStamp;
        }

        /// <summary>Debug / settings helper: wipes all progress.</summary>
        public void ResetAll()
        {
            var settings = Data.settings;
            Data = new SaveData { settings = settings, firstLaunchUtcTicks = DateTime.UtcNow.Ticks };
            SaveNow();
        }
    }
}
