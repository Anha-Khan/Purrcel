# Purrcel — design system

The locked design system for the game's identity and UI. Every presenter defers to this
file. Read it before changing any colour, size, layout, or the mark in
`Assets/_Project/Scripts/UI/`.

Enforced in code, not just documented:
`Assets/Tests/EditMode/UiContrastTests.cs` fails the build below WCAG AA.

---

## 1. Two registers, one scale

The game has two UI registers. They share a scale and a type ramp but nothing else.

| | In-run surfaces | Hub |
|---|---|---|
| Where | HUD, death screen, pause, story card, paywall | Run / Upgrades / Cats / Leaders |
| Ground | Moving painted scene | Painted street, left 63% column |
| Panel | Dark slate, accent cap, drop shadow | Parchment card, warm edge |
| Voice | Instrument: dense, dark, high-contrast | Place: warm, spacious, hand-drawn feel |

**Rule:** an in-run surface never uses parchment. A hub surface never uses slate.
Getting this wrong is how a screen ends up looking pasted on from somewhere else.

## 1a. The mark

**"Flap" — a coin that is a cat.** The circle is the run's coin purse; the two triangles
standing out of its top are a cat's ears, and an ear is an open parcel flap. It says
delivery without drawing a box, a van or an arrow. One sentence, and that is the test for
any change to it.

Master: `Tools/brand/purrcel-mark.svg`. Regenerate everything with
`python Tools/brand/make-lockups.py && python Tools/brand/make-brand-assets.py`.

| Version | Ratio | Where |
|---|---|---|
| Ink on Paper | **12.1:1** | hub, parchment register |
| Ink on Honey | **8.1:1** | launcher tile, store listing |
| Paper on Ink | **12.1:1** | night districts |
| Cream on Terracotta | 2.35:1 | **banned** — do not use |

**There is a small-size cut and it is not optional.** The nose is characterful at 24px and
becomes a smudge at 16px, so `mark-cut` (no nose) is used at ≤20px. One mark at every size
is the wrong answer here; the cut is why the mark survives a launcher icon.

Never recolour the mark outside the table. Never set live type in a lockup — the wordmark
outlines in `out/lockup-*.svg` are extracted from `PurrcelUI.ttf` at wght 700, so the logo
and the game share a typeface by construction. In the hub the wordmark stays live IMGUI
text so it scales with the UI; the mark is a texture.

## 2. Palette

All values are sRGB floats in `UiTheme` / `HubSkin`. Contrast column is measured, not
estimated — see §7.

### In-run (on `Slate` `#1F2E36`)

| Token | Value | Role | On slate |
|---|---|---|---|
| `Cream` | `#FFE8BD` | primary readouts | 11.4:1 |
| `Gold` | `#FAC95C` | currency, safe stats | 9.1:1 |
| `Muted` | `#9EB3BD` | secondary | 6.4:1 |
| `Honey` | `#FABD4F` | paywall headline | 8.3:1 |
| `Teal` | `#0F6E6E` | alternate action | — |
| `Danger` | `#F06B59` | urgent timer, **fake-data banner** | 4.6:1 |
| `Orange` | `#E88A33` | primary action face | — |

### Hub (on `Paper` `#FDF0D6`)

| Token | Value | Role | On paper |
|---|---|---|---|
| `Ink` | `#3D2921` | body text | 12.1:1 |
| `InkSoft` | `#704F49` | secondary | 5.9:1 |
| `Terracotta` | `#E38047` | tabs, START RUN, SELECT | 4.8:1 on `Ink` |
| `Honey` | `#FABD4F` | coin purse, rank-1 row | 8.1:1 on `Ink` |
| `Leaf` | `#427547` | unlocked, IN USE chip | 4.8:1 |
| `PaperEdge` | `#D9BD94` | card border, rules | — |

### Changed for contrast, and why

Three tokens were adjusted after audit. Do not "restore" them:

- `Danger` was `0.85,0.29,0.24` → **3.33:1** on slate. Unreadable, and it carried the
  **DEVELOPMENT FAKE DATA** warning. A judge failing to read that warning is the worst
  outcome this project has. Now 4.64:1.
