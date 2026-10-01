// Purrcel design system · UI · two registers (slate in-run / parchment hub)
// Tokens, type ramp, scale and the GUILayout-vs-Rect rule. Full spec: design.md
// Contrast floor is asserted by Assets/Tests/EditMode/UiContrastTests.cs (WCAG AA).
using System.Collections.Generic;
using UnityEngine;

namespace CatCourier.UI
{
    /// <summary>
    /// Scale, palette, typography and motion for every IMGUI surface.
    ///
    /// This used to live privately inside <c>HubPresenter</c>, so only the hub scaled its
    /// text and every other surface fell back to IMGUI's ~12px default on a 1080px-tall
    /// phone. One owner means the HUD, the death screen, the pause menu and the story
    /// card all read at the same size on any device.
    ///
    /// ponytail: sizes are clamped rather than driven by an OS font-scale API, because
    /// IMGUI does not expose one. Unity's built-in Arial also turns to mush past ~64px,
    /// so the clamp is what keeps large text crisp as well as bounded.
    /// </summary>
    public static class UiTheme
    {
        /// <summary>Reference resolution the pixel values below were authored against.</summary>
        private const float ReferenceWidth = 1280f;
        private const float ReferenceHeight = 720f;

        // Palette. Warm and teal against a dark slate, matched to the existing hub art.
        public static Color Ink => new Color(0.06f, 0.10f, 0.12f, 1f);
        public static Color Slate => new Color(0.12f, 0.18f, 0.21f, 0.94f);
        public static Color Teal => new Color(0.06f, 0.43f, 0.43f, 1f);
        public static Color Orange => new Color(0.91f, 0.54f, 0.20f, 1f);
        public static Color Cream => new Color(1f, 0.91f, 0.73f, 1f);
        public static Color Gold => new Color(0.98f, 0.79f, 0.36f, 1f);
        /// <summary>
        /// Lightened from 0.85,0.29,0.24, which measured 3.33:1 on Slate and failed AA
        /// for body text. This measures 4.64:1. The fake-data banner and the urgent
        /// delivery timer are the two places this colour carries meaning, so it is the
        /// one token that must not be eyeballed.
        /// </summary>
        public static Color Danger => new Color(0.94f, 0.42f, 0.35f, 1f);
        public static Color Muted => new Color(0.62f, 0.70f, 0.74f, 1f);

        /// <summary>Name of the bundled font under Resources. Shared with PersistentSystems.</summary>
        public const string BundledFontName = "PurrcelUI";

        /// <summary>Cached line-box ratio for <see cref="ProjectFont"/>. 0 until measured.</summary>
        private static float fontLineEm;

        /// <summary>No-wrap variants, so a clipped label does not allocate a style per frame.</summary>
        private static readonly Dictionary<GUIStyle, GUIStyle> NoWrap = new Dictionary<GUIStyle, GUIStyle>();

        private static readonly Dictionary<int, Texture2D> Solids = new Dictionary<int, Texture2D>();
        private static readonly Dictionary<int, GUIStyle> Styles = new Dictionary<int, GUIStyle>();
        private static int builtFor = -1;

        /// <summary>1x at the reference size, up to 1.5x on a modern landscape phone.</summary>
        public static float Scale
        {
            get
            {
                var safe = HubLayout.SafeRect;
                if (safe.width <= 0f || safe.height <= 0f)
                {
                    return 1f;
                }

                var factor = Mathf.Min(safe.width / ReferenceWidth, safe.height / ReferenceHeight);
                return Mathf.Clamp(factor, 0.85f, 1.5f);
            }
        }

        /// <summary>Scales a pixel value, clamped so nothing outgrows the built-in font.</summary>
        public static float Px(float value, float max = 64f)
        {
            return Mathf.Min(Mathf.Round(value * Scale), max);
        }

