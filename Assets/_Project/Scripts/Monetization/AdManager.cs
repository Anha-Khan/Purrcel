using System;
using CatCourier.Core;
using UnityEngine;

namespace CatCourier.Monetization
{
    public interface IAdBackend
    {
        void ShowInterstitial(string placementId, Action onClosed);
        void ShowRewarded(string placementId, Action<bool> onDone);
    }

    public enum AdFlowState
    {
        Idle,
        Showing,
        RewardGranted,
        NotEligible,
        Skipped,
        Failed
    }

    public sealed class AdManager : MonoBehaviour
    {
        public static AdManager Instance { get; private set; }

        [Tooltip("RevenueCat Ads has no Unity ad-serving SDK (docs/revenuecat-spike.md). The fake backend keeps ad flows usable until a real one exists.")]
        [SerializeField] private bool useFakeBackend = true;

        [Tooltip("Whether premium accounts may see rewarded ads. Unapproved business rule; default keeps 'premium sees no ads' true.")]
        [SerializeField] private bool premiumSeesRewardedAds;

        [Tooltip("Seconds an ad request may stay open before the flow is failed and released.")]
        [SerializeField] private float requestTimeoutSeconds = 10f;

        public bool ShouldShowAds => EntitlementChecker.Instance == null || !EntitlementChecker.Instance.IsPremium;

        public bool HasBackend => backend != null;
        public bool IsBusy { get; private set; }
        public bool InterstitialShownThisRun { get; private set; }
        public bool RewardGrantedThisRun { get; private set; }
        public AdFlowState State { get; private set; } = AdFlowState.Idle;
        public string StatusMessage { get; private set; } = string.Empty;

        public event Action OnStateChanged;

        private IAdBackend backend;
        private GameManager gameManager;
        private Action releaseAction;
        private float busyDeadline;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            PersistIfRoot();
            if (useFakeBackend)
            {
                backend = new FakeAdBackend();
            }
        }

        private void Update()
        {
            TrackGameManager();
            ReleaseTimedOutRequest();
        }

        private void PersistIfRoot()
        {
            // Managers are parented under PersistentSystems, which is already DontDestroyOnLoad.
            if (transform.parent == null)
            {
                DontDestroyOnLoad(gameObject);
            }
        }

        public void SetBackend(IAdBackend value)
        {
            backend = value;
        }

        public void ResetForNewRun()
        {
            InterstitialShownThisRun = false;
            RewardGrantedThisRun = false;
            ClearInFlight();
            SetState(AdFlowState.Idle, string.Empty);
        }

        public void ShowInterstitial(string placementId, Action onClosed)
        {
            var completion = new OnceAction(onClosed);
            if (string.IsNullOrWhiteSpace(placementId))
            {
                FailAndRelease(completion, "Missing placement id.");
                return;
            }

            if (!TryBegin(out var failMessage))
            {
                if (IsBusy)
                {
                    SetState(AdFlowState.NotEligible, failMessage);
                    completion.Invoke();
                }
                else
                {
                    FailAndRelease(completion, failMessage);
                }

                return;
            }

            SetState(AdFlowState.Showing, placementId);
            releaseAction = () => completion.Invoke();
            try
            {
                backend.ShowInterstitial(placementId, () =>
                {
                    if (!completion.TryClaim(out var closed))
                    {
                        return;
                    }

                    ClearInFlight();
                    SetState(AdFlowState.Idle, "Interstitial closed.");
                    closed();
                });
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Interstitial backend failed. {exception.GetType().Name}");
                FailAndRelease(completion, "Interstitial failed to load.");
            }
        }

        public void ShowRewarded(string placementId, Action<bool> onDone)
        {
            var completion = new OnceAction<bool>(onDone);
            if (string.IsNullOrWhiteSpace(placementId))
            {
                FailAndRelease(completion, "Missing placement id.");
                return;
            }

            if (!TryBegin(out var failMessage))
            {
                if (IsBusy)
                {
                    SetState(AdFlowState.NotEligible, failMessage);
                    completion.Invoke(false);
                }
                else
                {
                    FailAndRelease(completion, failMessage);
                }

                return;
            }

            SetState(AdFlowState.Showing, placementId);
            releaseAction = () => completion.Invoke(false);
            try
            {
                backend.ShowRewarded(placementId, completed =>
                {
                    if (!completion.TryClaim(out var settle))
                    {
                        return;
                    }

                    ClearInFlight();
                    if (!completed)
                    {
                        SetState(AdFlowState.Skipped, "Rewarded ad was skipped or failed.");
                        settle(false);
                        return;
                    }

                    settle(true);
                });
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Rewarded backend failed. {exception.GetType().Name}");
                FailAndRelease(completion, "Rewarded ad failed to load.");
            }
        }

