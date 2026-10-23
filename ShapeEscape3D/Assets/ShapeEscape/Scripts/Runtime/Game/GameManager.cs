using System.Collections;
using ShapeEscape.Core;
using UnityEngine;

namespace ShapeEscape
{
    public enum GameState
    {
        Boot,
        Home,
        Playing,
        Complete,
        Collection,
        Daily
    }

    /// <summary>
    /// Composition root and flow controller. Creates every subsystem in dependency order, wires their events,
    /// and implements the screen flow (Home → Play → Reveal → Level Complete → next level, Daily, Collection,
    /// Settings). Subsystems never find each other through globals other than this object.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public sealed class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        public SaveManager Save { get; private set; }
        public AnalyticsManager Analytics { get; private set; }
        public CrashReporter Crash { get; private set; }
        public SceneEnvironment Environment { get; private set; }
        public VfxManager Vfx { get; private set; }
        public QualityManager Quality { get; private set; }
        public AudioManager Audio { get; private set; }
        public HapticsManager Haptics { get; private set; }
        public ProgressionManager Progression { get; private set; }
        public CollectionManager Collection { get; private set; }
        public DailyChallengeManager Daily { get; private set; }
        public HintManager Hints { get; private set; }
        public CameraController CameraRig { get; private set; }
        public TouchInputManager Input { get; private set; }
        public LevelManager Levels { get; private set; }
        public RevealSequence Reveal { get; private set; }
        public CollectionViewer Viewer { get; private set; }
        public UIManager UI { get; private set; }
        public AdManager Ads { get; private set; }
        public PurchaseManager Purchases { get; private set; }
        public LivesManager Lives { get; private set; }
        public FriendsManager Friends { get; private set; }

        public GameState State { get; private set; }

        T Sub<T>(string name) where T : Component
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            return go.AddComponent<T>();
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Screen.orientation = ScreenOrientation.Portrait;

            Save = Sub<SaveManager>("Save");
            Save.Initialize();
            Analytics = Sub<AnalyticsManager>("Analytics");
            Analytics.Initialize(this);
            Crash = Sub<CrashReporter>("CrashReporter");
            Crash.Initialize(Analytics);

            Environment = Sub<SceneEnvironment>("Environment");
            Environment.Initialize();
            Vfx = Sub<VfxManager>("VFX");
            Vfx.Initialize();
            Quality = Sub<QualityManager>("Quality");
            Quality.Initialize(this);

            Audio = Sub<AudioManager>("Audio");
            Audio.Initialize(this);
            Haptics = Sub<HapticsManager>("Haptics");
            Haptics.Initialize(this);

            Progression = Sub<ProgressionManager>("Progression");
            Progression.Initialize(this);
            Collection = Sub<CollectionManager>("Collection");
            Collection.Initialize(this);
            Daily = Sub<DailyChallengeManager>("Daily");
            Daily.Initialize(this);
            Hints = Sub<HintManager>("Hints");
            Hints.Initialize(this);
            Lives = Sub<LivesManager>("Lives");
            Lives.Initialize(this);
            Friends = Sub<FriendsManager>("Friends");

            CameraRig = Environment.MainCamera.gameObject.AddComponent<CameraController>();
            CameraRig.Initialize(Environment.MainCamera);
            Input = Sub<TouchInputManager>("Input");

            Levels = Sub<LevelManager>("Levels");
            Levels.Initialize(this);
            Reveal = Levels.gameObject.AddComponent<RevealSequence>();
            Viewer = Sub<CollectionViewer>("CollectionViewer");
            Viewer.Initialize(this);

            UI = Sub<UIManager>("UI");
            UI.Initialize(this);
            Ads = Sub<AdManager>("Ads");
            Ads.Initialize(this);
            Purchases = Sub<PurchaseManager>("Purchases");
            Purchases.Initialize(this);

            var lang = Save.Data.settings.language;
            Loc.SetLanguage(string.IsNullOrEmpty(lang) ? Loc.DetectDeviceLanguage() : lang);

