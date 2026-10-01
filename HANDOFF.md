# Handoff

## Spec questions

Resolved, with the decision recorded in code:

- `DELIVERY_SCORE_PULSE` and its duration: `Constants.DELIVERY_SCORE_PULSE` is 1.5 for
  `PackageManager.DeliveryScoreDuration` (2 s), applied by `ScoreManager.DeliveryScoreMultiplier`.
- Package refill after delivery: `RunCoordinator.HandleCheckpointReached` refills only when
  `loadout.PackageSlots > 1`. A single-slot courier picks up a new parcel at the next leg's
  normal assignment instead.
- Continue invincibility: 2 s, granted by `PlayerController.RespawnFromContinue`, and a second
  death inside the window is absorbed.
- Rewarded-ad reward: continue-only, granted exactly once per run. No coin bonus ships.
- District reach vs purchase: reach gates are free at 600 m and 1200 m; the packs sell a 1.25x
  coin bonus in their own district.

Still open, and none block the Next Gen submission:

- Camera orthographic size and aspect-ratio behaviour on unusual device aspect ratios. The
  fallback route is verified in the editor; a phone with an extreme aspect needs a look.
- Package and checkpoint art requirements, specifically whether the parcels need to be visible
  in the world outside the cat's carry slot. Currently they are not.
- Ad-failure user feedback copy. `AdManager.StatusMessage` is displayed as-is; the wording is
  placeholder.

## Requests

- Provide a RevenueCat **Test Store** public key locally for the Next Gen Android demo. Do not
  paste keys into chat or commit them. `Cat Courier > Setup > Use Real RevenueCat for Next Gen
  Demo` selects the config asset and flips the two enums; the key is pasted into the Inspector.

## Blockers

- RevenueCat SDK cannot execute in the Unity Editor; Android sandbox device execution is still required for the Day 1 spike.
- No connected Android device or emulator with ADB was detected for purchase, restore, customer-info, touch, animation-visibility, collision, or frame-stability checks.
- Day 1 SDK details and go/no-go decision were recorded in the local `docs/revenuecat-spike.md`, which is gitignored and not part of this repository: RevenueCat purchases are supported on Android/iOS; RevenueCat is not a Unity ad-serving SDK; use AdMob for serving.
- RevenueCat Ads does not provide a Unity ad-serving SDK. Current docs describe AdMob integration and RevenueCat ad-event/reward tracking. Use AdMob directly on Day 5 after availability spike; keep `IAdBackend` abstraction.
- Day 6 remaining blockers, all content or account side, confirmed by `Cat Courier > Validate > Release Readiness`: `ChunkCatalog.asset` is empty, Harbour and Suburbs have no authored chunks, and there are no `AudioClip` or `AudioMixer` assets. The `CatBreedConfig` blocker is resolved: the generated art installer now creates five breed assets. Code paths for the rest are wired and safe when empty.
- Day 6 checks that cannot be automated here: Android and iOS device runs, real RevenueCat sandbox purchase/restore/trials, touch and animation-visibility feel, and representative-hardware frame pacing.
- Day 6 PlayMode suite stubs scene loading, so real Hub and Game scene wiring plus `RunCoordinator` serialized references are verified by manual play only. `GameScenePlayabilityTests` and `GeneratedCatArtTests` are the exceptions: they load the real scenes additively, so they inherit the harness save-path guard.
- `Next Gen` submission blockers: resolved. The `harbour_unlock` and `suburbs_unlock` packs used to unlock districts that already unlock free at 600 m and 1200 m. The reach gates are single-sourced in `RunLoadoutService`, and each pack now grants a 1.25x coin multiplier while the run is in its own district, applied by `RunCoordinator` on district change and on entitlement change.

## Done hand-offs

