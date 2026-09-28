using System.Reflection;
using CatCourier.Core;
using NUnit.Framework;
using UnityEngine;

namespace CatCourier.Tests
{
    public sealed class GameManagerTests
    {
        [Test]
        public void FailedGameSceneLoad_ReturnsStateToHub()
        {
            var host = new GameObject("GameManagerFailureTest");
            var loaderHost = new GameObject("SceneLoaderFailureTest");
            try
            {
                var manager = host.AddComponent<GameManager>();
                var loader = loaderHost.AddComponent<SceneLoader>();
                InvokeWithoutPersistence(manager, "ClaimSingleton");
                InvokeWithoutPersistence(loader, "ClaimSingleton");
                manager.StartRun();
                Assert.That(manager.State, Is.EqualTo(GameState.Running));
                manager.HandleSceneLoadFailed(SceneNames.Game);
                Assert.That(manager.State, Is.EqualTo(GameState.Hub));
            }
            finally
            {
                Object.DestroyImmediate(loaderHost);
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void DuplicateInstance_DoesNotReplaceSingleton()
        {
            var firstHost = new GameObject("GameManagerFirst");
            var secondHost = new GameObject("GameManagerSecond");
            try
            {
                var first = firstHost.AddComponent<GameManager>();
                var second = secondHost.AddComponent<GameManager>();
                var firstClaimed = (bool)InvokeWithoutPersistence(first, "ClaimSingleton");
                var secondClaimed = (bool)InvokeWithoutPersistence(second, "ClaimSingleton");
                Assert.That(firstClaimed, Is.True);
                Assert.That(secondClaimed, Is.False);
                Assert.That(GameManager.Instance, Is.SameAs(first));
            }
            finally
            {
                Object.DestroyImmediate(secondHost);
                Object.DestroyImmediate(firstHost);
            }
        }

        [Test]
        public void State_TransitionsHubToRunningToDeadToHub()
        {
            var host = new GameObject("GameManagerSequenceTest");
            var loaderHost = new GameObject("SceneLoaderSequenceTest");
            try
            {
                var manager = host.AddComponent<GameManager>();
                var loader = loaderHost.AddComponent<SceneLoader>();
                InvokeWithoutPersistence(manager, "ClaimSingleton");
                InvokeWithoutPersistence(loader, "ClaimSingleton");

                manager.StartRun();
                Assert.That(manager.State, Is.EqualTo(GameState.Running));
                manager.EndRun(default);
                Assert.That(manager.State, Is.EqualTo(GameState.Dead));
                manager.ReturnToHub();
                Assert.That(manager.State, Is.EqualTo(GameState.Hub));
            }
            finally
            {
                Object.DestroyImmediate(loaderHost);
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void FailedHubSceneLoad_RestoresDeadState()
        {
            var host = new GameObject("GameManagerHubFailureTest");
            var loaderHost = new GameObject("SceneLoaderHubFailureTest");
            try
            {
                var manager = host.AddComponent<GameManager>();
                var loader = loaderHost.AddComponent<SceneLoader>();
                InvokeWithoutPersistence(manager, "ClaimSingleton");
                InvokeWithoutPersistence(loader, "ClaimSingleton");
                manager.StartRun();
                manager.EndRun(default);
                manager.ReturnToHub();
                manager.HandleSceneLoadFailed(SceneNames.Hub);
                Assert.That(manager.State, Is.EqualTo(GameState.Dead));
            }
            finally
            {
                Object.DestroyImmediate(loaderHost);
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void StartRun_ResetsContinueBalanceAndEnforcesFreeCap()
        {
            var host = new GameObject("GameManagerContinueQuotaTest");
            var loaderHost = new GameObject("SceneLoaderContinueQuotaTest");
            try
            {
                var manager = host.AddComponent<GameManager>();
                var loader = loaderHost.AddComponent<SceneLoader>();
                InvokeWithoutPersistence(manager, "ClaimSingleton");
                InvokeWithoutPersistence(loader, "ClaimSingleton");
                manager.GrantContinue();
                manager.StartRun();
                Assert.That(manager.ContinuesLeft, Is.EqualTo(1));
                manager.GrantContinue();
                manager.GrantContinue();
                Assert.That(manager.ContinuesLeft, Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(loaderHost);
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void ContinueUseAndDecline_LeaveExpectedStates()
        {
            var host = new GameObject("GameManagerContinueFlowTest");
            var loaderHost = new GameObject("SceneLoaderContinueFlowTest");
            try
            {
                var manager = host.AddComponent<GameManager>();
                var loader = loaderHost.AddComponent<SceneLoader>();
                InvokeWithoutPersistence(manager, "ClaimSingleton");
                InvokeWithoutPersistence(loader, "ClaimSingleton");
                manager.StartRun();
                manager.GrantContinue();
                manager.EndRun(default);
                Assert.That(manager.State, Is.EqualTo(GameState.Dead));
                Assert.That(manager.UseContinue(), Is.True);
                Assert.That(manager.State, Is.EqualTo(GameState.Running));

                manager.EndRun(default);
                manager.DeclineContinue();
                Assert.That(manager.State, Is.EqualTo(GameState.Dead));
            }
            finally
            {
                Object.DestroyImmediate(loaderHost);
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void State_TransitionsRunningToPausedToRunning()
        {
            var host = new GameObject("GameManagerTests");
            var loaderHost = new GameObject("SceneLoaderTests");
            try
            {
                var manager = host.AddComponent<GameManager>();
                var loader = loaderHost.AddComponent<SceneLoader>();
                InvokeWithoutPersistence(manager, "ClaimSingleton");
                InvokeWithoutPersistence(loader, "ClaimSingleton");
                manager.StartRun();
                Assert.That(manager.State, Is.EqualTo(GameState.Running));

                manager.PauseRun();
                Assert.That(manager.State, Is.EqualTo(GameState.Paused));
                Assert.That(Time.timeScale, Is.Zero);

                manager.ResumeRun();
                Assert.That(manager.State, Is.EqualTo(GameState.Running));
                Assert.That(Time.timeScale, Is.EqualTo(1f));
            }
            finally
            {
                Object.DestroyImmediate(loaderHost);
                Object.DestroyImmediate(host);
            }
        }

        private static object InvokeWithoutPersistence(MonoBehaviour component, string methodName)
        {
            var method = component.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            return method.Invoke(component, null);
        }
    }
}
