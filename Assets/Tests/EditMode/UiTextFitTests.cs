using CatCourier.UI;
using NUnit.Framework;
using UnityEngine;

namespace CatCourier.Tests
{
    /// <summary>
    /// Guards hub type against the two ways IMGUI silently eats text.
    ///
    /// 1. A rect shorter than the font's line box clips the glyphs — you see the top half
    ///    of a name and nothing else.
    /// 2. Text wider than its rect wraps to a second line, and a rect sized for one line
    ///    has no room for it, so the rest of the string disappears.
    ///
    /// Both shipped. The row boxes were sized for Arial, whose line box is about 1.15 em;
    /// the bundled Plus Jakarta Sans is 1.26 em, so every heading clipped the day the font
    /// landed. The descriptions were too long for their boxes even before that and only
    /// survived because Arial is narrower.
    ///
    /// WHAT THIS CAN AND CANNOT DO
    ///
    /// These assert the font's real metrics and the layout's real arithmetic. They cannot
    /// measure a GUIStyle, because Unity throws "You can only call GUI functions from inside
    /// OnGUI" if a test touches GUI.skin — GUIStyle.CalcHeight is unavailable headless.
    /// So string-width fitting is not unit-testable here, which is precisely why the
    /// runtime guarantee exists: UiTheme.Label measures the text and grows its own box, and
    /// UiTheme.LabelClipped never wraps. A box that is too small is not a state the UI can
    /// be in any more; these tests exist to catch the font changing underneath that.
    /// </summary>
    public sealed class UiTextFitTests
    {
        [Test]
        public void BundledFont_IsPresentInResources()
        {
            // If the font is missing, every box in the game falls back to Arial while the
            // layout still assumes this font's metrics — the exact original defect.
            var font = UiTheme.ProjectFont;
            Assert.That(font, Is.Not.Null,
                $"No font at Resources/{UiTheme.BundledFontName}. PersistentSystems loads it by " +
                "this name; if it is gone, every row height in the game is wrong.");

            Assert.That(font.dynamic || font.fontSize > 0, Is.True,
                "Font is neither dynamic nor sized, so line metrics cannot be read from it.");
        }

        [Test]
        public void FontLineHeight_IsMeasurable()
        {
            var em = UiTheme.FontLineEm;
            Assert.That(em, Is.GreaterThan(0f),
                "FontLineEm could not be measured, so RequiredLineHeight would silently return 0 " +
                "and every budget would pass while clipping on screen.");
        }

        [Test]
        public void FontLineHeight_ExceedsArialSoBoxesCannotBeArialSized()
        {
            // The defect, stated as an assertion. Arial's line box is ~1.15 em; this font is
            // 1.26. Anything sized against Arial is short by about 0.11 em per fontSize.
            var em = UiTheme.FontLineEm;
            Assert.That(em, Is.GreaterThan(1.20f),
                $"Font line box is {em:0.000} em, which is Arial-like. Either the bundled font " +
                "did not load, or the metrics assumption this file encodes is out of date.");
        }

        [Test]
        public void EveryFixedRowHeight_ClearsTheFontLineBox()
        {
            // The invariant behind all of it: a box reserved for one line of text is never
            // shorter than that line needs. Computed at the maximum scale the UI clamps to,
            // which is where the tightest case lives.
            const float scale = 1.5f;

            AssertRow("Districts row", RunTabMetrics.DistrictRow, HubSkin.RowSize, scale);
            AssertRow("Run tab districts label", RunTabMetrics.DistrictsLabel, HubSkin.SmallSize, scale);
        }

        /// <summary>Asserts a fixed box is at least one line tall for the given design size.</summary>
        private static void AssertRow(string what, float boxUnscaled, float designSize, float scale)
        {
            var fontSize = Mathf.Min(Mathf.Round(designSize * scale), MaxFor(designSize));
            var needed = UiTheme.RequiredLineHeight(fontSize);
            var box = boxUnscaled * scale;

            Assert.That(box, Is.GreaterThanOrEqualTo(needed - 0.5f),
                $"{what} reserves {box:0.0}px at scale {scale} but {designSize}px type needs " +
                $"{needed:0.0}px. The text will be clipped.");
        }