        /// <summary>
        /// A soft-edged dark scrim between two rects.
        ///
        /// IMGUI cannot blur, so softness is faked with nested bands whose alpha ramps
        /// toward the centre. This is the honest alternative to a hard offset shadow: a
        /// zero-blur drop shadow over moving art reads as a duplicate element, whereas a
        /// ramp gives a genuine falloff and a tunable peak alpha.
        /// </summary>
        public static void SoftScrim(Rect target, float feather, float peakAlpha, float opacity = 1f)
        {
            var bands = 6;
            var peak = Mathf.Clamp01(peakAlpha) * Mathf.Clamp01(opacity);
            if (peak <= 0.001f)
            {
                return;
            }

            var previous = GUI.color;
            var white = Solid(new Color(1f, 1f, 1f, 1f));
            for (var band = 0; band < bands; band++)
            {
                // Nested rects shrinking onto `target`, thinnest last. Alpha ramps up with
                // them so the centre is the most opaque part of the scrim.
                var t = (band + 1f) / bands;
                var rect = Grow(target, feather * (1f - t));
                GUI.color = new Color(Ink.r, Ink.g, Ink.b, peak * t);
                GUI.DrawTexture(rect, white);
            }

            GUI.color = previous;
        }

        /// <summary>
        /// Expands a rect outward on every edge. The scrim's softness comes from nesting
        /// these, so the sign matters: positive grows, negative shrinks.
        /// </summary>
        private static Rect Grow(Rect rect, float amount)
        {
            return new Rect(
                rect.x - amount,
                rect.y - amount,
                rect.width + amount * 2f,
                rect.height + amount * 2f);
        }

        /// <summary>
        /// Optional project font, applied to every style when set.
        ///
        /// The project ships no font asset, so IMGUI's built-in Arial is the ceiling on
        /// display type. Assigning one here lifts that with no other change, and leaving it
        /// null is safe: every style falls back to GUI.skin. Changing it clears the style
        /// cache so nothing keeps the old face.
        /// </summary>
        public static Font DisplayFont { get; private set; }

        /// <summary>
        /// Bumped whenever the font changes. Both UiTheme and HubSkin key their style
        /// caches on it, so a font swap invalidates the hub skin too.
        /// </summary>
        public static int StyleVersion { get; private set; }

        public static void SetFont(Font font)
        {
            if (ReferenceEquals(DisplayFont, font))
            {
                return;
            }

            DisplayFont = font;
            Styles.Clear();
            // NoWrap is keyed by the old GUIStyle instances, which are about to be replaced.
            NoWrap.Clear();
            builtFor = -1;
            StyleVersion++;
        }

        /// <summary>Applies the project font, or leaves the skin font when none is set.</summary>
        public static GUIStyle WithFont(GUIStyle style)
        {
            if (DisplayFont != null)
            {
                style.font = DisplayFont;
            }

            return style;
        }

        private static GUIStyle Style(int key, System.Func<GUIStyle> build)
        {
            var token = Mathf.RoundToInt(Scale * 1000f);
            if (builtFor != token)
            {
                Styles.Clear();
                // Scale change replaces the styles, so the no-wrap clones are stale too.
                NoWrap.Clear();
                builtFor = token;
            }

            if (Styles.TryGetValue(key, out var cached) && cached != null)
            {
                return cached;
            }

            var created = build();
            Styles[key] = created;
            return created;
        }

        /// <summary>The number a player glances at. Largest role in the game.</summary>
        public static GUIStyle Display => Style(1, () => WithFont(new GUIStyle(GUI.skin.label)
        {
            fontSize = (int)UiTheme.Px(46f, 62f),
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleLeft
        }));

        /// <summary>Screen and panel headings.</summary>
        public static GUIStyle Title => Style(2, () => WithFont(new GUIStyle(GUI.skin.label)
        {
            fontSize = (int)UiTheme.Px(30f, 38f),
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleLeft
        }));

