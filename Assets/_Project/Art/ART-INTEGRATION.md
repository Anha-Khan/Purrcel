# Generated art in the Unity game

This folder contains the 239 production PNGs from the Cat Courier art handoff. The 10 archived draft PNGs and catalogue previews were deliberately left out. Filenames and folders are unchanged so artists can find the source sheets.

## First import

Open this project in Unity 2022.3.62f3 and wait for sprite import. Then choose **Cat Courier > Setup > Apply All**. The setup runs the generated art installer after the existing game setup. For an already configured project, choose **Cat Courier > Art > Install Generated Art** instead. The installer saves the Boot and Game scenes and creates `Assets/_Project/Config/GeneratedArtCatalog.asset` plus five `CatBreedConfig` assets. It is safe to run again.

## Connected in the current game

- Five distinct playable cats: tabby, black, ivory, calico, tuxedo. Each uses running, jumping, sliding, wall contact, recovery, finish, hit and game-over poses. The physics collider stays on `PlayerController`, and visuals stay under `CatRig`.
- Package art swaps between Normal, Fragile, Heavy and Urgent classes. A carried bindle bobs with the cat and drops on collision.
- Hit and game-over reactions show three rotating white songbirds. The camera enlarges the cat at run start and at impact, then smoothly widens for the result state.
- The Game scene uses painted old-town, nature and modern-town blocks, transition art, a shared sky, moving cloud, weather overlays, animated ambient props, coin, and obstacle visuals. Roads are painted on the fallback track while the original physics colliders remain active.
- HUD, cat selector, result, upgrade and premium screens read their painted icons from the generated art catalog. Prices and other gameplay text remain live Unity UI.
- The importer slices named sheet grids in top-left frame order and applies transparent Sprite settings. Raw source PNGs stay intact.

## Current project boundary

The uploaded `ChunkCatalog.asset` has no chunk prefabs. The fallback track now streams varied road hazards, coin trails, special coins and a shield booster while district art scrolls in world space. Its pit uses a painted opening and a trigger over continuous collision ground; authored gap geometry still needs work. Harbour and Suburbs district art is incomplete in the source pack. The scene and PlayMode tests were rendered and run in the macOS Unity editor; touch controls and frame pacing still need device review.
