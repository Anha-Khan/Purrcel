using UnityEngine;

namespace CatCourier.UI
{
    /// <summary>
    /// Row heights for the hub Run tab, in unscaled pixels.
    ///
    /// These live apart from the drawing code on purpose. The tab is laid out by hand in
    /// absolute rects, which means nothing stops two rows from overlapping or a column
    /// from outgrowing its card — IMGUI reports neither. The first version of this layout
    /// did exactly that: the "YOUR CAT" label sat on the cat name, GO PREMIUM sat on its
    /// own subtitle, CHANGE and BUY sat on their text, and the district list ran off the
    /// bottom of the card onto the street behind it.
    ///
    /// The presenter lays out from these and RunTabLayoutTests asserts the columns still
    /// fit, so a future tweak that overflows fails the build instead of the device.
    /// </summary>
    public static class RunTabMetrics
    {
        // ---- Primary column: the action and the run's own state ----

        public const float Heading = 34f;
        public const float GapAfterHeading = 8f;
        public const float StartButton = 64f;
        public const float GapAfterStart = 12f;
        public const float WeatherChip = 30f;
        public const float GapAfterChip = 12f;
        public const float Rule = 1f;
        public const float GapAfterRule = 12f;
        public const float DistrictsLabel = 20f;
        public const float GapAfterDistrictsLabel = 4f;
        public const float DistrictRow = 23f;
        public const int DistrictCount = 4;
        public const float TrailingRule = 1f;
        public const float GapAfterTrailingRule = 8f;

        // ---- Secondary column: who you are and what you can unlock ----

        // Row heights for text are NOT constants here. They are measured from the font at
        // draw time (UiTheme.LineHeight), because a literal sized for one font silently
        // clips under another: Plus Jakarta Sans needs 1.26 em where Arial needed ~1.15.
        // Only gaps, buttons and panels are fixed, because those are not text.
        public const float GapAfterColumnLabel = 6f;
        public const float GapAfterCatLabel = 2f;
        public const float GapAfterCatName = 6f;
        public const float ChangeButton = 40f;
        public const float GapAfterChange = 14f;

        /// <summary>
        /// Must clear pad + title + subtitle + button + pad. Pinned by a layout test; if
        /// you change the button or the title band, change this with it.
        /// </summary>
        public const float PremiumPanel = 112f;
        public const float GapAfterPremium = 14f;

        /// <summary>Height of the RESTORE pill. Fixed, not text-derived.</summary>
        public const float RestoreButtonHeight = 40f;

        /// <summary>Unscaled height the primary column needs, top to bottom.</summary>
        public static float PrimaryHeight
        {
            get
            {
                return Heading + GapAfterHeading
                       + StartButton + GapAfterStart
                       + WeatherChip + GapAfterChip
                       + Rule + GapAfterRule
                       + DistrictsLabel + GapAfterDistrictsLabel
                       + DistrictRow * DistrictCount
                       + TrailingRule + GapAfterTrailingRule;
            }
        }

        /// <summary>
        /// Height the secondary column needs in scaled pixels.
        ///
        /// The text rows are passed in as measured line heights rather than read from a
        /// GUIStyle here, because GUIStyle cannot be constructed outside OnGUI and this
        /// budget has to stay checkable from a test. The presenter passes the real
        /// UiTheme.LineHeight values; a test passes values derived from
        /// UiTheme.RequiredLineHeight.
        /// </summary>
        public static float SecondaryHeight(float smallLineHeight, float headingLineHeight, float scale)
        {
            return smallLineHeight + GapAfterColumnLabel * scale
                   + smallLineHeight + GapAfterCatLabel * scale
                   + headingLineHeight + GapAfterCatName * scale
                   + ChangeButton * scale + GapAfterChange * scale
                   + PremiumPanel * scale + GapAfterPremium * scale
                   + RestoreButtonHeight * scale;
        }

        /// <summary>
        /// Width reserved by a trailing button, plus the gap that keeps it off the text.
        /// A button pinned to the column's right edge has to come out of the text width or
        /// it draws straight over the label next to it.
        /// </summary>
        public const float ChangeButtonWidth = 110f;
        public const float ButtonGap = 12f;

        /// <summary>Text width that survives a trailing button of the given width.</summary>
        public static float TextWidthBeside(float columnWidth, float buttonWidth)
        {
            return Mathf.Max(40f, columnWidth - buttonWidth - ButtonGap);
        }
    }
}