        /// <summary>Default reading size.</summary>
        public static GUIStyle Body => Style(3, () => WithFont(new GUIStyle(GUI.skin.label)
        {
            fontSize = (int)UiTheme.Px(21f, 27f),
            alignment = TextAnchor.MiddleLeft
        }));

        /// <summary>Secondary and supporting values.</summary>
        public static GUIStyle Caption => Style(4, () => WithFont(new GUIStyle(GUI.skin.label)
        {
            fontSize = (int)UiTheme.Px(16f, 21f),
            alignment = TextAnchor.MiddleLeft
        }));

        public static GUIStyle BodyCentered => Style(5, () => new GUIStyle(Body)
        {
            alignment = TextAnchor.MiddleCenter
        });

        public static GUIStyle Button => Style(6, () => WithFont(new GUIStyle(GUI.skin.button)
        {
            fontSize = (int)UiTheme.Px(20f, 26f),
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        }));

        /// <summary>Android's minimum comfortable touch target, before scaling.</summary>
        public const float TouchTarget = 48f;

        public static float Touch => Mathf.Max(TouchTarget, TouchTarget * Scale);

        public static void DrawTexture(Rect rect, Texture2D texture)
        {
            if (texture != null)
            {
                GUI.DrawTexture(rect, texture);
            }
        }

        /// <summary>A 1x1 texture of the given colour, cached by value.</summary>
        public static Texture2D Solid(Color color)
        {
            var key = ((int)(color.r * 255) << 24) | ((int)(color.g * 255) << 16) |
                      ((int)(color.b * 255) << 8) | (int)(color.a * 255);
            if (Solids.TryGetValue(key, out var cached) && cached != null)
            {
                return cached;
            }

            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            texture.SetPixel(0, 0, color);
            texture.Apply();
            Solids[key] = texture;
            return texture;
        }

        /// <summary>Full-screen wash that lifts text off a moving background.</summary>
        public static void Scrim(float alpha, Color? tint = null)
        {
            var color = tint ?? Ink;
            color.a = Mathf.Clamp01(alpha);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Solid(color));
        }

        /// <summary>
        /// A raised panel: soft drop shadow, slate body, hairline edge, accent cap.
        /// Returns the content rect, inset from the panel body.
        /// </summary>
        public static Rect Panel(Rect outer, float alpha = 1f, Color? accent = null)
        {
            var shadow = 4f * Scale;
            GUI.DrawTexture(Offset(outer, shadow * 0.5f, -shadow), Solid(Fade(Ink, 0.34f * alpha)));
            GUI.DrawTexture(outer, Solid(Fade(Slate, alpha)));
            GUI.DrawTexture(new Rect(outer.x, outer.y, outer.width, Mathf.Max(1f, 2f * Scale)),
                Solid(Fade(accent ?? Teal, alpha)));

            var pad = 16f * Scale;
            return new Rect(outer.x + pad, outer.y + pad, outer.width - pad * 2f, outer.height - pad * 2f);
        }

        public static Rect Offset(Rect rect, float dx, float dy) =>
            new Rect(rect.x + dx, rect.y + dy, rect.width, rect.height);

        /// <summary>Scales a rect about its own centre, for entrance animation.</summary>
        public static Rect ScaledAboutCentre(Rect rect, float factor)
        {
            if (Mathf.Approximately(factor, 1f))
            {
                return rect;
            }

            var width = rect.width * factor;
            var height = rect.height * factor;
            var centre = rect.center;
            return new Rect(centre.x - width * 0.5f, centre.y - height * 0.5f, width, height);
        }

        public static Color Fade(Color color, float alpha) =>
            new Color(color.r, color.g, color.b, Mathf.Clamp01(color.a * alpha));

        /// <summary>A horizontal meter. Drawn left to right.</summary>
        public static void Bar(Rect rect, float fill01, Color colour, Color? track = null)
        {
            GUI.DrawTexture(rect, Solid(track ?? Fade(Ink, 0.55f)));
            var filled = Mathf.Clamp01(fill01) * rect.width;
            if (filled > 1f)
            {
                GUI.DrawTexture(new Rect(rect.x, rect.y, filled, rect.height), Solid(colour));
            }
        }

