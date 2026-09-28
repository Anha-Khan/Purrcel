# Cat Courier

Cat Courier is a landscape 2D endless runner built with Unity 2022.3 LTS, C#, URP, and RevenueCat. A delivery cat auto-runs across procedurally assembled rooftops, collects coins, carries packages, avoids obstacles, survives weather and district changes, and spends earned coins on upgrades and cat breeds between runs.

Repository: <https://github.com/Anha-Khan/Cat>

---

## Table of contents

- [Status](#status)
- [What is not finished yet](#what-is-not-finished-yet)
- [Requirements](#requirements)
- [Getting started](#getting-started)
- [Running the game](#running-the-game)
- [Controls](#controls)
- [Gameplay systems](#gameplay-systems)
- [Tuning values](#tuning-values)
- [Progression](#progression)
- [Economy and monetization](#economy-and-monetization)
- [Save data](#save-data)
- [Tests](#tests)
- [Building](#building)
- [Registering new content](#registering-new-content)
- [Repository layout](#repository-layout)
- [Team roles](#team-roles)
- [License](#license)

---

## Status

The game is implemented and verified on Windows in Unity `2022.3.62f3`. All gameplay systems, progression, monetization boundaries, and persistence are complete and covered by tests.

| Area | State |
|---|---|
| Player controller, input, physics | Complete |
| Procedural generation, chunk catalog, pooling | Complete (empty catalog, see below) |
| Obstacles, weather, packages, checkpoints | Complete |
| Scoring, combos, difficulty scaling | Complete |
| Economy, upgrades, cat breeds | Complete |
| RevenueCat boundary, entitlements, paywall | Complete (device verification pending) |
| Ad gating with backend abstraction | Complete (fake backend only) |
| Audio routing, crossfade, event relay | Complete (no clips yet) |
| UI presenters (hub, HUD, pause, death) | Complete (IMGUI placeholders) |
| Persistence with atomic writes and recovery | Complete |

Verification: **79/79 EditMode tests**, **17/17 PlayMode tests**, cold-import run passing, Android development build with 0 compile errors and 0 warnings, secret scan clean across the authored tree.

## What is not finished yet

These are content and account gates, not code defects. Nothing in this repository claims otherwise:

- **Authored chunk prefabs.** `ChunkCatalog.asset` is empty, so procedural generation safely falls back to a flat test track. The game is playable but has no real district geometry.
- **Art.** Player, cat, chunk, and UI visuals are functional placeholders. The UI is IMGUI-based and is not a shipping interface.
- **Audio clips and mixer.** The audio system is fully wired and silent-safe, but no clip assets exist yet.
- **A real ad SDK.** Only a deterministic fake backend ships. Ad serving is a provider decision; see [Economy and monetization](#economy-and-monetization).
- **Real RevenueCat sandbox purchases, restore, and trial metadata.** These require local public sandbox keys and a physical device.
- **Device-level verification** of touch feel, animation visibility, and frame pacing. iOS export requires macOS with Xcode.

Unfinished paid content is never sold. Breed packs and district packs stay hidden from the paywall until real content and matching entitlements exist, and Harbour and Suburbs remain locked until chunk content is registered. The in-game hub labels them accordingly.

---

## Requirements

- Unity `2022.3.62f3`
- Android Build Support, Android SDK & NDK Tools, and OpenJDK for local Android verification
- iOS Build Support on macOS for iOS export
- A Unity account with an accepted license

Packages resolve from `Packages/manifest.json`:

| Package | Version |
|---|---|
| Input System | 1.7.0 |
| Universal Render Pipeline | 14.0.12 |
| Unity Test Framework | 1.1.33 |
| RevenueCat Purchases Unity | 9.11.1 |
| External Dependency Manager | 1.2.189 |

Project configuration: Linear color space, IL2CPP scripting backend, .NET Standard 2.1 API level, landscape orientation, 60 FPS target, ARMv7 + ARM64 for Android, iOS deployment target 14.0.

---

## Getting started

1. Install Unity `2022.3.62f3` and Android Build Support.
2. Clone or download this repository and open the folder with that editor version.
3. Allow package import to finish.
4. Run **`Cat Courier > Setup > Apply All`**. It is idempotent and creates or repairs scenes, URP settings, the chunk catalog, the audio library, and a local RevenueCat config asset. On a fresh clone this first run also performs the initial import and compile that every later step depends on.
5. Enter RevenueCat Android/iOS **public sandbox keys** in `Assets/_Project/Config/RevenueCatConfig.asset`. This file is gitignored — never commit it.
6. Run **`Cat Courier > Validate > Release Readiness`** to print a PASS/WARN/BLOCKER report describing exactly what content is still missing.
7. Open `Assets/_Project/Scenes/Boot.unity` and enter Play mode.

> **Running command-line tests?** Run them before building, or at least let Unity import and compile once first. A `-runTests` invocation against a tree that last performed an Android player build can fail to resolve package assemblies such as Input System and URP, because they are rebuilt for the editor in the same pass. The project itself is valid; only the ordering is wrong. `Tools/verify-project.ps1` runs tests ahead of the build and also performs an import pass when it detects a cold clone.

The design specification and the implementation plan that drove this build are maintained locally and are intentionally not committed.

---

## Running the game

1. Open `Assets/_Project/Scenes/Boot.unity`.
2. Enter Play mode. Boot creates its persistent systems and loads the Hub.
3. Press **START RUN** on the Run tab to load the Game scene.
4. Play using the controls below. Pause with Escape.
5. On death, either spend a continue (one per run for free players, three for premium) or bank the run and return to the Hub.

## Controls

| Action | Input |
|---|---|
| Jump | Tap, Space, or the configured jump binding |
| Double jump | Jump again in mid-air, once unlocked |
| Slide | Downward swipe, S, or the configured slide binding |
| Pause | Escape or the pause control |

All bindings live in `Assets/_Project/Input/CatCourierControls.inputactions`.

---

## Gameplay systems

| System | Behavior |
|---|---|
| Player | Running, Jumping, Sliding, WallBounce, and Dead states; wall bounce, stumble, static and fall death; continue respawn with two seconds of invincibility |
| Coins | Base value scaled by premium, upgrade, and breed multipliers; magnet pickup radius from upgrades |
| Score | Distance plus package deliveries plus combo bonus, all scaled by the difficulty multiplier |
| Combos | Build by collecting without a gap wider than 8 units; multiplier steps at 10/20/30 coins; breaks on a miss, an obstacle, or death |
| Difficulty | Integer level 0–10 from distance, driving obstacle density, patrol speed, coin frequency, and score multiplier |
| Generation | Seeded weighted chunk selection, spacing rules, a fixed six-chunk pool, and a safe fallback when catalog entries are missing |
| Weather | Clear, rain, wind, and night, assigned once per run, each with distinct physics or visibility effects |
| Packages | Slot-based carry limit from upgrades, urgent delivery timer with a premium bonus, per-checkpoint delivery scoring, and per-leg replenishment |
| Districts | Old Town and Downtown always available; Harbour at 600 m, Suburbs at 1200 m, or via entitlement, or unlocked by premium |
| Story beats | One-time district story cards persisted so they never repeat |
| Economy | Atomic coin spend and purchase transactions that roll back completely if the write fails |
| Persistence | Atomic JSON save with backup, corruption quarantine, and safe default recovery |

---

## Tuning values

Central constants live in `Assets/_Project/Scripts/Core/Constants.cs` and are consumed by every system.

**Movement and physics**

| Value | Setting |
|---|---|
| Base run speed | 6.0 u/s, rising 0.05 u/s per second of run time, capped at 18.0 |
| Jump force | 14.0 · double jump 11.0 |
| Gravity / terminal velocity | −32.0 / −22.0 |
| Coyote time / jump buffer | 0.1 s / 0.1 s |
| Slide duration / hitbox height | 0.7 s / 0.4 units |
| Wall bounce | −4.0 horizontal, 8.0 vertical, 0.15 s |
| Player hitbox | 0.6 × 1.0 units |

**Scoring and economy**

| Value | Setting |
|---|---|
| Distance score | 10 per metre |
| Package score | 500 per delivery |
| Coin base / rare | 1 / 5 |
| Package delivery bonus | 15 coins |
| Delivery score pulse | ×1.5 for 2 s after a checkpoint |
| Combo break distance | 8.0 units |
| Difficulty step | 1 level per 150 m, capped at 10 |

**Procedural generation**

| Value | Setting |
|---|---|
| Chunk width / active chunk count | 20.0 units / 6 |
| Checkpoint interval | 300 m |
| Minimum gap between deadly obstacles | 2.5 units |
| Drone patrol width / speed | 3.0 units / 2.0 u/s |

Chunk selection weights interpolate across difficulty 0, 5, and 10 — for example SmallGap moves 30 → 20 → 10 while ObstacleDense moves 10 → 15 → 20. A checkpoint is always placed on the interval and always followed by an ObstacleSparse buffer, and two LargeGap chunks never appear back to back.

**Weather and packages**

| Value | Setting |
|---|---|
| Weather selection weights | Clear 40, Rain 25, Night 25, Wind 10 |
| Package selection weights | Normal 40, Fragile 20, Heavy 20, Urgent 20 |
| Urgent timer | 30 s + 0.18 s per metre to the next checkpoint, +15 s for premium |
| Fragile hard-landing threshold | −16.0 vertical |
| Wind drift | 1.8 u/s, direction randomised per run |
| Rain landing slide | 0.3 units |

**Audio**

Music crossfades over 1.5 s on scene and district changes, through Master, Music, SFX, and Ambient mixer groups.

---

## Progression

Six upgrades are purchased with coins between runs. Failed purchases leave coins, levels, and derived stats completely unchanged.

| Upgrade | Levels | Costs | Effect |
|---|---|---|---|
| Sprint Speed | 3 | 50 / 120 / 250 | Base run speed 6.0 → 6.5 → 7.0 → 8.0 |
| Jump Height | 3 | 60 / 150 / 300 | Jump force 14.0 → 15.5 → 17.0 → 19.0 |
| Double Jump | 2 | 200 / 400 | Unlocks double jump, then raises it 11.0 → 13.0 |
| Coin Magnet | 3 | 80 / 200 / 400 | Pickup radius 0 → 1.5 → 3.0 → 5.0 units |
| Coin Multiplier | 3 | 100 / 250 / 500 | 1.0× → 1.25× → 1.5× → 2.0× |
| Package Carry Limit | 2 | 150 / 350 | 1 → 2 → 3 package slots |

**Cats** carry additive speed and jump bonuses plus a multiplicative coin bonus. Tabby and Tuxedo are starters; premium-only and one-time-purchase breeds require both real content assets and a matching entitlement before they appear or can be selected.

---

## Economy and monetization

**Coin formula**

```text
finalCoins = rawCoins × premiumMultiplier × upgradeMultiplier × breedCoinMultiplier
```

Precedence is free 1× / premium 2×, then the upgrade multiplier, then the breed multiplier. Awards round to the nearest integer with midpoints away from zero, and coins are banked on run end whether the run is banked or lost.

**Entitlements** are cached and the UI updates on `OnEntitlementsChanged`:

| Entitlement | Grants |
|---|---|
| `premium` | 2× coins, no interstitials, 3 continues per run, all districts and premium cats |
| `rare_breeds_pack` | Scottish Fold, Sphynx, Munchkin |
| `legendary_cats_pack` | Manx, Turkish Van, Lykoi |
| `harbour_district` | Harbour district |
| `suburbs_district` | Suburbs district |

Offerings are `default` (monthly and annual subscriptions), `iap_breeds` (rare and legendary packs), and `iap_districts` (Harbour and Suburbs). Prices are always read live from RevenueCat; nothing is hardcoded, and the paywall prints an explicit development-fake-data banner when the fake backend is active.

**Paywall triggers** on the third completed run (first time only), on tapping a locked premium feature, and from the hub's Go Premium button.

**Ads** go through an `IAdBackend` interface. Premium suppresses interstitials entirely; free players see at most one death-screen interstitial per run, and a rewarded continue grants exactly once regardless of duplicate callbacks. RevenueCat is a purchases SDK and does not serve ads, so no ad provider is bundled — implementing the interface is the integration point.

---

## Save data

The save file is `save.json` under `Application.persistentDataPath`:

- **Windows** — `%USERPROFILE%\AppData\LocalLow\Cat Courier\Cat Courier\save.json`
- **Android** — `/storage/emulated/0/Android/data/com.catcourier.game/files/save.json`

It stores the coin bank, upgrade levels, selected and unlocked cats, unlocked districts, seen story beats, run history (best 10), completed run count, and paywall and trial flags.

Writes are atomic: serialize to a temporary file, validate it can be read back, replace the real file, and keep a backup until the replacement succeeds. A corrupt or missing save resets to defaults rather than crashing. Tests never touch the real path; they use temporary directories and assert the real path is unmodified.

---

## Tests

### Unity Test Runner

1. Open **Window > General > Test Runner**.
2. Select **EditMode**, run all — 79 tests.
3. Select **PlayMode**, run all — 17 tests.

The PlayMode suite proves save-path isolation by snapshotting `Application.persistentDataPath` and failing if it changes.

### Command line

```powershell
pwsh -File Tools/verify-project.ps1                    # secret scan, JSON, EditMode tests, Android build
pwsh -File Tools/verify-project.ps1 -SkipAndroidBuild  # skip the slow stage
pwsh -File Tools/verify-fresh-import.ps1               # cold-import proof with no Library cache
pwsh -File Tools/clean-generated-artifacts.ps1         # tidy generated logs and reports
```

The secret scanner never prints a matched value — only the path, line, rule, and length — and never performs a Git operation. Exit code is `0` on pass and `1` on failure.

---

## Building

### Android

1. Install Android Build Support and set the SDK path, or export `CAT_COURIER_ANDROID_SDK`.
2. Run **`Cat Courier > Build > Build Android (Development)`**.
3. The APK is written to `Builds/Android/CatCourier.apk`.

Package `com.catcourier.game`, version `0.1.0`, min SDK 24, target SDK 33, ARMv7 + ARM64. Build artifacts are gitignored; attach the APK as a release asset rather than committing it. This is a development build, which is also why the fake monetization backend is the default in it.

### iOS

Requires macOS with Xcode and iOS Build Support:

1. Open the project on macOS with Unity `2022.3.62f3`.
2. Run **`Cat Courier > Setup > Apply All`**.
3. Enter the Apple public sandbox key in `Assets/_Project/Config/RevenueCatConfig.asset`.
4. Set the signing team and bundle identifier in Player Settings.
5. Export from **File > Build Settings**.

### RevenueCat setup

1. Create a RevenueCat project and Android/iOS app.
2. Add the sandbox products and entitlements listed above.
3. Run **`Cat Courier > Setup > Apply All`** to create the local config asset and assign it to Boot.
4. Enter the public sandbox keys locally. Never commit the config asset.
5. Build to a device. The RevenueCat SDK does not execute in the Unity Editor.

---

## Registering new content

Every content slot is optional at runtime, so content can be added without touching code.

| Content | Where it goes | Ships automatically? |
|---|---|---|
| District chunk prefab | Add to `ChunkCatalog` for that district and chunk type, named `Chunk_[Type]_[Code]_[Variant]` with start and end markers at X 0 and 20 | Yes, once the catalog validates |
| Cat breed | Create a `CatBreedConfig` asset and assign it to the `CatBreedManager` in the scene | Yes |
| Upgrade | Create an `UpgradeConfig` asset and assign it to the `UpgradeManager` in the scene | Yes, replacing the built-in default |
| Audio clip | Add it to the `AudioLibrary` asset under the matching id | Yes |
| Paid breed or district | Set `isIAP` and `iapPackId` on the config, or register district chunks | Only once content exists; the paywall stays hidden otherwise |

After registering, run **`Cat Courier > Setup > Apply All`** and then **`Cat Courier > Validate > Release Readiness`** to confirm the blockers cleared. The readiness check reports missing chunks, unwired audio, and unshippable paid content without fabricating assets or failing the build.

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
Assets/_Project/Scripts/UI/            hub, HUD, pause, death, story and paywall presenters
Assets/_Project/Scripts/Monetization/  RevenueCat boundary, entitlements, ad gating
Assets/_Project/Scripts/Audio/         audio manager, library, event relay
Assets/_Project/Editor/                idempotent setup and release readiness validation
Assets/_Project/Config/                chunk catalog, audio library, local RevenueCat config
Assets/_Project/Scenes/                Boot, Hub, Game
Assets/Tests/EditMode/                 79 fast tests
Assets/Tests/PlayMode/                 17 scene and lifecycle tests
Tools/                                 local verification scripts
HANDOFF.md                             open questions, blockers, and delivery status
```

---

## Team roles

- **Engineering and systems** — gameplay, generation, progression, monetization boundary, audio, automated tests, build and verification tooling.
- **Content and art** — authored chunk prefabs, cat and UI art, audio clips and mixer, final district content.

The split is designed so neither role blocks the other. Content is registered through the catalog, library, and config assets, and every content slot degrades safely when empty.

---

## License

MIT License. See [`LICENSE`](LICENSE). All third-party packages remain under their own licenses.