            WireEvents();
        }

        void WireEvents()
        {
            Input.Tapped += pos => { if (State == GameState.Playing) Levels.HandleTap(pos); };
            Input.DragBegan += () => CameraRig.OnDragBegin();
            Input.Dragged += d => CameraRig.OnDrag(d);
            Input.DragEnded += () => CameraRig.OnDragEnd();
            Input.Pinched += f => CameraRig.OnZoom(f);

            Levels.LevelReady += s => Environment.SetBackground(SceneEnvironment.DefaultTop, SceneEnvironment.DefaultBottom);
            Levels.LevelStarted += OnLevelStarted;
            Levels.ProgressChanged += OnProgress;
            Levels.LevelCompleted += OnLevelCompleted;
            Levels.MechanicIntroduced += OnMechanicIntroduced;
            Levels.OutOfMoves += OnOutOfMoves;
        }

        IEnumerator Start()
        {
            State = GameState.Home;
            Analytics.Log(AnalyticsEvents.GameLaunch, "first_launch", Save.Data.sessions <= 1 ? 1 : 0,
                "device_low_end", Quality.IsLowEndDevice ? 1 : 0, "language", Loc.Current);
            Analytics.SetUserProperty("max_level", Save.Data.highestUnlockedLevel.ToString());
            GoHome();
            yield return null;
            Ads.StartConsentAndInit();
            yield return new WaitForSecondsRealtime(0.8f);
            if (Progression.LoginRewardAvailable && State == GameState.Home && !UI.AnyPopupOpen)
                UI.ShowPopup<LoginRewardPopup>();
        }

        // ---------------------------------------------------------------- navigation

        public void GoHome()
        {
            Time.timeScale = 1f;
            State = GameState.Home;
            Input.InputEnabled = true;
            Viewer.Hide();
            Environment.SetBackground(SceneEnvironment.DefaultTop, SceneEnvironment.DefaultBottom);
            UI.ShowPage<HomeScreen>();
            CameraRig.SetFraming(1.45f, 0.32f);

            var spec = LevelProvider.GetSpec(Progression.NextLevel);
            var cur = Levels.Current;
            if (cur == null || cur.Spec.Key != spec.Key || cur.Completed || cur.Started)
                Levels.Load(spec, false, s => CameraRig.SetFraming(1.45f, 0.32f));
            else
            {
                CameraRig.SetAutoSpin(12f);
                CameraRig.UserControlEnabled = false;
            }
        }

        public void PlayNextLevel()
        {
            PlayLevel(Progression.NextLevel);
        }

        public void PlayLevel(int number)
        {
            if (!Progression.IsUnlocked(number) && Progression.StarsFor(number) == 0) return;
            StartLevel(LevelProvider.GetSpec(number));
        }

        public void OpenMap()
        {
            State = GameState.Home;
            Levels.Unload();
            UI.ShowPage<LevelMapScreen>();
        }

        public void ShowLives()
        {
            UI.ShowPopup<LivesPopup>();
        }

        void StartLevel(LevelSpec spec)
        {
            // Campaign levels need a life (the daily challenge is free).
            if (!spec.isDaily && Lives.Lives <= 0)
            {
                if (State != GameState.Home) GoHome();
                ShowLives();
                return;
            }
            Time.timeScale = 1f;
            State = GameState.Playing;
            UI.ShowPage<GameplayScreen>();
            CameraRig.SetFraming(1f, 0.04f);
            var cur = Levels.Current;
            if (cur != null && cur.Spec.Key == spec.Key && !cur.Completed && !cur.Started && !Levels.IsLoading)
                Levels.StartPlaying();
            else
                Levels.Load(spec, true);
        }

        public void PlayDaily()
        {
            var spec = Daily.TodaySpec();
            Analytics.Log(AnalyticsEvents.DailyStart, "date", spec.dailyDate, "tier", spec.tier.ToString(), "streak", Daily.CurrentStreak);
            StartLevel(spec);
        }

        public void OpenDaily()
        {
            State = GameState.Daily;
            Levels.Unload();
            UI.ShowPage<DailyScreen>();
        }

        public void CloseDaily() { GoHome(); }

        public void OpenCollection()
        {
            State = GameState.Collection;
            Levels.Unload();
            UI.ShowPage<CollectionScreen>();
        }

        public void CloseCollection()
        {
            Viewer.Hide();
            GoHome();
        }

        public void OpenSettings()
        {
            UI.ShowPopup<SettingsPopup>();
        }

        // ---------------------------------------------------------------- in-level actions

        public void PauseGame()
        {
            if (State != GameState.Playing || UI.AnyPopupOpen) return;
            Time.timeScale = 0f;
            UI.ShowPopup<PausePopup>();
        }

        public void ResumeGame()
        {
            Time.timeScale = 1f;
            UI.ClosePopup(UI.Get<PausePopup>());
        }

        /// <summary>True while a campaign level is underway with at least one move made: leaving costs a life.</summary>
        bool LeavingCostsLife
        {
            get
            {
                var s = Levels.Current;
                return State == GameState.Playing && s != null && s.Started && !s.Completed && !s.IsDaily && s.HasMoveLimit && s.Taps > 0;
            }
        }

        public void RestartLevel()
        {
            if (LeavingCostsLife)
            {
                UI.CloseAllPopups();
                UI.Dialog(Loc.T("restart_title"), Loc.T("costs_life_body"),
                    UIManager.Btn(Loc.T("keep_playing"), () => PauseGame()),
                    UIManager.Btn(Loc.T("restart_cost"), () => { Lives.LoseLife("restart"); DoRestart(); }));
                return;
            }
            DoRestart();
        }

        void DoRestart()
        {
            Time.timeScale = 1f;
            UI.CloseAllPopups();
            var cur = Levels.Current;
            if (cur != null && !cur.IsDaily && Lives.Lives <= 0)
            {
                GoHome();
                ShowLives();
                return;
            }
            var s = Levels.Current;
            if (s != null)
                Analytics.Log(AnalyticsEvents.LevelRestart, "level", s.Spec.Key, "pieces_left", Levels.Collision.State != null ? Levels.Collision.State.RemainingCount : 0,
                    "seconds", Mathf.RoundToInt(s.Duration));
            State = GameState.Playing;
            UI.ShowPage<GameplayScreen>();
            Levels.Restart();
        }

        /// <summary>Leaves the level (recorded as a level_fail / abandonment for difficulty analytics).</summary>
        public void QuitToHome(bool confirm)
        {
            var s = Levels.Current;
            bool inProgress = State == GameState.Playing && s != null && s.Started && !s.Completed;
            if (confirm && inProgress && s.Taps > 0)
            {
                bool costs = LeavingCostsLife;
                UI.Dialog(Loc.T("quit_level_title"), costs ? Loc.T("costs_life_body") : Loc.T("quit_level_body"),
                    UIManager.Btn(Loc.T("stay"), () => PauseGame()),
                    UIManager.Btn(costs ? Loc.T("leave_cost") : Loc.T("leave"), () => { if (costs) Lives.LoseLife("quit"); QuitToHome(false); }));
                return;
            }
            if (inProgress)
                Analytics.Log(AnalyticsEvents.LevelFail, "level", s.Spec.Key, "reason", "quit", "seconds", Mathf.RoundToInt(s.Duration),
                    "pieces_left", Levels.Collision.State != null ? Levels.Collision.State.RemainingCount : 0, "blocked", s.BlockedTaps, "hints", s.HintsUsed);
            UI.CloseAllPopups();
            GoHome();
        }

        public void RequestHint()
        {
            if (State != GameState.Playing || !Levels.Interactive) return;
            if (Hints.TryUseHint()) return;
            if (Hints.Available > 0) return; // nothing to hint right now

            // Free starter hints are used up: one hint per optional rewarded ad, with a daily cap.
            int left = Ads.HintAdsLeftToday;
            if (left <= 0)
            {
                UI.Dialog(Loc.T("no_hints_title"), Loc.F("hint_ads_done", EconomyRules.AdHintsPerDay), UIManager.Btn(Loc.T("ok"), null));
                return;
            }
            UI.Dialog(Loc.T("no_hints_title"), Loc.F("hint_ad_body", EconomyRules.FreeHints, left),
                UIManager.Btn(Loc.T("watch_video_hint"), () => Ads.ShowRewardedForHint(earned =>
                {
                    if (earned)
                    {
                        Hints.AddHints(1, "rewarded_ad");
                        StartCoroutine(UseHintNextFrame());
                    }
                    else UI.Toast(Loc.T("ad_unavailable"));
                })),
                UIManager.Btn(Loc.T("cancel"), null));
        }

        IEnumerator UseHintNextFrame()
        {
            yield return null; // let the dialog close and input re-enable first
            Hints.TryUseHint();
        }

        public void ContinueAfterComplete()
        {
            if (State != GameState.Complete) return;
            State = GameState.Boot; // ignore double taps while the ad / load runs
            var finished = Levels.Current;
            Ads.MaybeShowInterstitial("level_complete", () =>
            {
                CameraRig.SetFraming(1f, 0.04f);
                if (finished != null && finished.IsDaily)
                {
                    GoHome();
                    return;
                }
                if (Progression.CampaignComplete && finished != null && finished.Spec.number >= LevelProvider.LevelCount)
                {
                    GoHome();
                    return;
                }
                StartLevel(LevelProvider.GetSpec(Progression.NextLevel));
            });
        }

        public void ResumeAfterTutorial()
        {
            OnPopupStateChanged();
        }

        // ---------------------------------------------------------------- level events

        void OnLevelStarted(LevelSession s)
        {
            Analytics.Log(AnalyticsEvents.LevelStart, "level", s.Spec.Key, "number", s.Spec.number, "object", s.Spec.objectId,
                "tier", s.Spec.tier.ToString(), "pieces", s.TotalPieces, "daily", s.IsDaily ? 1 : 0);
            var hud = UI.Get<GameplayScreen>();
            hud.Bind(s);
            if (!s.IsDaily && s.Spec.number == 1 && !Save.Data.seenTutorials.Contains("basic"))
                hud.ShowTip(Loc.T("tutorial_tap"));
            else if (!s.IsDaily && s.Spec.number == EconomyRules.PracticeLevels + 1 && !Save.Data.seenTutorials.Contains("hearts"))
            {
                Save.Data.seenTutorials.Add("hearts");
                Save.MarkDirty();
                hud.ShowTip(Loc.F("tutorial_hearts", EconomyRules.MistakesPerLevel), 5f);
            }
            else if (s.Spec.superHard) hud.ShowTip(Loc.T("tip_super_hard"), 3.5f);
            else if (s.Spec.hard) hud.ShowTip(Loc.T("tip_hard"), 3f);
            else
                hud.HideTip();
        }

        void OnProgress(LevelSession s)
        {
            var hud = UI.Get<GameplayScreen>();
            hud.UpdateProgress(s);
            hud.UpdateHearts(s);
            if (!Save.Data.seenTutorials.Contains("basic") && s.Spec.number == 1 && !s.IsDaily)
            {
                Save.Data.seenTutorials.Add("basic");
                Save.MarkDirty();
                hud.ShowTip(Loc.T("drag_to_rotate"), 3.5f);
            }
        }

        void OnMechanicIntroduced(ArrowKind kind)
        {
            string key = "mech_" + kind;
            if (Save.Data.seenTutorials.Contains(key)) return;
            Save.Data.seenTutorials.Add(key);
            Save.MarkDirty();
            Analytics.Log(AnalyticsEvents.TutorialShown, "mechanic", kind.ToString());
            UI.ShowPopup<TutorialPopup>().Setup(kind);
        }

        /// <summary>All hearts used: continue with an optional ad (once), try again or give up (both cost a life).</summary>
        void OnOutOfMoves(LevelSession s)
        {
            Analytics.Log(AnalyticsEvents.OutOfMoves, "level", s.Spec.Key, "pieces_left", Levels.Collision.State != null ? Levels.Collision.State.RemainingCount : 0, "continued", s.Continued ? 1 : 0);
            bool campaign = !s.IsDaily;
            var buttons = new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<string, System.Action>>();
            if (!s.Continued && Ads.CanShowRewarded)
                buttons.Add(UIManager.Btn(Loc.F("continue_ad", EconomyRules.ContinueMoves), () => Ads.ShowRewarded("continue", ok =>
                {
                    if (ok)
                    {
                        Analytics.Log(AnalyticsEvents.ContinueUsed, "level", s.Spec.Key);
                        Levels.ContinueWithExtraMoves(EconomyRules.ContinueMoves);
                        UI.Get<GameplayScreen>().UpdateHearts(s);
                    }
                    else OnOutOfMoves(s);
                })));
            buttons.Add(UIManager.Btn(campaign ? Loc.T("try_again_cost") : Loc.T("try_again"), () =>
            {
                if (campaign) Lives.LoseLife("failed");
                DoRestart();
            }));
            buttons.Add(UIManager.Btn(campaign ? Loc.T("give_up_cost") : Loc.T("home"), () =>
            {
                if (campaign) Lives.LoseLife("failed");
                Analytics.Log(AnalyticsEvents.LevelFail, "level", s.Spec.Key, "reason", "out_of_moves", "seconds", Mathf.RoundToInt(s.Duration));
                UI.CloseAllPopups();
                GoHome();
            }));
            UI.Dialog(Loc.T("out_of_moves"), campaign ? Loc.F("out_of_moves_body", Lives.Lives) : Loc.T("out_of_moves_daily"), buttons.ToArray());
        }

        void OnLevelCompleted(LevelSession s)
        {
            State = GameState.Complete;
            RewardSummary summary = s.IsDaily ? Daily.Complete(s) : Progression.CompleteCampaignLevel(s);
            if (!s.IsDaily) Ads.NotifyLevelCompleted(s.Spec.number);
            Analytics.Log(AnalyticsEvents.LevelComplete, "level", s.Spec.Key, "number", s.Spec.number, "object", s.Spec.objectId,
                "tier", s.Spec.tier.ToString(), "seconds", Mathf.RoundToInt(s.Duration), "taps", s.Taps, "blocked", s.BlockedTaps,
                "hints", s.HintsUsed, "stars", s.Stars, "coins", summary.coins, "daily", s.IsDaily ? 1 : 0);
            Analytics.SetUserProperty("max_level", Save.Data.highestUnlockedLevel.ToString());

            Environment.SetBackground(ColorUtil.Hex("#FFF1C1"), ColorUtil.Hex("#FFD6E8"));
            CameraRig.SetFraming(1.4f, 0.38f);
            UI.ShowPage<LevelCompleteScreen>().Setup(s, summary);
        }

        // ---------------------------------------------------------------- misc hooks

        /// <summary>Popups block gameplay input; closing the last one restores it.</summary>
        public void OnPopupStateChanged()
        {
            bool popup = UI.AnyPopupOpen;
            if (State == GameState.Playing)
            {
                Levels.SetInteractive(!popup);
                if (!popup) Time.timeScale = 1f;
            }
            Input.InputEnabled = !popup;
        }

        public void SettingsChanged()
        {
            Audio.ApplySettings();
            Save.MarkDirty();
        }

        public void SetLanguage(string code)
        {
            Save.Data.settings.language = code;
            Save.MarkDirty();
            Loc.SetLanguage(code);
            UI.RefreshAll();
        }

        public void OnBackAtRoot()
        {
            switch (State)
            {
                case GameState.Home:
#if UNITY_ANDROID && !UNITY_EDITOR
                    // Behave like other Android apps: go to the background instead of killing the process.
                    using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                    using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                        activity.Call<bool>("moveTaskToBack", true);
#endif
                    break;
                case GameState.Playing:
                    PauseGame();
                    break;
                case GameState.Complete:
                    ContinueAfterComplete();
                    break;
                default:
                    GoHome();
                    break;
            }
        }

        void OnApplicationPause(bool paused)
        {
            if (paused && State == GameState.Playing && !UI.AnyPopupOpen) PauseGame();
        }
    }
}
