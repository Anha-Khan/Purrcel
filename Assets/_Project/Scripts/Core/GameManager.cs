using System;
using UnityEngine;

namespace CatCourier.Core
{
    public sealed class GameManager : MonoBehaviour
    {
        private const float ContinueOfferSeconds = 5f;

        public static GameManager Instance { get; private set; }
        public GameState State { get; private set; } = GameState.Hub;
        public RunResult LastRun { get; private set; }
        public int ContinuesLeft { get; private set; }
        public bool IsContinueOfferActive => continuePending;
        public bool IsStartPending { get; private set; }

        public event Action<GameState> OnStateChanged;
        public event Action<float> OnContinueOffered;

        private float continueDeadline;
        private bool continuePending;
        private bool hasSceneLoader;
        private string requestedScene;
        private GameState stateBeforeSceneLoad;
        private bool hasPendingRunResult;
        private RunResult pendingRunResult;
        private int continueCap;

        private void Awake()
        {
            if (!ClaimSingleton())
            {
                return;
            }

            PersistIfRoot();
        }

        private void PersistIfRoot()
        {
            // Managers are parented under PersistentSystems, which is already DontDestroyOnLoad.
            // Calling it on a child logs a warning and does nothing.
            if (transform.parent == null)
            {
                DontDestroyOnLoad(gameObject);
            }
        }

        private bool ClaimSingleton()
        {
            if (Instance != null && Instance != this)
            {
                if (Application.isPlaying)
                {
                    Destroy(gameObject);
                }
                else
                {
                    DestroyImmediate(gameObject);
                }
                return false;
            }

            Instance = this;
            return true;
        }

        private void Update()
        {
            if (IsStartPending)
            {
                var revenueCat = Monetization.RevenueCatManager.Instance;
                if (revenueCat == null || revenueCat.IsReady)
                {
                    IsStartPending = false;
                    StartRun();
                    return;
                }
            }

            if (!continuePending)
            {
                return;
            }

            var remaining = continueDeadline - Time.unscaledTime;
            OnContinueOffered?.Invoke(remaining);
            if (remaining <= 0f)
            {
                continuePending = false;
                DeclineContinue();
            }
        }

        public void StartRun()
        {
            if (State == GameState.Running || State == GameState.Paused || hasPendingRunResult)
            {
                return;
            }

            if (SceneLoader.Instance == null)
            {
                return;
            }

            var revenueCat = Monetization.RevenueCatManager.Instance;
            if (revenueCat != null && !revenueCat.IsReady)
            {
                IsStartPending = true;
                return;
            }

            IsStartPending = false;

            Time.timeScale = 1f;
            ResetContinuesForRun(Monetization.EntitlementChecker.Instance?.IsPremium ?? false);
            stateBeforeSceneLoad = State;
            requestedScene = SceneNames.Game;
            hasSceneLoader = true;
            SetState(GameState.Running);
            SceneLoader.Instance.Load(SceneNames.Game);
        }

        public void PauseRun()
        {
            if (State == GameState.Running)
            {
                Time.timeScale = 0f;
                SetState(GameState.Paused);
            }
        }

        public void ResumeRun()
        {
            if (State == GameState.Paused)
            {
                Time.timeScale = 1f;
                SetState(GameState.Running);
            }
        }

        public void EndRun(RunResult result)
        {
            Time.timeScale = 1f;
            if (hasPendingRunResult)
            {
                return;
            }

            LastRun = result;
            pendingRunResult = result;
            hasPendingRunResult = true;
            SetState(GameState.Dead);

            if (ContinuesLeft > 0)
            {
                continuePending = true;
                continueDeadline = Time.unscaledTime + ContinueOfferSeconds;
                OnContinueOffered?.Invoke(ContinueOfferSeconds);
            }
            else
            {
                FinalizePendingRun();
            }
        }

        public bool UseContinue()
        {
            if (!continuePending || ContinuesLeft <= 0)
            {
                return false;
            }

            continuePending = false;
            hasPendingRunResult = false;
            ContinuesLeft--;
            SetState(GameState.Running);
            RunCoordinator.Active?.RespawnFromContinue();
            return true;
        }

        public void DeclineContinue()
        {
            continuePending = false;
            FinalizePendingRun();
        }

        public void GrantContinue()
        {
            if (ContinuesLeft < continueCap)
            {
                ContinuesLeft++;
            }
        }

        public void ReturnToHub()
        {
            if (State == GameState.Dead && !FinalizePendingRun())
            {
                return;
            }

            if (SceneLoader.Instance == null)
            {
                return;
            }

            continuePending = false;
            Time.timeScale = 1f;
            stateBeforeSceneLoad = State;
            requestedScene = SceneNames.Hub;
            hasSceneLoader = true;
            SetState(GameState.Hub);
            SceneLoader.Instance.Load(SceneNames.Hub);
        }

        public void ResetContinuesForRun(bool premium)
        {
            continueCap = premium ? 3 : 1;
            ContinuesLeft = continueCap;
        }

        public void HandleSceneLoadFailed(string sceneName)
        {
            if (!hasSceneLoader)
            {
                return;
            }

            hasSceneLoader = false;
            if (sceneName != requestedScene)
            {
                return;
            }

            requestedScene = null;
            SetState(sceneName == SceneNames.Game ? GameState.Hub : stateBeforeSceneLoad);
        }

        private bool FinalizePendingRun()
        {
            if (!hasPendingRunResult)
            {
                return true;
            }

            var saveSystem = SaveSystem.Instance;
            if (saveSystem == null)
            {
                hasPendingRunResult = false;
                return true;
            }

            if (!saveSystem.RecordCompletedRun(pendingRunResult))
            {
                return false;
            }

            hasPendingRunResult = false;
            Monetization.PaywallGate.NotifyRunCompleted();
            return true;
        }

        private void SetState(GameState state)
        {
            if (State == state)
            {
                return;
            }

            State = state;
            OnStateChanged?.Invoke(State);
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Time.timeScale = 1f;
                Instance = null;
            }
        }
    }
}
