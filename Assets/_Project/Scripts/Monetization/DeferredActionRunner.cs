using System;
using System.Collections;
using UnityEngine;

namespace CatCourier.Monetization
{
    [DisallowMultipleComponent]
    public sealed class DeferredActionRunner : MonoBehaviour
    {
        private Action action;

        public void RunNextFrames(Action value)
        {
            action = value;
            StartCoroutine(InvokeNextFrames());
        }

        private IEnumerator InvokeNextFrames()
        {
            yield return null;
            yield return null;
            var pending = action;
            action = null;
            pending?.Invoke();
        }
    }
}
