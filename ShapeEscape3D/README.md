# Shape Escape 3D: Everyday Objects

A 3D arrow-removal puzzle for Android (Unity 6, URP). Tap pieces to slide them out along their arrows until the
everyday object is dismantled, then watch it reveal as a full-colour 3D model.

Playable browser version (same puzzle engine, used for quick design reviews): `WebDemo/index.html`.

## Status

| Area | State |
|---|---|
| Puzzle engine, 30 levels, daily challenge | Done, covered by tests |
| Lives, hearts, ad-based hints and continues, saw-tooth difficulty, level map | Done in code |
| Unity project opens and builds | Project files present; **not yet opened or built in Unity** |
| AdMob, Firebase, Remove Ads purchase, Facebook / Play Games friends | Code hooks only; SDKs and accounts not set up |
| Play Store listing, privacy policy, closed test | Not started |

## Open and build (first time)

1. Install **Unity 6 (6000.0 LTS)** with **Android Build Support** (OpenJDK + Android SDK/NDK) from Unity Hub.
2. Unity Hub › Add › select this `ShapeEscape3D` folder. If Hub offers a newer 6000.0.x, accept it.
3. Wait for packages to import (URP, Input System, uGUI, Test Framework).
4. Menu **Shape Escape › 1. Configure Project**: sets URP, Android player settings (AAB, IL2CPP, ARM64,
   target SDK), input handling and app icons. Restart Unity if it asks (input handling change).
5. Menu **Shape Escape › 2. Create Main Scene**.
6. Press **Play** in the editor: the game builds itself in code (camera, lights, UI, levels). Ads are simulated
   in the editor.
7. Test on a phone: **Shape Escape › Build › Android APK (Development)**, then install
   `Builds/Android/*.apk` (`adb install -r <file>`).
8. Release: create an upload keystore (Player Settings › Publishing Settings › Keystore Manager), then
   **Shape Escape › Build › Android App Bundle (Release)** and upload the `.aab` to the Play Console.

Command-line release build (CI):

```bash
export SE_KEYSTORE_PATH=/secure/upload.keystore SE_KEYSTORE_PASS=... SE_KEY_ALIAS=upload SE_KEY_PASS=... SE_VERSION_CODE=2
Unity -batchmode -quit -projectPath . -buildTarget Android \
      -executeMethod ShapeEscape.EditorTools.BuildScript.CiReleaseAab
```

Never commit the keystore or its passwords (`.gitignore` blocks `*.keystore` / `*.jks`).

## Tests

- In Unity: Window › General › Test Runner › EditMode › Run All.
- Without Unity (Mono): `Tools/CoreTests/run.sh -v` runs the engine, economy and difficulty-curve checks over
  all 30 levels and a year of daily puzzles. `Tools/CompileCheck/check.sh` compiles every script against
  Unity reference assemblies.

## Game economy (Candy Crush model)

All numbers live in `Scripts/Core/Economy.cs` (`EconomyRules`) and `Ads/AdSettings.cs`.

| Rule | Value |
|---|---|
| Lives | 5 max, 1 back every 30 min; failing or leaving a campaign level after moving costs 1 |
| Hearts per level | 3 wrong moves from level 4 (levels 1-3 are practice) |
| Out of moves | Watch ad for +2 moves (once per attempt), or try again / give up (−1 life) |
| Out of lives | Ad for +1 life (5/day), ask friends (when connected), 250-coin refill, or wait |
| Hints | 3 free per install; then 1 per rewarded ad, 5/day |
| Interstitials | Only between levels: from level 6, every 3 completed levels, not within 2 min of a rewarded ad |
| Other rewarded ads | x2 coins on level complete |
| Hard levels | 5, 10, 20 (1.5x coins); Super Hard 15, 25, 30 (2x coins) |

## Enabling the real SDKs

| Feature | Steps |
|---|---|
| AdMob | Import the Google Mobile Ads Unity plugin (v9+), set the AdMob **App ID** in Assets › Google Mobile Ads › Settings, add the `SE_ADMOB` scripting define, create `Resources/ShapeEscape/AdSettings` with your ad unit ids. Development builds always use Google test ads. |
| Firebase Analytics + Crashlytics | Import the Firebase Unity SDK with `google-services.json`, add the `SE_FIREBASE` define. |
| Remove Ads purchase | Implement `IPurchaseProvider` with Unity IAP / Play Billing (product id `remove_ads`) and register it in `PurchaseManager.SetProvider`. |
| Friends / send lives | Implement `IFriendsProvider` (Facebook Gaming or Play Games friends + a small backend such as Firestore) and register it in `FriendsManager.SetProvider`. Friend buttons stay hidden until then. |

## Project layout

```
Assets/ShapeEscape/
  Scripts/Core/      Engine-free puzzle rules, generator, validator, objects, levels, economy (unit tested)
  Scripts/Runtime/   Unity systems: GameManager, LevelManager, camera/input, UI screens, audio, save,
                     ads, analytics, lives, collection, daily challenge
  Scripts/Editor/    Project setup, level tools, Android build script
  Tests/EditMode/    NUnit tests
  link.xml           IL2CPP stripping exceptions
Packages/manifest.json, ProjectSettings/ProjectVersion.txt
Tools/               Mono test runner and compile check (no Unity needed)
WebDemo/             Browser version
```
