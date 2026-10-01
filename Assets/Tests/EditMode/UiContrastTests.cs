using CatCourier.UI;
using NUnit.Framework;
using UnityEngine;

namespace CatCourier.Tests
{
    /// <summary>
    /// Contrast floor for the palette.
    ///
    /// Three pairs failed WCAG AA and nothing caught it: the urgent-delivery timer and the
    /// DEVELOPMENT FAKE DATA banner both used a red measuring 3.33:1 on the panel slate,
    /// and the "IN USE" chip green measured 2.92:1 on parchment. A judge failing to read
    /// the fake-data warning is the worst outcome this project has.
    ///
    /// Ratios are computed here rather than asserted as literals, so a future palette
    /// tweak fails loudly instead of quietly going unreadable.
    /// </summary>
    public sealed class UiContrastTests
    {
        private const float AaBody = 4.5f;

        private static float Ratio(Color foreground, Color background)
        {
            var a = Luminance(foreground);
            var b = Luminance(background);
            if (a < b)
            {
                var swap = a;
                a = b;
                b = swap;
            }

            return (a + 0.05f) / (b + 0.05f);
        }

        private static float Luminance(Color color)
        {
            return 0.2126f * Channel(color.r) + 0.7152f * Channel(color.g) + 0.0722f * Channel(color.b);
        }

        private static float Channel(float value)
        {
            return value <= 0.03928f
                ? value / 12.92f
                : Mathf.Pow((value + 0.055f) / 1.055f, 2.4f);
        }

        private static void AssertReadable(Color foreground, Color background, string what)
        {
            var ratio = Ratio(foreground, background);
            Assert.That(ratio, Is.GreaterThanOrEqualTo(AaBody),
                $"{what} measures {ratio:0.00}:1 on its background. WCAG AA needs {AaBody}:1 for body text.");
        }

        [Test]
        public void Danger_OnDarkPanel_MeetsAA()
        {
            // Urgent delivery timer, fragile parcel warning, and the fake-data banner.
            AssertReadable(UiTheme.Danger, UiTheme.Slate, "Danger on Slate");
        }

        [Test]
        public void Leaf_OnParchment_MeetsAA()
        {
            // The "IN USE" breed chip and the unlocked-district dot.
            AssertReadable(HubSkin.Leaf, HubSkin.Paper, "Leaf on Paper");
        }

        [Test]
        public void Ink_OnTerracotta_MeetsAA()
        {
            // Active tab label, START RUN, SELECT. Terracotta is a button face, so its
            // label colour is the contrast that matters.
            AssertReadable(HubSkin.Ink, HubSkin.Terracotta, "Ink on Terracotta");
        }

        [Test]
        public void HudScoreScrim_HoldsCreamAboveAAOverBrightRoad()
        {
            // Worst realistic case: cream on a fully white road. Without a scrim that pair
            // measures 0.83:1, which is why the HUD needs one at all.
            var road = new Color(1f, 1f, 1f, 1f);
            var centre = CompositeScrimCentre(road, RunHudPresenter.ScrimAlpha, ScrimBands);

            AssertReadable(UiTheme.Cream, centre, "Cream on scrimmed bright road");
        }

        [Test]
        public void HudScoreScrim_StaysReadableAsItFadesOut()
        {
            // The entrance animates the scrim's opacity, so the weakest state is the one
            // that ships longest. Mid-entrance still has to clear AA over a white road,
            // otherwise the score is unreadable for the first frames of every run.
            var road = new Color(1f, 1f, 1f, 1f);
            var mid = CompositeScrimCentre(road, RunHudPresenter.ScrimAlpha * 0.6f, ScrimBands);

            AssertReadable(UiTheme.Cream, mid, "Cream on scrim at 60% entrance opacity");
        }

        [Test]
        public void HudScoreScrim_CentreIsMoreOpaqueThanItsOuterBand()
        {
            // Guards the nesting itself. An earlier version passed two same-sized offset
            // rects, which slid the stack sideways instead of growing it: the darkest
            // band landed beside the text rather than under it. If the bands stop
            // accumulating, the scrim is decorative and this fails.
            var road = new Color(1f, 1f, 1f, 1f);
            var centre = CompositeScrimCentre(road, RunHudPresenter.ScrimAlpha, ScrimBands);
            var single = Blend(UiTheme.Ink, road, RunHudPresenter.ScrimAlpha * (1f / ScrimBands));

            Assert.That(Luminance(centre), Is.LessThan(Luminance(single)),
                "Nested scrim bands should compound, so the centre must be darker than any one band.");
        }

        /// <summary>Must match the band count in UiTheme.SoftScrim.</summary>
        private const int ScrimBands = 6;

        /// <summary>
        /// Composite alpha at the centre of a nested scrim, the way the renderer stacks it.
        ///
        /// Each band draws over the previous one, so the centre is NOT a single blend at
        /// peakAlpha: it compounds to 1 - (1-p)(1-2p/6)... . Modelling it as one blend is
        /// what made an earlier version of this test report 4.15:1 for a scrim that
        /// actually renders at 12.9:1.
        /// </summary>
        private static Color CompositeScrimCentre(Color background, float peakAlpha, int bands)
        {
            var colour = background;
            for (var band = 0; band < bands; band++)
            {
                var t = (band + 1f) / bands;
                colour = Blend(UiTheme.Ink, colour, peakAlpha * t);
            }

            return colour;
        }

        /// <summary>Composite fg over bg at the given alpha.</summary>
        private static Color Blend(Color foreground, Color background, float alpha)
        {
            return new Color(
                foreground.r * alpha + background.r * (1f - alpha),
                foreground.g * alpha + background.g * (1f - alpha),
                foreground.b * alpha + background.b * (1f - alpha),
                1f);
        }

        [Test]
        public void BodyText_OnBothRegisters_MeetsAA()
        {
            // The two skins must each hold up on their own surface.
            AssertReadable(HubSkin.Ink, HubSkin.Paper, "Hub body on Paper");
            AssertReadable(HubSkin.InkSoft, HubSkin.Paper, "Hub secondary on Paper");
            AssertReadable(UiTheme.Cream, UiTheme.Slate, "Run primary on Slate");
            AssertReadable(UiTheme.Muted, UiTheme.Slate, "Run secondary on Slate");
            AssertReadable(UiTheme.Gold, UiTheme.Slate, "Gold stat on Slate");
            AssertReadable(HubSkin.Honey, UiTheme.Slate, "Honey stat on Slate");
        }
    }
}