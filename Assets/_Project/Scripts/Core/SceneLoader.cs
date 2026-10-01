using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CatCourier.Core
{
    public sealed class SceneLoader : MonoBehaviour
    {
        public static SceneLoader Instance { get; private set; }
        public event Action<string> OnSceneLoadStarted;
        public event Action<string> OnSceneLoadFinished;
        public event Action<string> OnSceneLoadFailed;

        private bool isLoading;
        private bool subscribedGameManager;

        private void Awake()
        {
            if (!ClaimSingleton())
            {
                return;
            }

            PersistIfRoot();

            // PersistentSystems creates GameManager before SceneLoader, so subscribing
            // only when the singleton already exists is deliberate. The PlayMode harness
            // relies on it: it builds the loader first so the failure event has no
            // subscriber and the stubbed load cannot disturb the state machine.
            if (GameManager.Instance != null)
            {
                subscribedGameManager = true;
                OnSceneLoadFailed += GameManager.Instance.HandleSceneLoadFailed;
            }
        }

        private void PersistIfRoot()
        {
            // Managers are parented under PersistentSystems, which is already DontDestroyOnLoad.
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

        public void Load(string sceneName)
        {
            if (isLoading)
            {
                OnSceneLoadFailed?.Invoke(sceneName);
                return;
            }

            StartCoroutine(LoadRoutine(sceneName));
        }

        private System.Collections.IEnumerator LoadRoutine(string sceneName)
        {
            isLoading = true;
            OnSceneLoadStarted?.Invoke(sceneName);

            AsyncOperation operation;
            try
            {
                operation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            }
            catch (Exception)
            {
                isLoading = false;
                OnSceneLoadFailed?.Invoke(sceneName);
                yield break;
            }

            if (operation == null)
            {
                isLoading = false;
                OnSceneLoadFailed?.Invoke(sceneName);
                yield break;
            }

            while (!operation.isDone)
            {
                yield return null;
            }

            isLoading = false;
            OnSceneLoadFinished?.Invoke(sceneName);
        }

        private void OnDestroy()
        {
            if (subscribedGameManager && GameManager.Instance != null)
            {
                OnSceneLoadFailed -= GameManager.Instance.HandleSceneLoadFailed;
            }

            subscribedGameManager = false;

            if (Instance == this)
            {
                Instance = null;
            }
        }
    }
}
