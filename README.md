# Purrcel

A landscape 2D endless runner about a courier cat. Purrcel auto-runs across a streamed
Mediterranean-style rooftop route, collects coins, carries timed parcels, ducks and jumps
its way through weather and four districts, and banks what it earns into upgrades and cat
breeds between runs.

Built with **Unity 2022.3 LTS**, C#, URP, and the **RevenueCat** SDK.


> **Shipaton Next Gen submission.** This project is the Android demo entry. The purchase
> demonstration must happen on a physical device against a RevenueCat **Test Store** key;
> a purchase made in the Unity Editor is a local simulation and does not demonstrate the
> SDK. Full instructions are in [`NEXT-GEN-SUBMISSION.md`](NEXT-GEN-SUBMISSION.md).
>
> **Prebuilt APKs** are attached to the
> [GitHub releases](https://github.com/Anha-Khan/Purrcel/releases) rather than committed to
> the repository.

---

## Table of contents

- [Status](#status)
- [What is not finished](#what-is-not-finished)
- [Requirements](#requirements)
- [Getting started](#getting-started)
- [Screenshots and the app icon](#screenshots-and-the-app-icon)
- [The game](#the-game)
- [Controls](#controls)
- [Gameplay systems](#gameplay-systems)
- [Tuning values](#tuning-values)
- [Progression and economy](#progression-and-economy)
- [Monetization](#monetization)
- [Brand and design system](#brand-and-design-system)
- [Save data](#save-data)
- [Tests](#tests)
- [Building](#building)
- [Registering content](#registering-content)
- [Repository layout](#repository-layout)
- [License](#license)

---

## Status

Runs in Unity `2022.3.62f3`. The Game scene has a fully playable fallback route while
authored chunk prefabs are being built. Monetization is written and compiles, but has never
executed — only a device can prove it.

| Area | State |
|---|---|
| Player controller, input, physics | Complete |
| Procedural generation, chunk catalog, pooling | Playable fallback stream; authored catalog empty |
| Obstacles, weather, packages, checkpoints | Complete on the fallback route |
| Scoring, combos, difficulty scaling | Complete |
| Economy, upgrades, cat breeds | Complete |
| RevenueCat boundary, entitlements, paywall | Complete (device verification pending) |
| Ad gating with backend abstraction | Complete (fake backend only) |
| Audio routing, crossfade, event relay | Complete (17 synthesised clips through a real mixer) |
| UI presenters (hub, HUD, pause, death, paywall) | Complete (IMGUI, bundled typeface, measured contrast) |
| Brand identity, mark, app icon | Complete |
| Persistence with atomic writes and recovery | Complete |

**Verification:** `184/184` EditMode and `32/32` PlayMode, green on the tagged release
commit. The PlayMode suite drives a 120 m run through randomized hazards, verifies coin
collection, checks the Hub has a painted camera view, proves an abandoned run still banks
its coins, and proves a fallback-route checkpoint actually delivers a package.

**Latest Android build:** succeeded — IL2CPP, ARM64, target SDK 34, 267.4 MB. It has **not
been run on a device.** Its first launch on hardware once exposed a fatal bug that no editor
test could reach: an exception inside a coroutine silently prevented the Hub scene from
loading, leaving a black screen. Boot now contains a monetization failure, so a purchase SDK
can no longer stop the game from starting.

---

## Requirements

- Unity `2022.3.62f3` — pinned in `Tools/Common.ps1`. A newer editor may be installed
  alongside it; its API is not this project's API.
- Android Build Support, Android SDK & NDK Tools, and OpenJDK for local Android builds
- iOS Build Support on macOS for iOS export
- A Unity account with an accepted license

| Package | Version |
|---|---|
| Input System | 1.7.0 |
| Universal Render Pipeline | 14.0.12 |
| Unity Test Framework | 1.1.33 |
| RevenueCat Purchases Unity | 9.11.1 |
| External Dependency Manager | 1.2.189 |

Project configuration: linear color space, IL2CPP, .NET Standard 2.1, landscape, 60 FPS
target, ARM64 for Android, min SDK 24, target SDK 34, iOS deployment target 14.0.

---

## Getting started

1. Install Unity `2022.3.62f3` and Android Build Support.
2. Clone this repository.
3. In the Unity Hub choose **Add > Add project from disk**, select the folder, and pick the
   `2022.3.62f3` editor. Hub will offer to install it if missing.
4. **Let the first import finish.** It is slow, and every later step depends on it.
5. Run **`Purrcel > Setup > Apply All`**. It is idempotent, and on a fresh clone this first
   run also performs the import and compile. It creates or repairs scenes, URP settings, the
   chunk catalog, the audio library and mixer, the brand icons, and a local RevenueCat
   config asset assigned to Boot.
6. Enter a RevenueCat **Test Store public key** in
   `Assets/_Project/Config/RevenueCatConfig.asset` for the Android demo, or platform sandbox
   keys for store testing. **This file is gitignored — never commit it.**
7. Run **`Purrcel > Validate > Release Readiness`** to print a PASS / WARN / BLOCKER report
   of exactly what content is still missing.
8. Open `Assets/_Project/Scenes/Boot.unity` and enter Play mode.

> **Two editor traps, both learned the hard way.**
>
> **Unity only has one instance per project.** A second exits with `1073741845`. Close the
> editor before running the command-line verifier.
>
> **Build menu items are silently ignored in Play Mode** — no dialog, nothing in the log,
> and the build simply never starts, which looks exactly like a build that is taking forever.
> Stop Play Mode first. The Purrcel build items now detect this and say so rather than
> failing quietly.

> **Running command-line tests?** Run them before building, or at least let Unity import and
> compile once first. A `-runTests` invocation against a tree that last performed an Android
> player build can fail to resolve package assemblies such as Input System and URP, because
> they are rebuilt for the editor in the same pass. The project is valid; only the ordering
> is wrong. `Tools/verify-project.ps1` runs tests ahead of the build and performs an import
> pass when it detects a cold clone.

---

## Screenshots and the app icon

The required **1179 × 2556** screenshot must be captured from the running Android build. A
mockup presented as gameplay is not acceptable, and the store listing is judged on it.

The canonical app icon is the Purrcel mark, exported to
`Assets/_Project/Brand/icon-512.png`. The Play Console needs a **1024 × 1024** export, which
is the same artwork at a different size — not a separate design. `Purrcel > Setup > Apply
Brand Icons` assigns it to the Android player settings, and `Apply All` runs it, so a fresh
clone cannot ship without an icon.

Brand artwork is regenerated from source rather than hand-edited:

```bash
python Tools/brand/make-lockups.py      # master SVGs, including real letter outlines
python Tools/brand/make-brand-assets.py # launcher tiles, adaptive layers, bare marks
```

---

## The game

**Core run.** Auto-run courier across a streamed procedural route. Jump, double jump once
unlocked, slide, wall bounce. Death by static obstacle, pit, or falling road. A continue
respawns you with two seconds of invincibility. Pause and resume mid-run.

**Delivery loop.** You carry parcels with an urgent timer. Every 300 m the route places a
delivery checkpoint; reaching it delivers the parcels, pays coins and score, replays that
district's story card the first time you arrive, and unlocks the next district. This is the
core loop, not a placeholder.

**Districts.** Old Town and Downtown are free from the start. Harbour and Suburbs open at
600 m and 1200 m. Crossing a district boundary changes the parallax art, the weather, and
the music bed.

**Economy.** Coins bank on death *and* on abandoning a run. Spend them on upgrades and cat
breeds between runs.

**Difficulty.** Obstacle spacing and hazard mix tighten with distance; special-coin and
booster intervals shorten. The whole route is seeded and the seed is logged, so any run can
be reproduced exactly.

**Monetization.** RevenueCat purchases, entitlements, and a paywall behind a backend
interface, with a fake backend for local development so the editor never touches the
network.

**Audio.** 17 clips — one per SFX event, plus a looping bed per district and the hub —
routed through Master / Music / SFX / Ambient groups with a 1.5 s crossfade on district
change and a volume dip on pause.

**Persistence.** Atomic writes with a backup and a recovery path, so a failed save mid-run
cannot corrupt the profile or lose a banked run.

**Ads.** Interstitial gating with per-run caps, a backend interface, and a development fake.
No ad SDK ships.

---

## Controls

| Action | Input |
|---|---|
| Jump | Tap, Space, or the configured jump binding |
| Double jump | Jump again in mid-air, once unlocked |
| Slide | Downward swipe, S, or the configured slide binding |
| Pause | Escape or the pause control |

Bindings live in `Assets/_Project/Input/CatCourierControls.inputactions`.

> Touch controls are the primary target and the shipped default. **The pause binding is
> currently unbound on touch**, so a phone player cannot pause mid-run. This is a known gap.

---

## Gameplay systems

| System | Behaviour |
|---|---|
| Player | Running, Jumping, Sliding, WallBounce, Dead states; death by obstacle, pit, fall or stumble; continue respawn with 2 s invincibility |
| Coins | Base value scaled by premium, upgrade and breed multipliers; magnet radius from upgrades |
| Score | Distance plus package deliveries plus combo bonus, scaled by the difficulty multiplier |
| Combos | Build by collecting without a gap wider than 8 units; steps at 10 / 20 / 30 coins; breaks on a miss, an obstacle, or death |
| Difficulty | Integer level 0–10 from distance, driving obstacle density, patrol speed, coin frequency and score multiplier |
| Generation | Seeded weighted chunk selection, spacing rules, a six-chunk pool, and a safe fallback when catalog entries are missing |
| Weather | Clear, Rain, Night, Wind — assigned once per run, each with distinct physics or visibility effects |
| Packages | Slot-based carry limit from upgrades, urgent timer with a premium bonus, per-checkpoint delivery scoring, per-leg replenishment |
| Districts | Old Town and Downtown always available; Harbour at 600 m, Suburbs at 1200 m, or via premium. The two district packs add a 1.25× coin bonus in their own district |
| Checkpoints | Placed on a fixed interval on the fallback route; a checkpoint always delivers and always clears a radius around itself |
| Obstacles | A duck-under hazard carries an explicit `requiresDuckUnder` contract rather than relying on its name to imply it |
| Story beats | One-time district story cards, persisted so they never repeat |
| Economy | Atomic spend and purchase transactions that roll back completely if the write fails |
| Persistence | Atomic JSON save with backup, corruption quarantine, and safe default recovery |

---

## Monetization

Entitlements are cached and the UI updates on `OnEntitlementsChanged`.

| Entitlement | Grants |
|---|---|
| `purrcel_pro` | 2× coins, no interstitials, 3 continues per run, all districts, premium cats |
| `rare_breeds_pack` | Scottish Fold, Sphynx, Munchkin |
| `legendary_cats_pack` | Manx, Turkish Van, Lykoi |
| `harbour_district` | 1.25× coins while running in Harbour |
| `suburbs_district` | 1.25× coins while running in Suburbs |

Harbour and Suburbs already unlock free at 600 m and 1200 m, or with premium. The two
district packs therefore sell a coin bonus *in their own district*, not access to it. The
bonus follows the district as the run moves and applies immediately if bought mid-run.

Offerings are `default` (monthly and annual), `iap_breeds`, and `iap_districts`. Prices are
always read live from RevenueCat; nothing is hardcoded.

**Paywall triggers** on the third completed run (first time only), on tapping a locked
premium feature, and from the hub's Go Premium button.

**Key selection is explicit and tested.** A RevenueCat Test Store key is a *separate sandbox
credential* from the Play Store key, and the SDK rejects it in a release build — so it may
only be selected under `DEVELOPMENT_BUILD`. `RevenueCatKeySelectionTests` locks that down,
including an assertion that the readiness check can never disagree with the key that would
actually be sent to the SDK. That disagreement was a real bug: a correctly-pasted Test Store
key produced a build that passed every check and then handed the SDK an empty string.

**Ads** go through an `IAdBackend` interface. Premium suppresses interstitials entirely;
free players see at most one death-screen interstitial per run, and a rewarded continue
grants exactly once regardless of duplicate callbacks.

---
## Building

### Android

1. Install Android Build Support, or export `CAT_COURIER_ANDROID_SDK`.
2. Stop Play Mode if the editor is running.
3. Run **`Purrcel > Build > Android Development APK`**, or
   **`Purrcel > Build > Android Next Gen Demo APK`** for the submission.
4. The APK is written to `Builds/Android/CatCourier.apk`.

The Next Gen path refuses to build with a fake backend or a missing key, which is the
mistake most likely to waste a device test.

The most recent build is 267.4 MB, SHA-256
`676865C68DAFE167670838531E93794569D5B9F04F19D6F5916F56E3BD92A5C9`. That size is almost
entirely the generated art, which Unity imports uncompressed for fast in-game loads. Build
artifacts are gitignored; attach APKs to a release rather than committing them.

**ARM64 only** because IL2CPP compiles native code once per ABI and 32-bit devices are no
longer in real use. To add ARMv7 back, edit `CatCourierProjectSetup.ApplyPlayerSettings`.

> **Test Store keys only work in debuggable builds.** Never upload a build using one to
> Google Play — RevenueCat rejects such keys in release builds. This is why the submission
> APK is a development build and says so.

---

## Repository layout

```text
Assets/_Project/Scripts/Core/          game manager, save system, contracts, constants
Assets/_Project/Scripts/Player/        player controller, input
Assets/_Project/Scripts/Generation/    procedural generator, chunk catalog and manager
Assets/_Project/Scripts/Obstacles/     obstacle behaviours
Assets/_Project/Scripts/Packages/      package carry and delivery
Assets/_Project/Scripts/Coins/         coin pickup and value pipeline
Assets/_Project/Scripts/Scoring/       score, combos, difficulty
Assets/_Project/Scripts/Progression/   upgrades and cat breeds
Assets/_Project/Scripts/Weather/       weather selection and modifiers
Assets/_Project/Scripts/UI/            hub, HUD, pause, death, story, paywall, theme
Assets/_Project/Scripts/Monetization/  RevenueCat boundary, entitlements, ad gating
Assets/_Project/Scripts/Audio/         audio manager, library, event relay
Assets/_Project/Scripts/Art/           generated art, backdrop, UI sprites
Assets/_Project/Editor/                idempotent setup, brand icons, release readiness
Assets/_Project/Brand/                 exported launcher tiles, adaptive layers, marks
Assets/_Project/Config/                chunk catalog, audio library, mixer, local RevenueCat config
Assets/_Project/Audio/                 synthesised SFX and music (Tools/make-audio.py)
Assets/_Project/Resources/             bundled typeface and marks
Assets/_Project/Scenes/                Boot, Hub, Game
Assets/Tests/EditMode/                 184 fast tests
Assets/Tests/PlayMode/                 32 scene and lifecycle tests
Tools/                                 verification scripts, make-audio.py, brand generators
design.md                              the design system specification
```

---

## License

MIT License — see [`LICENSE`](LICENSE). All third-party packages and the bundled typeface
(Plus Jakarta Sans, SIL OFL 1.1) remain under their own licenses.
