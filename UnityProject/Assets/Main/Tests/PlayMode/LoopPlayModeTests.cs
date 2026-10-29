using System.Collections;
using NUnit.Framework;
using ProjectJianghu.Core.Application.Flow;
using ProjectJianghu.Game.Presentation;
using ProjectJianghu.Game.Presentation.Arena;
using ProjectJianghu.Game.Presentation.Flow;
using ProjectJianghu.Game.Presentation.Rest;
using ProjectJianghu.Game.Presentation.Steps;
using ProjectJianghu.Game.ResultScene;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using VContainer;

namespace ProjectJianghu.Game.Tests.PlayMode
{
    /// <summary>
    /// The game's loop, played through the real scenes:
    /// Boot → Title → CharacterCreate → Bracket → Rest ⇄ Arena → Result → Title.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the one check the EditMode suites cannot make: that the scenes exist in the build list,
    /// that each scene's scope builds, and that the Run scope created at the title survives every
    /// crossing and is disposed when the title comes back (ADR 0022). It drives the same controls a
    /// player presses, through the views' own public methods.
    /// </para>
    /// <para>
    /// The twenty crossings are the Stage 03 acceptance item: a run that clears twenty rounds must
    /// keep one run, count every round, and leave no arena scope behind — the failure mode being a
    /// scope, a presenter or a session that survives the scene it belonged to.
    /// </para>
    /// <para>
    /// Boot runs its smoke suite first, so the wait is on the Title scene being active rather than on a
    /// frame count. Every wait has a timeout: a scene that never loads fails the test instead of
    /// hanging the run.
    /// </para>
    /// </remarks>
    public sealed class LoopPlayModeTests
    {
        private const float TimeoutSeconds = 60f;

        /// <summary>
        /// Starts each test from an empty shell.
        /// </summary>
        /// <remarks>
        /// Boot moves the composition root into <c>DontDestroyOnLoad</c> and keeps a global parent
        /// override alive, so it outlives the scene load that ends the previous test — and with it the
        /// run that test ended. Without this, the second test would begin on a run that is already over
        /// and the Rest would (correctly) refuse to depart. Destroying the scope also destroys the run
        /// scope parented under it.
        /// </remarks>
        [SetUp]
        public void ResetShell()
        {
            RootLifetimeScope[] scopes = UnityEngine.Object.FindObjectsByType<RootLifetimeScope>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            for (int i = 0; i < scopes.Length; i++)
            {
                if (scopes[i] is null)
                {
                    continue;
                }

                scopes[i].StopPersisting();
                UnityEngine.Object.Destroy(scopes[i].gameObject);
            }
        }

        [UnityTest]
        public IEnumerator The_loop_reaches_the_summary_and_comes_back_to_the_title()
        {
            yield return LoadSceneAsync("Boot");
            yield return WaitForActiveScene("Title", TimeoutSeconds);

            var title = Object.FindFirstObjectByType<TitleSceneDirector>(FindObjectsInactive.Include);
            Assert.That(title, Is.Not.Null, "the Title scene has no TitleSceneDirector");
            Assert.That(title.TryStart(), Is.True);

            var view = Object.FindFirstObjectByType<TitleView>(FindObjectsInactive.Include);
            Assert.That(view, Is.Not.Null, "the Title scene has no TitleView");
            view.PressStart();

            yield return WaitForActiveScene("CharacterCreate", TimeoutSeconds);

            RunScopeOwner owner = Object.FindFirstObjectByType<RootLifetimeScope>(FindObjectsInactive.Include)
                .Container.Resolve<RunScopeOwner>();
            Assert.That(owner.HasRun, Is.True, "no run survived the move into character creation");
            Assert.That(owner.Current.EncounterIndex, Is.EqualTo(0));

            PressContinue<StepSceneDirector>("CharacterCreate");
            yield return WaitForActiveScene("Bracket", TimeoutSeconds);

            PressContinue<StepSceneDirector>("Bracket");
            yield return WaitForActiveScene("Rest", TimeoutSeconds);

            RestView rest = Require<RestView>("Rest");
            rest.PressDepart();
            yield return WaitForActiveScene("Arena", TimeoutSeconds);

            Assert.That(owner.HasRun, Is.True, "the run did not survive the move into the arena");
            Assert.That(owner.Current.IsInEncounter, Is.True);

            Require<ArenaView>("Arena").PressWin();
            yield return WaitForActiveScene("Rest", TimeoutSeconds);
            Assert.That(owner.Current.EncounterIndex, Is.EqualTo(1), "a won round was not counted");
            Assert.That(FirstOrNull<ArenaSceneScope>(), Is.Null, "the arena scope outlived its scene");

            Require<RestView>("Rest").PressFinish();
            yield return WaitForActiveScene("Result", TimeoutSeconds);

            var result = Object.FindFirstObjectByType<ResultSceneDirector>(FindObjectsInactive.Include);
            Assert.That(result, Is.Not.Null, "the Result scene has no ResultSceneDirector");
            Assert.That(result.Started, Is.True, "the summary did not start: " + result.StartError);

            Require<ResultView>("Result").PressTitle();
            yield return WaitForActiveScene("Title", TimeoutSeconds);

            Assert.That(owner.HasRun, Is.False, "arriving at the title must dispose the run scope");
        }

