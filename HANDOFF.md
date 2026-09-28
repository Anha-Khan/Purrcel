# Handoff

## Spec questions

- `DELIVERY_SCORE_PULSE` needs one approved calculation and duration constant.
- Define package refill behavior after delivery when only one slot exists.
- Define continue invincibility collision behavior.
- Define rewarded-ad coin bonus, or confirm continue-only reward.
- Define delivery pulse score application and expiration.
- Define reach-unlock and purchase-unlock relationship for districts.
- Approve camera orthographic size and aspect-ratio behavior.
- Approve package/checkpoint art requirements without world pickups.
- Approve ad-failure user feedback.

## Requests

- Provide RevenueCat public sandbox keys locally. Do not paste keys into chat or commit them.

## Blockers

- RevenueCat SDK cannot execute in the Unity Editor; Android sandbox device execution is still required for the Day 1 spike.
- No connected Android device or emulator with ADB was detected for purchase, restore, customer-info, touch, animation-visibility, collision, or frame-stability checks.
- Day 1 SDK details and go/no-go decision are recorded in `docs/revenuecat-spike.md`: RevenueCat purchases supported on Android/iOS; RevenueCat is not a Unity ad-serving SDK; use AdMob for serving.
- RevenueCat Ads does not provide a Unity ad-serving SDK. Current docs describe AdMob integration and RevenueCat ad-event/reward tracking. Use AdMob directly on Day 5 after availability spike; keep `IAdBackend` abstraction.
- Day 6 remaining blockers, all content or account side, confirmed by `Cat Courier > Validate > Release Readiness`: `ChunkCatalog.asset` is empty, Harbour and Suburbs have no authored chunks, there are no `AudioClip` or `AudioMixer` assets, and no `CatBreedConfig` assets exist. Code paths for all of them are wired and safe when empty.
- Day 6 checks that cannot be automated here: Android and iOS device runs, real RevenueCat sandbox purchase/restore/trials, touch and animation-visibility feel, and representative-hardware frame pacing.
- Day 6 PlayMode suite stubs scene loading, so real Hub and Game scene wiring plus `RunCoordinator` serialized references are verified by manual play only.

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
- No Git operations were performed.