- `Leaf` was `0.36,0.61,0.38` → **2.92:1** on parchment. Now 4.78:1.
- `Terracotta` was `0.86,0.42,0.24` → 4.03:1 with `Ink`. Lightened rather than darkening
  `Ink`, which is body text across the whole parchment register.

## 3. Type

**Plus Jakarta Sans** (SIL OFL 1.1), bundled at `Assets/_Project/Resources/PurrcelUI.ttf`
with its licence beside it. Loaded by name from `Resources`, so a fresh clone styles
correctly with no setup, and a missing file degrades to Arial rather than breaking.

It is a variable font: Unity renders one weight and synthesises bold. If bold ever looks
weak, add a static bold TTF rather than reaching for `fontStyle`.

### The line box is 1.26 em, and that number caused a real bug

Arial's line box is about **1.15 em**. Plus Jakarta Sans is **1.26 em** — 0.11 em per
fontSize taller. Every row in the game had been sized against Arial, so the day this font
landed, every heading showed only its top half: `Upgrade/name`, `Cats/name`, the leaderboard
rank and score were all short by 7–9px.

**Never hardcode a height for a text row.** Use:

| Need | Call |
|---|---|
| Height a row needs for its text | `UiTheme.MeasureHeight(style, text, width)` |
| Height of one line | `UiTheme.LineHeight(style)` |
| Width of one line | `UiTheme.MeasureWidth(style, text)` |
| A name/value that must stay on one line | `UiTheme.LabelClipped(rect, text, …)` |

`UiTheme.Label` measures and grows its own box, so a short box is not a reachable state.
Rows that can wrap should size their container from `MeasureHeight` — the upgrade card is
allocated from its description's measured height for exactly this reason.

Design sizes are constants on `HubSkin` (`HeadingSize`, `SmallSize`, `RowSize`, and their
`*Max` clamps) and the styles build from those same constants. Two independent numbers for
"how big is this text" is how the bug happened; there is now one.

### Testability limit — know this before writing a UI test

`GUIStyle` **cannot be constructed outside `OnGUI`**: touching `GUI.skin` in an Edit Mode
test throws *"You can only call GUI functions from inside OnGUI."* So `CalcHeight` and
`CalcSize` are unavailable in tests, and **string-width fitting cannot be unit-tested.**

For budgets, use `UiTheme.RequiredLineHeight(fontSize)`, which reads the line box off the
`Font` asset via `UiTheme.FontLineEm` — no IMGUI involved. `UiTextFitTests` uses this to
assert the font is present, that its line box is not Arial-like, and that every fixed row
height clears it.

The runtime guarantee is what covers string fitting. That split is deliberate: the test
guards the font, the code handles the text.

Ramp, in `UiTheme.Px` / `HubSkin`. `Px` scales by `UiTheme.Scale` and clamps at `max`:

| Role | UiTheme | Clamp | Use |
|---|---|---|---|
| Display | 46 | 62 | score, death-screen score |
| Title | 30 | 38 | panel headings |
| Body | 21 | 27 | default reading |
| Caption | 16 | 21 | secondary |
| Button | 20 | 26 | all controls |

Hub ramp is one step down: Wordmark 40, Heading 27, Row 19, Small 15, Button 18.

**The 62px clamp is a deliberate ceiling** tied to the single-weight font. Raising it
changes every screen at once — treat it as a whole-UI decision, not a local tweak.

## 4. Scale and spacing

`UiTheme.Scale` = `clamp(min(safeWidth/1280, safeHeight/720), 0.85, 1.5)`, derived from
`Screen.safeArea` so a notch inset cannot distort it. Reference resolution is **1280×720**.

Space in multiples of the scale: 4 / 8 / 10 / 12 / 18 / 24 / 30 / 40 / 52.

**Touch targets: 48dp minimum** (`UiTheme.Touch`). This is Android's guideline, not a
preference. The story card's Skip button was 24px before this was enforced.

## 5. LAYOUT RULE — read before writing a presenter

**Within one drawing pass, pick one coordinate system and stay in it.**

**GUILayout pass** — allocate each row with `GUILayoutUtility.GetRect(...)` and draw the
row's contents into the rect it returns.

