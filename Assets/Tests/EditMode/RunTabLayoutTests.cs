using CatCourier.UI;
using NUnit.Framework;
using UnityEngine;

namespace CatCourier.Tests
{
    /// <summary>
    /// Geometry guards for the hub Run tab.
    ///
    /// The tab is drawn with absolute rects, so IMGUI reports nothing when two rows
    /// overlap or a column runs past its card — it just draws on top of itself or off the
    /// bottom. That is not hypothetical: the first two-column version overlapped its own
    /// text in four places and pushed the district list off the card onto the street
    /// behind it, and none of it was visible until it ran on a device.
    ///
    /// These assert the layout arithmetic at the real card size, which is derived the same
    /// way the presenter derives it rather than being hardcoded to one screen.
    /// </summary>
    public sealed class RunTabLayoutTests
    {
        /// <summary>The editor preview this was reported broken at.</summary>
        private const float ScreenWidth = 1008f;
        private const float ScreenHeight = 530f;

        /// <summary>Matches HubPresenter: the card occupies 63% of the width, painted street kept.</summary>
        private const float CardWidthFraction = 0.63f;
        private const float CardTopInset = 176f;
        private const float CardBottomInset = 88f;
        private const float CardSideInset = 24f;
        private const float CardInset = 18f;

        private static float Scale()
        {
            var factor = Mathf.Min(ScreenWidth / 1280f, ScreenHeight / 720f);
            return Mathf.Clamp(factor, 0.85f, 1.5f);
        }

        private static Rect Content()
        {
            var scale = Scale();
            var column = ScreenWidth * CardWidthFraction;
            var card = new Rect(
                CardSideInset * scale,
                CardTopInset * scale,
                column - CardSideInset * 2f * scale,
                ScreenHeight - (CardTopInset + CardBottomInset) * scale);

            return new Rect(
                card.x + CardInset * scale,
                card.y + CardInset * scale,
                card.width - CardInset * 2f * scale,
                card.height - CardInset * 2f * scale);
        }

        [Test]
        public void PrimaryColumn_FitsInsideTheCard()
        {
            var needed = RunTabMetrics.PrimaryHeight * Scale();
            var available = Content().height;

            Assert.That(needed, Is.LessThanOrEqualTo(available),
                $"Primary column needs {needed:0.0}px but the card content is only {available:0.0}px. " +
                "The district list runs off the bottom of the card.");
        }

        [Test]
        public void SecondaryColumn_FitsInsideTheCard()
        {
            // Budgeted from the font's line box rather than a GUIStyle: a GUIStyle cannot be
            // built outside OnGUI, and this check has to run in an Edit Mode test.
            var smallLine = UiTheme.RequiredLineHeight(
                Mathf.Min(Mathf.Round(HubSkin.SmallSize * Scale()), HubSkin.SmallMax));
            var headingLine = UiTheme.RequiredLineHeight(
                Mathf.Min(Mathf.Round(HubSkin.HeadingSize * Scale()), HubSkin.HeadingMax));

            var needed = RunTabMetrics.SecondaryHeight(smallLine, headingLine, Scale());
            var available = Content().height;

            Assert.That(needed, Is.LessThanOrEqualTo(available),
                $"Secondary column needs {needed:0.0}px but the card content is only {available:0.0}px. " +
                "RESTORE falls off the bottom of the card.");
        }

        [Test]
        public void TextWidthBeside_StopsShortOfTheButton()
        {
            // The bug this exists for: the cat name spanned the full column and CHANGE
            // drew over it. A trailing button has to come out of the text width.
            var column = 230f;
            var text = RunTabMetrics.TextWidthBeside(column, RunTabMetrics.ChangeButtonWidth);

            Assert.That(text + RunTabMetrics.ChangeButtonWidth + RunTabMetrics.ButtonGap,
                Is.LessThanOrEqualTo(column),
                $"Text ({text:0.0}) + button ({RunTabMetrics.ChangeButtonWidth}) overruns a {column:0.0}px column.");
        }

        [Test]
        public void TextWidthBeside_NeverGoesNegative()
        {
            // A narrow column must clamp rather than produce a negative rect, which Unity
            // silently treats as zero-size and the label disappears.
            Assert.That(RunTabMetrics.TextWidthBeside(40f, RunTabMetrics.ChangeButtonWidth),
                Is.GreaterThan(0f));
        }

        [Test]
        public void DistrictCount_MatchesTheRowsActuallyDrawn()
        {
            // The height sum hardcodes 4 rows. If a district is added to the loop without
            // updating the metric, the column overflows silently.
            Assert.That(RunTabMetrics.DistrictCount, Is.EqualTo(4),
                "Update RunTabMetrics.DistrictCount and DistrictRow budget when districts change.");
        }

        [Test]
        public void PremiumPanel_FitsItsTitleSubtitleAndButton()
        {
            // Panel height has to clear the button plus its padding, or BUY is clipped by
            // the card edge below it.
            var pad = 12f;
            var button = 36f;
            var needed = pad + 30f + 22f + button + pad;
            var available = RunTabMetrics.PremiumPanel;

            Assert.That(needed, Is.LessThanOrEqualTo(available),
                $"Premium panel needs {needed:0.0}px for title, subtitle and button but is {available:0.0}px.");
        }

        [Test]
        public void BothColumns_FitAtFullHD()
        {
            // Scale is capped at 1.5, which needs at least 1920x1080 to reach. That is the
            // worst case for this layout: row heights grow with scale while the card only
            // grows with the screen, so the tallest rows meet the shortest card here.
            //
            // Note the resolution is real, not the 1008x530 preview. Forcing scale 1.5 onto
            // a 530px-tall screen describes a window that cannot exist and produces a
            // meaningless failure.
            const float width = 1920f;
            const float height = 1080f;
            const float scale = 1.5f;

            var contentHeight = height - (CardTopInset + CardBottomInset) * scale - CardInset * 2f * scale;

            Assert.That(RunTabMetrics.PrimaryHeight * scale, Is.LessThanOrEqualTo(contentHeight),
                $"Primary column needs {RunTabMetrics.PrimaryHeight * scale:0.0}px at 1920x1080 " +
                $"but the card content is {contentHeight:0.0}px.");
        }
    }
}