        /// <summary>A row of a stat block: label left, value right, one line.</summary>
        public static void StatRow(Rect rect, string label, string value, Color valueColor)
        {
            var labelStyle = Caption;
            var valueStyle = Style(8, () => new GUIStyle(Body)
            {
                alignment = TextAnchor.MiddleRight,
                fontStyle = FontStyle.Bold
            });

            Label(rect, label, labelStyle, Muted);
            Label(rect, value, valueStyle, valueColor);
        }

        /// <summary>
        /// Height this style needs to show <paramref name="text"/> at the given width,
        /// including wrapping and the style's own padding.
        ///
        /// Callers should size rows with this rather than a literal. A rect shorter than
        /// this is silently clipped by IMGUI, which is how every heading in the hub ended
        /// up showing only its top half: the boxes were sized for Arial, and the bundled
        /// Plus Jakarta Sans has a 1.26 em line box against Arial's ~1.15.
        /// </summary>
        public static float MeasureHeight(GUIStyle style, string text, float width)
        {
            if (string.IsNullOrEmpty(text))
            {
                return 0f;
            }

            return style.CalcHeight(new GUIContent(text), Mathf.Max(1f, width));
        }

        /// <summary>Width this style needs to show the text on one line.</summary>
        public static float MeasureWidth(GUIStyle style, string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return 0f;
            }

