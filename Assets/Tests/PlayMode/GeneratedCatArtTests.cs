using System.Collections;
using CatCourier.Art;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CatCourier.Tests.PlayMode
{
    public sealed class GeneratedCatArtTests
    {
        [UnityTest]
        public IEnumerator AwakeCreatesPackageAndDizzyBirdRenderers()
        {
            var host = new GameObject("Generated Cat Art Test");
            try
            {
                host.AddComponent<SpriteRenderer>();
                host.AddComponent<GeneratedCatArt>();
                yield return null;

                Assert.That(host.transform.Find("Carried Bindle")?.GetComponent<SpriteRenderer>(), Is.Not.Null);
                for (var i = 0; i < 3; i++)
                    Assert.That(host.transform.Find("Dizzy Bird " + i)?.GetComponent<SpriteRenderer>(), Is.Not.Null);
            }
            finally
            {
                Object.Destroy(host);
            }
        }
    }
}
