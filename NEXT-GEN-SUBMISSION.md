# Purrcel — Shipaton Next Gen Android demo

This is the student-only Next Gen route. It does not require an app-store release, but the submitted game must work, demonstrate a purchase through the RevenueCat SDK, and include a public open-source repository and a device video. A Unity Editor purchase using `FakePurchasesBackend` is only a local simulation and is not the RevenueCat demonstration.

## 1. Create the RevenueCat Test Store purchase

1. Create a RevenueCat project at [RevenueCat](https://app.revenuecat.com/). Its Test Store is created with the project; if it is absent, create one under **Apps and providers → Test configuration**.
2. Under **Product catalog → Entitlements**, create the identifier **`premium`**.
3. Under **Product catalog → Products**, create a Test Store monthly subscription (for example, product ID `catcourier_premium_monthly`). Attach it to `premium`. Choose the price and trial deliberately; the game displays the price and trial metadata supplied by RevenueCat.
4. Under **Product catalog → Offerings**, create an offering with identifier **`default`**. Add a monthly package with RevenueCat identifier **`$rc_monthly`**, and attach the Test Store product. Make this the default offering.
5. Copy the **Test Store public API key** from **Project settings → API keys**. Do not use a secret API key or put the key into source files.

The only purchase required for this demo is the monthly package above. `iap_breeds` and `iap_districts` are optional and their unfinished content stays hidden. The game requires the `premium` entitlement to unlock premium behavior after a successful test purchase.

## 2. Configure and build the Unity project

1. Open this Unity 2022.3.62f3 project. Install **Android Build Support**, including the SDK/NDK and OpenJDK modules.
2. Run **Cat Courier → Setup → Apply All**.
3. Run **Cat Courier → Setup → Use Real RevenueCat for Next Gen Demo**. It selects the local, ignored `RevenueCatConfig.asset` and sets sandbox mode with the **real** purchases backend.
4. In that asset's Inspector, paste the key into **Development Test Store Public Key**. Leave the production key fields empty for this demo.
5. Run **Cat Courier → Build → Android Next Gen Demo APK**. It refuses to build if the backend is fake or the key is missing. The development APK is written to `Builds/Android/CatCourier.apk`.
6. Install that APK on an Android phone with USB debugging enabled, for example with `adb install -r Builds/Android/CatCourier.apk`. The RevenueCat Unity SDK is unsupported in the Unity Editor, so test the purchase on the phone.

Test Store keys only work in debuggable development builds. Never upload a build using one to Google Play; RevenueCat deliberately rejects such keys in release builds.

## 3. Verify the actual SDK purchase

On the phone, open Purrcel, tap **Go Premium**, and confirm the live monthly price appears with no **DEVELOPMENT FAKE DATA** banner. Tap **Buy** and choose the Test Store success action. Confirm the paywall reports success, the hub says premium is active, and the `premium` entitlement appears for the customer in the RevenueCat dashboard as a sandbox transaction. Restart the app and use **Restore Purchases** to check that access returns. Record this sequence for the demo.

If the paywall says “Store unavailable,” check the key, `default` offering, `$rc_monthly` package, product attachment, phone internet connection, and Unity Console/Android logcat. If the purchase succeeds but premium stays locked, check the product's `premium` entitlement attachment.

## 4. Next Gen submission materials

- Confirm you are an active student with a qualifying academic email on Devpost. Submit to **Next Gen Award** before the event deadline shown on Devpost.
- Make the GitHub repository public. Keep the root `LICENSE`, Unity project source, assets, and these build instructions visible. Verify the license is shown at the top of the GitHub repository page.
- Film the game **running on the Android phone**, including running, collecting coins, the live paywall, and the RevenueCat Test Store purchase. Keep the public YouTube/Vimeo video under two minutes; use only music and art you are authorized to use.
- Provide an English text description, a **1024 × 1024** app icon, and at least one **1179 × 2556** screenshot without a device frame. Capture the submitted screenshot from the working app; do not present a mockup as gameplay.
- The ready-to-upload icon is [`Submission/cat-courier-icon-1024.png`](Submission/cat-courier-icon-1024.png). The device screenshot still has to be captured from the Android build.
- Explain the gameplay, hand-drawn art, and why optional premium purchases suit the 2D runner. Do not claim real ads: the repository has an ad interface and development fake only.

Official references: [Shipaton rules](https://revenuecat-shipaton-2026.devpost.com/rules), [RevenueCat Test Store](https://www.revenuecat.com/docs/test-and-launch/sandbox/test-store), [RevenueCat entitlements](https://www.revenuecat.com/docs/getting-started/entitlements), [RevenueCat offerings](https://www.revenuecat.com/docs/offerings/overview).