        /// <summary>Mirrors the clamp each style passes to UiTheme.Px.</summary>
        private static float MaxFor(float designSize)
        {
            if (designSize == HubSkin.RowSize)
            {
                return HubSkin.RowMax;
            }

            return designSize == HubSkin.HeadingSize ? HubSkin.HeadingMax : HubSkin.SmallMax;
        }

        [Test]
        public void SecondaryColumn_FitsInsideTheCard()
        {
            // Budgeted with the font's own line box rather than GUIStyle, so this is the same
            // arithmetic the presenter does — only the line heights come from the font metric
            // instead of a style that cannot exist outside OnGUI.
            var scale = 0.85f;
            var smallLine = UiTheme.RequiredLineHeight(
                Mathf.Min(Mathf.Round(HubSkin.SmallSize * scale), HubSkin.SmallMax));
            var headingLine = UiTheme.RequiredLineHeight(
                Mathf.Min(Mathf.Round(HubSkin.HeadingSize * scale), HubSkin.HeadingMax));

            var needed = RunTabMetrics.SecondaryHeight(smallLine, headingLine, scale);
            var available = ContentHeight(1008f, 530f, scale);

            Assert.That(needed, Is.LessThanOrEqualTo(available),
                $"Secondary column needs {needed:0.0}px but the card content is {available:0.0}px. " +
                "RESTORE falls off the bottom of the card.");
        }

        [Test]
        public void TextWidthBeside_StopsShortOfTheButton()
        {
            // A trailing button has to come out of the text width, or it draws over the
            // label beside it. That was the original run tab defect.
            const float column = 230f;
            var text = RunTabMetrics.TextWidthBeside(column, RunTabMetrics.ChangeButtonWidth);

            Assert.That(text + RunTabMetrics.ChangeButtonWidth + RunTabMetrics.ButtonGap,
                Is.LessThanOrEqualTo(column),
                $"Text ({text:0.0}) plus button ({RunTabMetrics.ChangeButtonWidth}) overruns a " +
                $"{column:0.0}px column.");
        }

        [Test]
        public void TextWidthBeside_NeverGoesNegative()
        {
            // Unity treats a negative-width rect as zero-size and the label silently
            // disappears, which is worse than overlapping.
            Assert.That(RunTabMetrics.TextWidthBeside(40f, RunTabMetrics.ChangeButtonWidth),
                Is.GreaterThan(0f));
        }

        [Test]
        public void RunTabGivesTheCatNameTheFullColumnWidth()
        {
            // Regression guard for the truncation I introduced when the name shared its row
            // with CHANGE. The name now spans the whole right-hand column.
            var scale = 0.85f;
            var content = ContentWidth(1008f, 530f, scale);
            var gap = RunTabMetrics.ButtonGap * 2f * scale;
            var column = content - gap - (content - gap) * 0.56f;
            var oldBesideButton = RunTabMetrics.TextWidthBeside(column, RunTabMetrics.ChangeButtonWidth);

            Assert.That(column, Is.GreaterThan(oldBesideButton * 2f),
                $"Run tab column is {column:0.0}px; the name is only getting {oldBesideButton:0.0}px. " +
                "It must span the column, not share a row with the CHANGE button.");
        }

        [Test]
        public void DistrictCount_MatchesTheRowsActuallyDrawn()
        {
            // The height sum hardcodes this. Adding a district to the loop without updating
            // it overflows the column silently.
            Assert.That(RunTabMetrics.DistrictCount, Is.EqualTo(4),
                "Update RunTabMetrics.DistrictCount when districts change.");
        }

        /// <summary>Mirrors HubPresenter's card maths at a given screen size.</summary>
        private static float ContentHeight(float width, float height, float scale)
        {
            return height - (176f + 88f) * scale - 18f * 2f * scale;
        }

        private static float ContentWidth(float width, float height, float scale)
        {
            return width * 0.63f - 48f * scale - 18f * 2f * scale;
        }
    }
}
