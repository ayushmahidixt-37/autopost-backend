using System;
using NUnit.Framework;
using ShapeEscape.Core;

namespace ShapeEscape.Tests
{
    public sealed class EconomyTests
    {
        static readonly DateTime T0 = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

        [Test]
        public void LosingALifeFromFullStartsTheRegenClock()
        {
            var s = new LivesState(EconomyRules.MaxLives, 0).Lose(T0);
            Assert.AreEqual(EconomyRules.MaxLives - 1, s.lives);
            Assert.AreEqual(T0.Ticks, s.regenStartTicks);
            Assert.AreEqual(EconomyRules.LifeRegen, s.TimeToNext(T0));
        }

        [Test]
        public void LivesRefillOneEveryThirtyMinutesUpToMax()
        {
            var s = new LivesState(EconomyRules.MaxLives, 0).Lose(T0).Lose(T0.AddMinutes(10));
            Assert.AreEqual(3, s.lives);
            var later = s.Sync(T0.AddMinutes(31));
            Assert.AreEqual(4, later.lives);
            Assert.AreEqual(T0.AddMinutes(30).Ticks, later.regenStartTicks, "clock carries over to the next life");
            var full = s.Sync(T0.AddHours(5));
            Assert.AreEqual(EconomyRules.MaxLives, full.lives);
            Assert.AreEqual(0, full.regenStartTicks);
        }

        [Test]
        public void LivesNeverExceedMaxOrDropBelowZero()
        {
            Assert.AreEqual(EconomyRules.MaxLives, new LivesState(0, T0.Ticks).Add(99, T0).lives);
            Assert.AreEqual(0, new LivesState(0, T0.Ticks).Lose(T0).lives);
        }

        [Test]
        public void PracticeLevelsHaveNoMoveLimit()
        {
            Assert.AreEqual(int.MaxValue, EconomyRules.MistakesFor(LevelCatalog.Get(1)));
            Assert.AreEqual(int.MaxValue, EconomyRules.MistakesFor(LevelCatalog.Get(EconomyRules.PracticeLevels)));
            Assert.AreEqual(EconomyRules.MistakesPerLevel, EconomyRules.MistakesFor(LevelCatalog.Get(EconomyRules.PracticeLevels + 1)));
        }

        [Test]
        public void HardLevelsPayMore()
        {
            Assert.AreEqual(1f, EconomyRules.CoinMultiplier(LevelCatalog.Get(11)));
            Assert.AreEqual(1.5f, EconomyRules.CoinMultiplier(LevelCatalog.Get(10)));
            Assert.AreEqual(2f, EconomyRules.CoinMultiplier(LevelCatalog.Get(30)));
        }
    }
}