```csharp
var row = GUILayoutUtility.GetRect(width, 68f, GUILayout.Width(width));
UiTheme.DrawTexture(row, face);
UiTheme.Label(new Rect(row.x + 16f, row.y + 10f, ...), "...", style, ink);
GUILayout.Space(10f);
```

**Rect pass** — position everything by hand from a content rect. Fine for a fixed panel
(death screen, paywall, HUD, hub Run tab). **Must not** be wrapped in a scroll view.

Mixing them is what produced three separate defects in one session: an empty hub card, a
START RUN button drawn under its own label, and upgrade icons that never appeared. IMGUI
does **not** error when this is wrong. It silently draws in the wrong place, and inside a
scroll view the content never moves.

## 6. Components

| | Pattern |
|---|---|
| **Panel** (`UiTheme.Panel`) | offset drop shadow → slate body → 2px accent cap → 16px inset |
| **Card** (`HubSkin.Card`) | offset soft shadow → parchment body → warm edge → 18px inset |
| **Button** (`UiTheme.FaceButton`) | in-run, min 48dp, `Brighten` on press |
| **Pill** (`HubSkin.Pill`) | hub, min 48dp, darker lip beneath for a raised key |
| **Bar** (`UiTheme.Bar`) | track + fill, used for urgent timer and upgrade level |
| **Chip** (`HubSkin.Chip`) | status only, never interactive |
| **Label** (`UiTheme.Label`) | measures its text and grows; never clips |
| **LabelClipped** (`UiTheme.LabelClipped`) | single line, ellipsis; for names, scores, dates |
| **Scrim** (`UiTheme.Scrim`) | flat wash behind a modal |
| **SoftScrim** (`UiTheme.SoftScrim`) | 6 nested bands growing outward; IMGUI cannot blur |

## 7. Motion

`UiMotion`. All timings use **unscaled** time so they survive pause.

| | |
|---|---|
| Easings | `EaseOutCubic` (panels) · `EaseOutBack` (drops, score pop) · `EaseInOutCubic` (exit) |
| Panel enter | 0.28–0.45s, scale 0.94→1 with alpha |
| HUD enter | 0.4s slide from left, `EaseOutCubic` |
| Score pop | 0.22s, one per **50** points — a pop per point reads as noise |
| Story card | 0.32s `EaseOutBack` in, 0.3s lift away over the last 0.3s of its life |
| Urgent pulse | only under 2s remaining, on colour not layout |

**Reduced motion:** there is no OS preference API in IMGUI, so motion is kept short,
non-essential, and confined to decoration. Nothing that communicates state is
motion-only — the urgent timer also changes colour, the countdown also shows a number.

## 8. Copy

- Controls name their action: **START RUN**, **KEEP RUNNING**, **CLOSE**, **BUY**.
- **No SDK identifiers in player-facing text.** `$rc_monthly` renders as `MONTHLY`.
- Dates are `4 Oct`, never a raw ISO timestamp.
- **The fake-data banner is mandatory and load-bearing.** It stays until the real backend
  is configured. Never restyle it into something that reads as decoration.
- Unshipped content says so plainly (`not shipped yet`) rather than hiding behind a lock.

## 9. Hard constraints

- **IMGUI has no CSS.** Do not plan web techniques; every value is an explicit rect.
- **Hand-laid rows need a measured height budget.** Absolute rects report nothing when
  they overlap. `RunTabMetrics` holds every row height and `RunTabLayoutTests` asserts
  both columns fit the card. Add a row there, not as an inline literal.
- **Alpha that compounds.** Nested scrim bands stack, so the centre is
  `1 - (1-p)(1-2p/6)…`, not `p`. The HUD scrim's `0.62` renders at `0.95`. Any contrast
  reasoning about it must model the composite, or it will be wrong in both directions.
- **No font asset existed before Batch 2.** Anything assuming Arial's metrics is now wrong.
- **Two Unity instances cannot open this project.** The second exits `1073741845`.
- Never commit `Assets/_Project/Config/RevenueCatConfig.asset` — it holds a live key.
- **This project builds with Unity 2022.3.62f3** (`Tools/Common.ps1` pins the CLI). A newer
  editor may also be installed and its API is *not* this project's API — the icon call
  differs between 2022.3 and Unity 6. Check the pinned version before using an editor API.