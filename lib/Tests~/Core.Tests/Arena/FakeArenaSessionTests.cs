using System.Collections.Generic;
using ProjectJianghu.Core.Application.Arena.Fake;
using ProjectJianghu.Core.Domain.Arena;
using Xunit;

namespace ProjectJianghu.Core.Tests.Arena
{
    /// <summary>
    /// The parts of the fake that are not the contract: the scripted endings the suite
    /// uses to stage outcomes, and the seed behaviour of the headless resolver.
    /// </summary>
    public sealed class FakeArenaSessionTests
    {
        [Fact]
        public void WinAtTick_ends_as_a_victory_without_any_fighting()
        {
            var factory = new FakeArenaSessionFactory(new FakeArenaOptions { WinAtTick = 5 });
            using IArenaSession session = factory.Create(
                ArenaTestSetup.Create(playerFillTicks: 500, enemyFillTicks: 500));

            RunToRunning(session);
            StepUntilFinished(session);

            Assert.True(session.TryGetResult(out ArenaMatchResult result));
            Assert.Equal(ArenaOutcome.Victory, result.Outcome);
            Assert.Equal(ArenaEndReason.AllEnemiesDefeated, result.Reason);
            Assert.Equal(5, result.DurationTicks);
        }

        [Fact]
        public void LoseAtTick_ends_as_a_defeat()
        {
            var factory = new FakeArenaSessionFactory(new FakeArenaOptions { LoseAtTick = 3 });
            using IArenaSession session = factory.Create(
                ArenaTestSetup.Create(playerFillTicks: 500, enemyFillTicks: 500));

            RunToRunning(session);
            StepUntilFinished(session);

            Assert.True(session.TryGetResult(out ArenaMatchResult result));
            Assert.Equal(ArenaOutcome.Defeat, result.Outcome);
            Assert.Equal(ArenaEndReason.AllAlliesDefeated, result.Reason);
            Assert.Equal(3, result.DurationTicks);
        }

        [Fact]
        public void TimeoutAtTick_resolves_by_hp_percentage()
        {
            var factory = new FakeArenaSessionFactory(new FakeArenaOptions { TimeoutAtTick = 2 });
            using IArenaSession session = factory.Create(ArenaTestSetup.Create(
                playerFillTicks: 500,
                enemyFillTicks: 500,
                playerStartHp: 900,
                enemyStartHp: 100));

            RunToRunning(session);
            StepUntilFinished(session);

            Assert.True(session.TryGetResult(out ArenaMatchResult result));
            Assert.Equal(ArenaOutcome.Victory, result.Outcome);
            Assert.Equal(ArenaEndReason.TimeoutHpRatio, result.Reason);
        }

        [Fact]
        public void AutoResolver_is_reproducible_for_one_seed_and_varies_by_seed()
        {
            ArenaMatchSetup setup = ArenaTestSetup.Create(
                playerHp: 100,
                enemyHp: 100,
                damage: 10,
                skillRange: 4,
                playerFillTicks: 20,
                enemyFillTicks: 20,
                timeLimitTicks: 0,
                seed: 7);

            var resolver = new FakeAutoResolver();

            ArenaMatchResult first = resolver.Resolve(setup, 5000);
            ArenaMatchResult second = resolver.Resolve(setup, 5000);

            Assert.Equal(first.Outcome, second.Outcome);
            Assert.Equal(first.Reason, second.Reason);
            Assert.Equal(first.DurationTicks, second.DurationTicks);
            Assert.Equal(first.Fighters.Count, second.Fighters.Count);
            for (int i = 0; i < first.Fighters.Count; i++)
            {
                Assert.Equal(first.Fighters[i].Id, second.Fighters[i].Id);
                Assert.Equal(first.Fighters[i].FinalHp, second.Fighters[i].FinalHp);
                Assert.Equal(first.Fighters[i].DamageDealt, second.Fighters[i].DamageDealt);
            }

            var digests = new HashSet<string>();
            for (ulong seed = 1; seed <= 20; seed++)
            {
                setup.Seed = seed;
                ArenaMatchResult resolved = resolver.Resolve(setup, 5000);
                digests.Add(
                    resolved.Outcome + "/" + resolved.Reason + "/" + resolved.DurationTicks + "/"
                    + resolved.Fighters[0].FinalHp + "/" + resolved.Fighters[1].FinalHp);
            }

            Assert.True(digests.Count > 1, "every seed resolved to the same match");
        }

        private static void RunToRunning(IArenaSession session)
        {
            for (int i = 0; i < ArenaConstants.IntroTicks + 10 && session.Phase == ArenaPhase.Intro; i++)
            {
                session.Step();
            }

            Assert.Equal(ArenaPhase.Running, session.Phase);
        }

        private static void StepUntilFinished(IArenaSession session)
        {
            for (int i = 0; i < 100 && session.Phase != ArenaPhase.Finished; i++)
            {
                session.Step();
            }

            Assert.Equal(ArenaPhase.Finished, session.Phase);
        }
    }
}