        [UnityTest]
        public IEnumerator Twenty_rest_arena_crossings_keep_one_run_and_count_every_round()
        {
            yield return LoadSceneAsync("Boot");
            yield return WaitForActiveScene("Title", TimeoutSeconds);

            Require<TitleView>("Title").PressStart();
            yield return WaitForActiveScene("CharacterCreate", TimeoutSeconds);

            PressContinue<StepSceneDirector>("CharacterCreate");
            yield return WaitForActiveScene("Bracket", TimeoutSeconds);

            PressContinue<StepSceneDirector>("Bracket");
            yield return WaitForActiveScene("Rest", TimeoutSeconds);

            RunScopeOwner owner = Object.FindFirstObjectByType<RootLifetimeScope>(FindObjectsInactive.Include)
                .Container.Resolve<RunScopeOwner>();
            uint startingRun = owner.Current.Seed;

            for (int round = 1; round <= 20; round++)
            {
                Require<RestView>("Rest").PressDepart();
                yield return WaitForActiveScene("Arena", TimeoutSeconds);

                Assert.That(owner.HasRun, Is.True, "the run was disposed by crossing " + round);
                Assert.That(owner.Current.Seed, Is.EqualTo(startingRun), "a crossing replaced the run");
                Assert.That(owner.Current.IsInEncounter, Is.True, "round " + round + " has no encounter");

                Require<ArenaView>("Arena").PressWin();
                yield return WaitForActiveScene("Rest", TimeoutSeconds);

                Assert.That(
                    owner.Current.EncounterIndex, Is.EqualTo(round),
                    "after round " + round + " the run counted " + owner.Current.EncounterIndex);
                Assert.That(
                    FirstOrNull<ArenaSceneScope>(), Is.Null,
                    "the arena scope outlived round " + round);
            }

            Require<RestView>("Rest").PressFinish();
            yield return WaitForActiveScene("Result", TimeoutSeconds);

            var result = Object.FindFirstObjectByType<ResultSceneDirector>(FindObjectsInactive.Include);
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Started, Is.True, result.StartError);
            Assert.That(result.Run.EncounterIndex, Is.EqualTo(20));

            Require<ResultView>("Result").PressTitle();
            yield return WaitForActiveScene("Title", TimeoutSeconds);
            Assert.That(owner.HasRun, Is.False);
        }

        [UnityTest]
        public IEnumerator A_lost_encounter_ends_the_run_on_the_summary()
        {
            yield return LoadSceneAsync("Boot");
            yield return WaitForActiveScene("Title", TimeoutSeconds);

            Require<TitleView>("Title").PressStart();
            yield return WaitForActiveScene("CharacterCreate", TimeoutSeconds);

            PressContinue<StepSceneDirector>("CharacterCreate");
            yield return WaitForActiveScene("Bracket", TimeoutSeconds);

            PressContinue<StepSceneDirector>("Bracket");
            yield return WaitForActiveScene("Rest", TimeoutSeconds);

            Require<RestView>("Rest").PressDepart();
            yield return WaitForActiveScene("Arena", TimeoutSeconds);

            Require<ArenaView>("Arena").PressLose();
            yield return WaitForActiveScene("Result", TimeoutSeconds);

            var director = Object.FindFirstObjectByType<ResultSceneDirector>(FindObjectsInactive.Include);
            Assert.That(director, Is.Not.Null);
            Assert.That(director.Started, Is.True, director.StartError);
            Assert.That(director.Run.IsFinished, Is.True);
        }

        private static void PressContinue<TDirector>(string sceneName) where TDirector : Component
        {
            var director = Object.FindFirstObjectByType<TDirector>(FindObjectsInactive.Include);
            Assert.That(director, Is.Not.Null, "the " + sceneName + " scene has no " + typeof(TDirector).Name);

            var view = Object.FindFirstObjectByType<StepView>(FindObjectsInactive.Include);
            Assert.That(view, Is.Not.Null, "the " + sceneName + " scene has no StepView");
            view.PressContinue();
        }

        private static T Require<T>(string sceneName) where T : Component
        {
            var found = Object.FindFirstObjectByType<T>(FindObjectsInactive.Include);
            Assert.That(found, Is.Not.Null, "the " + sceneName + " scene has no " + typeof(T).Name);
            return found;
        }

        private static T FirstOrNull<T>() where T : Component =>
            Object.FindFirstObjectByType<T>(FindObjectsInactive.Include);

        private static IEnumerator LoadSceneAsync(string sceneName)
        {
            AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            Assert.That(operation, Is.Not.Null, "the scene '" + sceneName + "' is not in the build settings");

            while (!operation.isDone)
            {
                yield return null;
            }

            // One frame for the new scene's Start to run before anyone looks for its views.
            yield return null;
        }

        private static IEnumerator WaitForActiveScene(string sceneName, float timeoutSeconds)
        {
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (SceneManager.GetActiveScene().name != sceneName)
            {
                if (Time.realtimeSinceStartup > deadline)
                {
                    Assert.Fail(
                        "the '" + sceneName + "' scene never became active; on '" +
                        SceneManager.GetActiveScene().name + "'");
                }

                yield return null;
            }

            yield return null;
        }
    }
}