        public void ShowDeathInterstitial(Action onClosed)
        {
            var completion = new OnceAction(onClosed);
            if (!ShouldShowAds)
            {
                NotEligible(completion, "Premium: ads are disabled.");
                return;
            }

            if (InterstitialShownThisRun)
            {
                NotEligible(completion, "Interstitial already shown this run.");
                return;
            }

            InterstitialShownThisRun = true;
            ShowInterstitial(Constants.AD_PLACEMENT_DEATH, () =>
            {
                if (!completion.TryClaim(out var closed))
                {
                    return;
                }

                closed();
            });
        }

        public void ShowContinueRewarded(Action<bool> onRewardGranted)
        {
            var completion = new OnceAction<bool>(onRewardGranted);
            var checker = EntitlementChecker.Instance;
            if (checker != null && checker.IsPremium && !premiumSeesRewardedAds)
            {
                NotEligible(completion, "Premium: ads are disabled.");
                return;
            }

            if (RewardGrantedThisRun)
            {
                NotEligible(completion, "Continue reward already granted this run.");
                return;
            }

            var continuesBefore = GameManager.Instance?.ContinuesLeft ?? 0;
            ShowRewarded(Constants.AD_PLACEMENT_CONTINUE, completed =>
            {
                if (!completion.TryClaim(out var settle))
                {
                    return;
                }

                if (!completed)
                {
                    settle(false);
                    return;
                }

                settle(GrantContinueReward(continuesBefore));
            });
        }

        private bool GrantContinueReward(int continuesBefore)
        {
            var game = GameManager.Instance;
            if (game == null || game.State != GameState.Dead || !game.IsContinueOfferActive)
            {
                SetState(AdFlowState.NotEligible, "No continue decision is active.");
                return false;
            }

            game.GrantContinue();
            if (game.ContinuesLeft <= continuesBefore)
            {
                SetState(AdFlowState.NotEligible, "Continue is already at the run limit.");
                return false;
            }

            RewardGrantedThisRun = true;
            SetState(AdFlowState.RewardGranted, "Continue granted.");
            return true;
        }

        private bool TryBegin(out string failMessage)
        {
            if (backend == null)
            {
                failMessage = "No ad backend is configured.";
                return false;
            }

            if (IsBusy)
            {
                failMessage = "Another ad is already showing.";
                return false;
            }

            IsBusy = true;
            busyDeadline = Time.unscaledTime + Mathf.Max(1f, requestTimeoutSeconds);
            failMessage = string.Empty;
            return true;
        }

        private void ReleaseTimedOutRequest()
        {
            if (!IsBusy || releaseAction == null || Time.unscaledTime < busyDeadline)
            {
                return;
            }

            var release = releaseAction;
            ClearInFlight();
            SetState(AdFlowState.Failed, "Ad did not report completion in time.");
            release();
        }

        private void NotEligible(OnceAction completion, string message)
        {
            SetState(AdFlowState.NotEligible, message);
            completion.Invoke();
        }

        private void NotEligible<T>(OnceAction<T> completion, string message)
        {
            SetState(AdFlowState.NotEligible, message);
            completion.Invoke(default);
        }

        private void FailAndRelease(OnceAction completion, string message)
        {
            ClearInFlight();
            SetState(AdFlowState.Failed, message);
            completion.Invoke();
        }

        private void FailAndRelease<T>(OnceAction<T> completion, string message)
        {
            ClearInFlight();
            SetState(AdFlowState.Failed, message);
            completion.Invoke(default);
        }

        private void ClearInFlight()
        {
            IsBusy = false;
            releaseAction = null;
        }

        private void TrackGameManager()
        {
            if (gameManager != null)
            {
                return;
            }

            gameManager = GameManager.Instance;
            if (gameManager == null)
            {
                return;
            }

            gameManager.OnStateChanged -= HandleGameStateChanged;
            gameManager.OnStateChanged += HandleGameStateChanged;
        }

        private void HandleGameStateChanged(GameState state)
        {
            if (state == GameState.Running)
            {
                ResetForNewRun();
            }
        }

        private void SetState(AdFlowState state, string message)
        {
            State = state;
            StatusMessage = message ?? string.Empty;
            OnStateChanged?.Invoke();
        }

        private void OnDestroy()
        {
            if (gameManager != null)
            {
                gameManager.OnStateChanged -= HandleGameStateChanged;
            }

            if (Instance == this)
            {
                Instance = null;
            }
        }

        private sealed class OnceAction
        {
            private Action callback;

            public OnceAction(Action callback)
            {
                this.callback = callback;
            }

            public bool TryClaim(out Action claimed)
            {
                var pending = callback;
                callback = null;
                claimed = pending;
                return pending != null;
            }

            public void Invoke()
            {
                if (TryClaim(out var pending))
                {
                    pending();
                }
            }
        }

        private sealed class OnceAction<T>
        {
            private Action<T> callback;

            public OnceAction(Action<T> callback)
            {
                this.callback = callback;
            }

            public bool TryClaim(out Action<T> claimed)
            {
                var pending = callback;
                callback = null;
                claimed = pending;
                return pending != null;
            }

            public void Invoke(T value)
            {
                if (TryClaim(out var pending))
                {
                    pending(value);
                }
            }
        }
    }
}