            return style.CalcSize(new GUIContent(text)).x;
        }

        /// <summary>
        /// The font the UI is using: whatever was assigned, else the bundled one.
        ///
        /// Read from Resources rather than only from the assigned field so it is also
        /// correct before PersistentSystems has run — which is the case in an Edit Mode
        /// test, and exactly when a test most needs to know the font is present.
        /// </summary>
        public static Font ProjectFont =>
            DisplayFont != null ? DisplayFont : Resources.Load<Font>(BundledFontName);

        /// <summary>
        /// The font's line box, in em. 1.26 for Plus Jakarta Sans, against roughly 1.15 for
        /// Arial — the whole reason the pre-existing boxes clipped.
        ///
        /// Measured off the Font asset rather than a GUIStyle, because GUIStyle cannot be
        /// built outside OnGUI and this has to be answerable from a test. Returns 0 if there
        /// is no usable font, which callers must treat as "unknown" rather than "zero".
        /// </summary>
        public static float FontLineEm
        {
            get
            {
                if (fontLineEm > 0f)
                {
                    return fontLineEm;
                }

                var font = ProjectFont;
                if (font == null)
                {
                    return 0f;
                }

                // Font.fontSize is read-only, so the ratio comes from the asset's own current
                // size rather than being sampled at a size we choose. Dividing cancels the
                // size out, so the result is size-independent either way.
                if (font.fontSize <= 0)
                {
                    return 0f;
                }

                fontLineEm = font.lineHeight / (float)font.fontSize;
                return fontLineEm;
            }
        }

        /// <summary>
        /// Height a line of text needs at this font size, from <see cref="FontLineEm"/>.
        /// The headless equivalent of <see cref="LineHeight"/> for budgeting in tests.
        /// </summary>
        public static float RequiredLineHeight(float fontSize)
        {
            return fontSize * FontLineEm;
        }

        /// <summary>
        /// Height of a single line in this style. Use for fixed-height rows — buttons, chips,
        /// one-line values — where wrapping is not wanted and only the font's line box matters.
        /// </summary>
        public static float LineHeight(GUIStyle style)
        {
            return style.CalcHeight(GUIContent.none, 10000f);
        }

        public static void Label(Rect rect, string text, GUIStyle style, Color color)
        {
            // Grow the box downward if the text needs more room than the caller gave it.
            // Layout should already have sized the row with MeasureHeight; this is the
            // backstop that makes clipping impossible even when it did not, because a short
            // box draws a cut-off label rather than a readable one.
            var needed = MeasureHeight(style, text, rect.width);
            if (needed > rect.height)
            {
                rect.height = needed;
            }

            var previous = GUI.color;
            GUI.color = color;
            GUI.Label(rect, text, style);
            GUI.color = previous;
        }

        /// <summary>
        /// A label that must stay on one line, truncating with an ellipsis if it does not fit.
        ///
        /// Use for values and names where a second line would break the row: IMGUI's default
        /// wordWrap hides the overflow by pushing it to a line the rect has no room for, which
        /// is how "Long Haired Tuxedo" disappeared from the Cats tab entirely.
        /// </summary>
        public static void LabelClipped(Rect rect, string text, GUIStyle style, Color color)
        {
            var single = SingleLine(style);
            var previous = GUI.color;
            GUI.color = color;
            GUI.Label(rect, text, single);
            GUI.color = previous;
        }

        /// <summary>No-wrap variant of a style, cached by the style's cache key.</summary>
        private static GUIStyle SingleLine(GUIStyle style)
        {
            if (!NoWrap.ContainsKey(style))
            {
                var copy = new GUIStyle(style) { wordWrap = false, clipping = TextClipping.Clip };
                NoWrap[style] = copy;
            }

            return NoWrap[style];
        }

        /// <summary>Draws a coloured button face, then the label. Returns whether it was hit.</summary>
        public static bool FaceButton(Rect rect, string text, Color face, Color ink, bool enabled = true)
        {
            var target = new Rect(rect.x, rect.y, Mathf.Max(rect.width, Touch * 1.5f),
                Mathf.Max(rect.height, Touch));
            var hover = GUI.Button(target, GUIContent.none);
            var lifted = hover && enabled;
            GUI.DrawTexture(target, Solid(Fade(lifted ? Brighten(face, 0.10f) : face, enabled ? 1f : 0.4f)));
            Label(target, text, Button, enabled ? ink : Fade(ink, 0.55f));
            return lifted;
        }

        public static Color Brighten(Color color, float amount) =>
            new Color(
                Mathf.Clamp01(color.r + amount),
                Mathf.Clamp01(color.g + amount),
                Mathf.Clamp01(color.b + amount),
                color.a);

        public static void ResetForTests()
        {
            builtFor = -1;
            NoWrap.Clear();
        }
    }

    /// <summary>
    /// The hub's warmer, friendlier register.
    ///
    /// In-run surfaces are dark and instrument-like because they sit over a busy scene and
    /// must not compete with it. The hub sits on a painted sunset street, so it gets cream
    /// paper, terracotta and rounded chunky controls instead. Same tokens, different voice.
    ///
    /// LAYOUT RULE, learned the hard way. Within one drawing pass, pick one coordinate
    /// system and stay in it:
    ///
    ///   GUILayout pass - allocate each row with GUILayoutUtility.GetRect and draw the row
    ///   contents into the rect it returns. This is the only pattern that scrolls, because
    ///   the layout cursor is what advances as content overflows.
    ///
    ///   Rect pass - position everything by hand from a content rect. Fine for a fixed
    ///   panel like the death screen or paywall. Must not be wrapped in a scroll view.
    ///
    /// Mixing them is what produced three separate defects in one session: an empty hub
    /// card, a START RUN bar drawn under its own label, and upgrade icons that never
    /// appeared. IMGUI does not error when this is wrong; it just draws in the wrong place.
    /// </summary>
    public static class HubSkin
    {
        /// <summary>Parchment card behind hub content.</summary>
        public static Color Paper => new Color(0.99f, 0.94f, 0.84f, 0.96f);

        public static Color PaperEdge => new Color(0.85f, 0.74f, 0.58f, 0.85f);

        /// <summary>Warm ink for text on parchment. Never pure black; it reads as a stain.</summary>
        public static Color Ink => new Color(0.24f, 0.16f, 0.13f, 1f);

        public static Color InkSoft => new Color(0.44f, 0.34f, 0.29f, 1f);

        /// <summary>
        /// Lightened from 0.86,0.42,0.24 so dark ink on it measures 4.83:1 rather than
        /// 4.03:1. Lightening the face was chosen over darkening Ink, which is the body
        /// text colour for the whole parchment register.
        /// </summary>
        public static Color Terracotta => new Color(0.89f, 0.50f, 0.28f, 1f);

        public static Color Honey => new Color(0.98f, 0.74f, 0.31f, 1f);

        /// <summary>
        /// Darkened from 0.36,0.61,0.38, which measured 2.92:1 on Paper. This measures
        /// 4.78:1, so a status chip is readable rather than decorative.
        /// </summary>
        public static Color Leaf => new Color(0.26f, 0.46f, 0.28f, 1f);

        // Design sizes, exposed so layout budgets and the styles that render them cannot
        // drift apart. These are the numbers RunTabMetrics and the layout tests budget
        // against; the styles below build from the same constants rather than repeating
        // the literals. GUIStyle cannot be constructed outside OnGUI, so a test that needs
        // to reason about type size has to start from these.
        public const float HeadingSize = 27f;
        public const float HeadingMax = 34f;
        public const float WordmarkSize = 40f;
        public const float WordmarkMax = 50f;
        public const float RowSize = 19f;
        public const float RowMax = 25f;
        public const float SmallSize = 15f;
        public const float SmallMax = 19f;

        public static GUIStyle Heading => Skin(20, () => UiTheme.WithFont(new GUIStyle(GUI.skin.label)
        {
            fontSize = (int)UiTheme.Px(HeadingSize, HeadingMax),
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleLeft
        }));

        public static GUIStyle Wordmark => Skin(21, () => UiTheme.WithFont(new GUIStyle(GUI.skin.label)
        {
            fontSize = (int)UiTheme.Px(WordmarkSize, WordmarkMax),
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleLeft
        }));

        public static GUIStyle Row => Skin(22, () => UiTheme.WithFont(new GUIStyle(GUI.skin.label)
        {
            fontSize = (int)UiTheme.Px(RowSize, RowMax),
            alignment = TextAnchor.MiddleLeft
        }));

        public static GUIStyle Small => Skin(23, () => UiTheme.WithFont(new GUIStyle(GUI.skin.label)
        {
            fontSize = (int)UiTheme.Px(SmallSize, SmallMax),
            alignment = TextAnchor.MiddleLeft
        }));

        public static GUIStyle Button => Skin(24, () => UiTheme.WithFont(new GUIStyle(GUI.skin.button)
        {
            fontSize = (int)UiTheme.Px(18f, 23f),
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        }));

        public static GUIStyle TabButton => Skin(25, () => new GUIStyle(Button)
        {
            fontSize = (int)UiTheme.Px(17f, 21f)
        });

        public static GUIStyle TabActive => Skin(26, () => new GUIStyle(TabButton));

        private static GUIStyle Skin(int key, System.Func<GUIStyle> build)
        {
            // Scale and font version together: a font swap must rebuild the hub skin too.
            var token = Mathf.RoundToInt(UiTheme.Scale * 1000f) + UiTheme.StyleVersion * 7919;
            if (builtFor != token)
            {
                cache.Clear();
                builtFor = token;
            }

            if (cache.TryGetValue(key, out var cached) && cached != null)
            {
                return cached;
            }

            var created = build();
            cache[key] = created;
            return created;
        }

        private static readonly Dictionary<int, GUIStyle> cache = new Dictionary<int, GUIStyle>();
        private static int builtFor = -1;

        /// <summary>A parchment card with a soft drop shadow and a warm edge.</summary>
        public static void Card(Rect outer)
        {
            var shadow = 5f * UiTheme.Scale;
            UiTheme.DrawTexture(UiTheme.Offset(outer, shadow * 0.4f, -shadow),
                UiTheme.Solid(UiTheme.Fade(UiTheme.Ink, 0.22f)));
            UiTheme.DrawTexture(outer, UiTheme.Solid(Paper));
            UiTheme.DrawTexture(new Rect(outer.x, outer.y, outer.width, Mathf.Max(1f, 2f * UiTheme.Scale)),
                UiTheme.Solid(PaperEdge));
        }

        public static Rect Inset(Rect outer)
        {
            var pad = 18f * UiTheme.Scale;
            return new Rect(outer.x + pad, outer.y + pad, outer.width - pad * 2f, outer.height - pad * 2f);
        }

        /// <summary>A chunky pill button with a pressed look, drawn on parchment.</summary>
        public static bool Pill(Rect rect, string text, Color face, Color? ink = null, bool enabled = true)
        {
            var target = new Rect(rect.x, rect.y, Mathf.Max(rect.width, UiTheme.Touch * 1.4f),
                Mathf.Max(rect.height, UiTheme.Touch * 0.8f));
            var hit = GUI.Button(target, GUIContent.none) && enabled;
            var lift = hit ? -2f * UiTheme.Scale : 0f;
            var body = UiTheme.Offset(target, 0f, lift);

            // A darker lip under the face is what makes a flat IMGUI rect read as a key.
            UiTheme.DrawTexture(UiTheme.Offset(body, 0f, 3f * UiTheme.Scale),
                UiTheme.Solid(UiTheme.Fade(face, enabled ? 0.45f : 0.2f)));
            UiTheme.DrawTexture(body, UiTheme.Solid(enabled ? face : UiTheme.Fade(face, 0.35f)));
            UiTheme.Label(body, text, Button, enabled ? (ink ?? Ink) : UiTheme.Fade(ink ?? Ink, 0.5f));
            return hit;
        }

        /// <summary>Soft tinted pill, used for status chips like weather and ad state.</summary>
        public static void Chip(Rect rect, string text, Color tint)
        {
            UiTheme.DrawTexture(rect, UiTheme.Solid(UiTheme.Fade(tint, 0.22f)));
            UiTheme.Label(rect, text, Small, UiTheme.Fade(tint, 1f));
        }

        /// <summary>A short rule that separates sections of a card.</summary>
        public static void Rule(Rect rect)
        {
            UiTheme.DrawTexture(new Rect(rect.x, rect.y + rect.height * 0.5f, rect.width,
                Mathf.Max(1f, 1.5f * UiTheme.Scale)), UiTheme.Solid(PaperEdge));
        }
    }

    /// <summary>Easing and entrance timing for the UI.</summary>
    public static class UiMotion
    {
        public static float EaseOutCubic(float t)
        {
            t = Mathf.Clamp01(t);
            return 1f - Mathf.Pow(1f - t, 3f);
        }

        public static float EaseInOutCubic(float t)
        {
            t = Mathf.Clamp01(t);
            return t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) * 0.5f;
        }

        /// <summary>Overshoots slightly then settles. For panels and score pops.</summary>
        public static float EaseOutBack(float t)
        {
            t = Mathf.Clamp01(t);
            const float overshoot = 1.70158f;
            var p = t - 1f;
            return 1f + (overshoot + 1f) * p * p * p + overshoot * p * p;
        }

        /// <summary>0 to 1 across a duration from a start stamp, unscaled so it survives pause.</summary>
        public static float Progress(float startTime, float duration)
        {
            if (duration <= 0f)
            {
                return 1f;
            }

            return Mathf.Clamp01((Time.unscaledTime - startTime) / duration);
        }

        /// <summary>A decaying 0..1 pulse for attention without a per-frame allocation.</summary>
        public static float Pulse(float speed = 6f)
        {
            return 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * speed);
        }
    }
}
