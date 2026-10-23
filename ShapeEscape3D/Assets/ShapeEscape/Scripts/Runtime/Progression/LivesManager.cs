using System;
using ShapeEscape.Core;
using UnityEngine;

namespace ShapeEscape
{
    /// <summary>
    /// Candy-Crush-style lives: up to 5, one refills every 30 minutes (wall-clock, so it keeps counting while the
    /// app is closed). A failed campaign level, or leaving one after moving, costs a life. Rules live in
    /// <see cref="LivesState"/> (unit tested in the core).
    /// </summary>
    public sealed class LivesManager : MonoBehaviour
    {
        public event Action<int> LivesChanged;

        GameManager _game;
        int _lastBroadcast = -1;

        public void Initialize(GameManager game)
        {
            _game = game;
            Sync();
        }

        LivesState State
        {
            get { return new LivesState(_game.Save.Data.lives, _game.Save.Data.livesRegenStartTicks); }
            set
            {
                var d = _game.Save.Data;
                if (d.lives == value.lives && d.livesRegenStartTicks == value.regenStartTicks) return;
                d.lives = value.lives;
                d.livesRegenStartTicks = value.regenStartTicks;
                _game.Save.MarkDirty();
            }
        }

        public int Lives { get { Sync(); return _game.Save.Data.lives; } }
        public bool IsFull { get { return Lives >= EconomyRules.MaxLives; } }
        public TimeSpan TimeToNext { get { return State.TimeToNext(DateTime.UtcNow); } }

        public void Sync()
        {
            State = State.Sync(DateTime.UtcNow);
            Broadcast();
        }

        public void LoseLife(string reason)
        {
            State = State.Lose(DateTime.UtcNow);
            _game.Analytics.Log(AnalyticsEvents.LifeLost, "reason", reason, "lives", _game.Save.Data.lives);
            Broadcast();
        }

        public void AddLives(int amount, string source)
        {
            State = State.Add(amount, DateTime.UtcNow);
            _game.Analytics.Log(AnalyticsEvents.LifeGained, "amount", amount, "source", source, "lives", _game.Save.Data.lives);
            Broadcast();
        }

        public bool RefillWithCoins()
        {
            if (IsFull || !_game.Progression.SpendCoins(EconomyRules.LivesRefillCost, "lives_refill")) return false;
            AddLives(EconomyRules.MaxLives, "coins");
            return true;
        }

        float _timer;

        void Update()
        {
            _timer -= Time.unscaledDeltaTime;
            if (_timer > 0) return;
            _timer = 1f;
            Sync();
        }

        void Broadcast()
        {
            int lives = _game.Save.Data.lives;
            if (lives == _lastBroadcast) return;
            _lastBroadcast = lives;
            if (LivesChanged != null) LivesChanged(lives);
        }

        public static string FormatCountdown(TimeSpan t)
        {
            int s = Mathf.CeilToInt((float)t.TotalSeconds);
            return (s / 60) + ":" + (s % 60).ToString("00");
        }
    }

    /// <summary>
    /// Social layer for sending and requesting lives. The shipped implementation is a no-op; plug in Facebook
    /// (Gaming Services / Graph API) or Google Play Games friends plus a small backend (e.g. Firebase Firestore:
    /// players/{id} with gifts/asks maps, as in the web demo) and register it via GameManager.Friends.SetProvider.
    /// While unavailable, the UI hides every friends control.
    /// </summary>
    public interface IFriendsProvider
    {
        bool IsAvailable { get; }
        void AskForLives(Action<bool> done);
        void SendLife(string friendId, Action<bool> done);
    }

    public sealed class NullFriendsProvider : IFriendsProvider
    {
        public bool IsAvailable { get { return false; } }
        public void AskForLives(Action<bool> done) { if (done != null) done(false); }
        public void SendLife(string friendId, Action<bool> done) { if (done != null) done(false); }
    }

    public sealed class FriendsManager : MonoBehaviour
    {
        IFriendsProvider _provider = new NullFriendsProvider();
        public bool IsAvailable { get { return _provider.IsAvailable; } }
        public void SetProvider(IFriendsProvider provider) { _provider = provider ?? new NullFriendsProvider(); }
        public void AskForLives(Action<bool> done) { _provider.AskForLives(done); }
        public void SendLife(string friendId, Action<bool> done) { _provider.SendLife(friendId, done); }
    }
}