- 2026-09-25: Day 1 source/config scaffold, shared contracts, save implementation/tests, SDK boundary, and docs created.
- 2026-09-25: Unity `2022.3.62f3` installed; packages resolved; project imported; Android Build Support, SDK/NDK Tools, OpenJDK, and Platform 33 installed; Android APK build succeeded; Edit Mode suite passed 25/25.
- 2026-09-25: Local ignored `RevenueCatConfig` generated and assigned to Boot. Public sandbox keys and device execution remain pending.
- 2026-09-25: MIT license approved and recorded.
- 2026-09-25: Day 3 systems implemented: seeded procedural selection, catalog validation/variants, chunk pooling contracts, district/checkpoint coordination, static/stumble/bounce/patrol/rolling obstacles, weather modifiers, package assignment/timers/delivery, run loadouts, story-beat persistence, and HUD state presenter. All 63 EditMode tests pass.
- 2026-09-25: Day 4 implemented: guarded RevenueCat environment/backend configuration, neutral offering/package caching, fake and real backend boundaries, manager lifecycle states, entitlement APIs, persistent third-run paywall gating, neutral paywall presenter, and monetization tests. All 68 EditMode tests pass.
- 2026-09-26: Day 5 implemented: atomic SaveSystem economy transactions, six upgrade systems, starter cat breeds with entitlement-aware selection and runtime bonuses, ad gating with reward-once fake backend, free=1/premium=3 continue quotas, two-second continue invincibility, package leg replenishment, Hub/run/death/pause/upgrade/cat/leaderboard presenters, and silent-safe audio with crossfade/event relay. All 79 EditMode tests pass.
- 2026-09-25: Day 3 setup creates an empty `ChunkCatalog.asset`; authored Old Town/Downtown chunk prefabs are still required before endless generation can produce actual district chunks. Missing entries are skipped safely.
- 2026-09-26: Day 6 integration and hardening implemented: PlayMode test assembly (17 tests covering boot, pause, continue respawn, catalog fallback, seeded generation soak, and repeated run cleanup), release-readiness validator, `Tools/` verification scripts, `docs/verification.md`, save-path test seam, authored audio/catalog wiring on `PersistentSystems`, and a full README rewrite.
- 2026-09-26: Day 6 defects found and fixed during review: fake-backend entitlement loss on Hub reload, district entitlements never consumed, package legs not refilling after delivery, `PlayerStats` shared instance mutated at runtime, premium coin multiplier never applied, ad failure clearing another ad's in-flight state, ad continue granted after the offer expired, run start silently blocked when RevenueCat is not ready, and Hub tab panels drawing outside any layout area.
- 2026-09-26: Day 6 verification results: EditMode 79/79, PlayMode 17/17, cold-import `Apply All` + EditMode 79/79, secret scan 0 findings across 281 authored files, JSON validation pass, Android build with 0 errors and 0 warnings.
- 2026-09-26: Release readiness report written to `Logs/release-readiness.md`: 19 pass, 7 warn, 7 blocker. All blockers are missing authored content (chunk prefabs, audio clips, mixer, paid breed art), not code defects.
- 2026-09-26: Over-engineering audit applied: deleted three unreferenced files (`HubStartButton`, `PauseButton`, `NeighborhoodTheme`), removed dead `SaveSystem` mutators (`TrySpendCoins`, `UnlockCat`, `MarkStoryBeatSeen`), the unused `RunLoadout.EligibleDistricts` field, and two redundant `EntitlementChecker` accessors; replaced the hand-written 20-line `PlayerStats.Clone` with `MemberwiseClone`. Net -78 runtime lines across 3 fewer files, 0 dependencies.
- 2026-09-26: Added `CatCourierProjectSetup.RemoveMissingScripts` to `ApplyAll` so deleting a component class cannot leave a missing script in a saved scene. It also caught a real break: setup still called `AddComponent<UI.PauseButton>()`.
- 2026-09-26: Final Android development APK rebuilt at `Builds/Android/CatCourier.apk`; SHA-256 `7086C49FA546EC30119644D3F1E30B5F3576B9A51BEA2F4A64471E355541068E`.
- 2026-09-26: Day 4 real SDK sandbox purchases, restore, trial metadata, and device-only checks remain pending local public sandbox keys and a connected Android device. No Git operations were performed.
- 2026-09-26: Day 5 manual gates remain: authored cat/upgrade/district content, real ad SDK/provider selection, audio clips/mixer assets, and Android device verification of upgrades, purchases, ads, continue, UI, audio, persistence, and performance.
- 2026-09-25: Day 2 spec decisions recorded: wall-bounce duration `0.15f`, combo value uses raw coin value, score difficulty uses D0/D5/D10 interpolation, and integer awards round midpoint away from zero.
- 2026-10-01: Fix pass on `codex/playable-cat-run`. Player-facing: abandoning a run now banks its coins, the death screen shows the live bank instead of the pre-run save value, and a failed save no longer traps the player on the death screen. Ads: the per-run interstitial and reward caps survive a continue, a failed load no longer burns the run's ad, and a build with no ad backend skips the interstitial entirely. Economy: the two district unlock packs now grant a 1.25x coin bonus in their own district instead of unlocking content that already unlocks free, and the premium 2x rule has a single owner. Test integrity: the false-green RevenueCat boundary test, the order-dependent EditMode fixtures, and the two PlayMode suites that lacked the save-path guard are all fixed. Gameplay: the fallback route has a real difficulty curve and logs its seed, `StumbleObstacle` no longer corrupts run speed, and screen shake is wired. UI: every menu is inset by the safe area, and the HUD shows the carried parcel, its urgent countdown, and the fragile warning that used to be invisible. Verified 150/150 EditMode and 31/31 PlayMode on Windows; secret scan clean.
- 2026-10-01: Cleanup pass. Removed nine events that were invoked but never subscribed to, so the API stopped advertising behaviour that did not happen. Cached four `FindObjectOfType` lookups that ran per frame or per trigger. Fixed a lapsed premium silently swapping the player's cat for a starter with no explanation, which the hub now reports. Single-sourced the package to offering mapping, which had three copies that could drift. The RevenueCat re-init path was dead, because a failed init disposed the backend and nothing rebuilt it; it now does, so a tester can recover after fixing a key. A failed restore or refresh no longer marks the whole SDK degraded until restart. Chunk recycling now drains every chunk behind the camera instead of one per frame, which had no headroom above the current count. A missing RevenueCat config is a release blocker rather than a warning, since a development build in that state runs on fake purchases.
- 2026-10-01: Closed five of the nine open spec questions, each recorded next to the decision in `HANDOFF.md` and in the code. Four remain open and none block the submission; see the top of this file.
- 2026-10-01: First Android development build covering the generated art and the current gameplay. Succeeded with 0 compiler errors and 0 warnings, IL2CPP for ARMv7 and ARM64, target SDK 34, 276.6 MB, SHA-256 `1934307041A19339FB3CEAAFF514EA928383459BB9E89168410273BC1C198130`. This supersedes the 2026-09-26 APK, which predated the art. The size is almost entirely the 239 generated PNGs, imported uncompressed for fast in-game loads. It has **not** been run on a device: touch feel, safe-area layout, frame pacing, and the real RevenueCat purchase are all still unverified, and this build uses the fake monetization backend because no Test Store key has been entered.
- The `docs/` directory is gitignored, so the verification guide and spike notes referenced by older entries are local-only and not in this repository.
