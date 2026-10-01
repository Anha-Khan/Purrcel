using System.Collections;
using CatCourier.Art;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CatCourier.Tests.PlayMode
{
    public sealed class GeneratedCatArtTests : Day6PlayModeTestBase
    {
        [UnityTest]
        public IEnumerator AwakeCreatesPackageAndDizzyBirdRenderers()
        {
            var host = Track(new GameObject("Generated Cat Art Test"));
            var catRenderer = host.AddComponent<SpriteRenderer>();
            host.AddComponent<GeneratedCatArt>();
            yield return null;

            // Assert the renderers are real and layered, not merely present. The old
            // version only checked that three children had a SpriteRenderer, so it would
            // have passed on three magenta pixels in the wrong order.
            var bundle = host.transform.Find("Carried Bindle");
            Assert.That(bundle, Is.Not.Null);
            var bundleRenderer = bundle.GetComponent<SpriteRenderer>();
            Assert.That(bundleRenderer, Is.Not.Null);
            Assert.That(bundleRenderer.sortingOrder, Is.EqualTo(catRenderer.sortingOrder + 1),
                "The carried parcel must draw in front of the cat.");
            Assert.That(bundleRenderer.enabled, Is.False,
                "The bindle is hidden until a package is actually carried.");
            Assert.That(bundle.localScale.x, Is.EqualTo(0.13f).Within(0.001f));
            Assert.That(bundle.localPosition.x, Is.EqualTo(-0.52f).Within(0.001f));

            for (var i = 0; i < 3; i++)
            {
                var bird = host.transform.Find("Dizzy Bird " + i);
                Assert.That(bird, Is.Not.Null, $"Dizzy Bird {i} was not created.");
                var birdRenderer = bird.GetComponent<SpriteRenderer>();
                Assert.That(birdRenderer, Is.Not.Null);
                Assert.That(birdRenderer.sortingOrder, Is.EqualTo(catRenderer.sortingOrder + 3),
                    "The dizzy birds must draw in front of the cat and its parcel.");
                Assert.That(birdRenderer.enabled, Is.False,
                    "The dizzy birds are a death reaction and must start hidden.");
                Assert.That(bird.localScale.x, Is.EqualTo(0.04f).Within(0.001f));
            }
        }
    }
}
