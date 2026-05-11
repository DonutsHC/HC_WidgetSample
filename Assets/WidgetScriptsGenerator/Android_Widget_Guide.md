# Android Widget for Unity 6.3 — Complete Guide

## Overview

This guide explains how to create an Android home-screen widget connected to a Unity mobile game. The widget reads a JSON file written by Unity to determine if the player has opened the game today, and shows a different image accordingly (like Duolingo's widget).

**Data flow:** Unity Game writes JSON to `persistentDataPath` → Android Widget reads that file → displays image based on login status and time of day

---

## File Structure

```
Assets/
├── Scripts/
│   ├── WidgetDataWriter.cs              ← Unity: writes login data + triggers widget refresh
│   ├── WidgetData.cs                    ← Unity: serializable data class
│   ├── WidgetUtility.cs                 ← Unity: shared Android JNI calls (update + pin)
│   └── AddWidgetButton.cs              ← Unity: UI button to pin widget to home screen
└── Plugins/
    └── Android/
        ├── mainTemplate.gradle          ← Gradle: clean Unity default (no widget-specific changes)
        ├── settingsTemplate.gradle      ← Gradle: repository config (enable in Player Settings)
        └── GameWidget.androidlib/       ← Android Library: source + resources + manifest
            ├── AndroidManifest.xml      ← registers the widget receiver
            ├── build.gradle             ← namespace + SDK config + sourceSets
            ├── project.properties       ← tells Unity "this is an Android Library"
            ├── proguard-rules.pro       ← prevents code stripping
            ├── src/main/java/com/[COMPANY_NAME]/[PRODUCT_NAME]/
            │   └── GameWidgetProvider.java  ← Android: widget logic (reads JSON, updates UI)
            └── res/
                ├── drawable/
                │   ├── widget_ok_1..4.png     ← images: logged in (4 time-slot variants)
                │   ├── widget_notok_1..4.png  ← images: not logged in (4 time-slot variants)
                ├── font/
                │   └── lilita_one.ttf         ← custom font for streak text
                ├── layout/
                │   └── widget_layout.xml      ← widget UI layout (2x1 FrameLayout)
                ├── values/
                │   └── strings.xml            ← widget description string
                └── xml/
                    └── widget_info.xml        ← widget metadata (size, refresh rate)
```

---

## Unity Settings Required

Before building, enable these checkboxes in **Player Settings → Android → Publishing Settings**:

1. **Custom Main Gradle Template** — creates `mainTemplate.gradle`
2. **Custom Settings Gradle Template** — creates `settingsTemplate.gradle`

Remember to set your **Application Identifier** (e.g., `com.[COMPANY_NAME].[PRODUCT_NAME]`) in Player Settings → Android → Other Settings with the other settings for your widget.

---

## Key Concepts

### 6am Day Boundary

Both the C# and Java sides use a 6:00 AM day boundary instead of midnight. Playing before 6am counts as the previous day for login and streak purposes. This is implemented by subtracting 6 hours from the current time before taking the date. The constant `DAY_BOUNDARY_HOUR` must match in both `WidgetDataWriter.cs` and `GameWidgetProvider.java`.

### Time-Slot Image Rotation

Instead of a single happy/sad image, the widget uses 4 image variants that rotate based on time of day:
- **Slot 1** (06:00–10:59) — morning
- **Slot 2** (11:00–18:59) — afternoon
- **Slot 3** (19:00–21:59) — evening
- **Slot 4** (22:00–05:59) — night

Drawable naming convention: `widget_ok_1` through `widget_ok_4`, `widget_notok_1` through `widget_notok_4`.

### Severity Escalation

When the player misses logging in:
- **First missed day:** the "not ok" image rotates by time slot (notok_1 through notok_4)
- **2+ consecutive missed days:** locks to `widget_notok_4` (the most severe expression)

### Custom Font Bitmap Rendering

Instead of using a plain `TextView`, the streak count is rendered as a `Bitmap` using Android's `Canvas` + `Paint` API with the Lilita One font, golden color (#FFEA84), and a drop shadow. This bitmap is displayed in an `ImageView` (id: `widget_text_image`), giving full control over typography that `RemoteViews` normally doesn't allow.

---

## File-by-File Explanation

### 1. `WidgetDataWriter.cs` — Unity Login Tracker

**Location:** `Assets/Scripts/WidgetDataWriter.cs`

**Purpose:** Attach this to a GameObject in your first scene. On `Start()`, it writes a JSON file with the current date and streak count to `Application.persistentDataPath`, then calls `WidgetUtility.RequestWidgetUpdate()` to refresh the widget immediately.

**Key responsibilities:**
- Writes `widget_data.json` with `lastLoginDate` (yyyy-MM-dd) and `streak` count
- Uses `GetEffectiveDate()` to apply the 6am day boundary — `DateTime.Now.AddHours(-DAY_BOUNDARY_HOUR).Date`
- Calculates streak: +1 if last login was yesterday (effective), reset to 1 if a day was missed
- Delegates the Android broadcast to `WidgetUtility.RequestWidgetUpdate()`

### 2. `WidgetData.cs` — Data Class

**Location:** `Assets/Scripts/WidgetData.cs`

**Purpose:** A standalone `[Serializable]` public class with two fields: `lastLoginDate` (string) and `streak` (int). Used by `JsonUtility.ToJson()` and `JsonUtility.FromJson()`.

### 3. `WidgetUtility.cs` — Shared Android JNI Calls

**Location:** `Assets/Scripts/WidgetUtility.cs`

**Purpose:** A static utility class that consolidates all Android JNI calls for the widget. This keeps `WidgetDataWriter` and `AddWidgetButton` clean — they just call static methods instead of duplicating JNI boilerplate.

**Methods:**
- `RequestWidgetUpdate()` — sends an `APPWIDGET_UPDATE` broadcast to refresh all widget instances. Wrapped in `#if UNITY_ANDROID && !UNITY_EDITOR`.
- `RequestPinWidget()` — calls `AppWidgetManager.requestPinAppWidget()` to prompt the user to add the widget to their home screen. Includes an API 26 check and a launcher capability check before calling.

### 4. `AddWidgetButton.cs` — Pin Widget Button

**Location:** `Assets/Scripts/AddWidgetButton.cs`

**Purpose:** Attach to a UI Button. On click, calls `WidgetUtility.RequestPinWidget()` to prompt the player to add the widget to their home screen.

**Key details:**
- **iOS guard:** Auto-destroys the GameObject on iOS (`#if UNITY_IOS Destroy(gameObject); return;`) since widgets are Android-only
- Simplified to a thin wrapper around `WidgetUtility.RequestPinWidget()`

### 5. `GameWidgetProvider.java` — Widget Logic (Android Native)

**Location:** `GameWidget.androidlib/src/main/java/com/[COMPANY_NAME]/[PRODUCT_NAME]/GameWidgetProvider.java`

**Purpose:** The core Android widget class. Extends `AppWidgetProvider` and runs natively on Android (outside Unity).

**Key responsibilities:**
- `onUpdate()` — called by Android every 30 minutes (or when triggered by Unity's broadcast). Reads the JSON file and updates the widget.
- `onReceive()` — handles the explicit `APPWIDGET_UPDATE` broadcast sent by Unity.
- `readLoginStatus()` — reads `widget_data.json` from **both** `context.getFilesDir()` (internal) and `context.getExternalFilesDir()` (external), using the 6am day boundary via `getEffectiveDate()`. Also calculates `daysSinceLogin` for severity escalation.
- `getCurrentTimeSlot()` — returns 1–4 based on current hour for image rotation.
- `renderStreakText()` — renders streak count as a `Bitmap` with Lilita One font, golden color, and drop shadow.
- `getResId()` — resolves resource IDs by name using `context.getResources().getIdentifier()`, avoiding R class import issues.
- Makes the widget tappable — clicking it launches the game.

**Why is this file inside the `.androidlib`?**
With `java.srcDirs = ['src/main/java']` in the `.androidlib`'s `build.gradle`, the Java source is compiled as part of the library module. This keeps all widget code (source + resources + manifest) self-contained in one folder.

### 6. `GameWidget.androidlib/` — Android Library

**Why `.androidlib`?**
Unity 6.3 removed support for loose `res/` folders in `Assets/Plugins/Android/`. All Android resources must now be inside an Android Library. Unity recognizes a folder as an Android Library when it ends with `.androidlib` and contains `project.properties`.

### 7. `project.properties`

**Location:** `GameWidget.androidlib/project.properties`

**Purpose:** The minimum file needed for Unity to recognize this folder as an Android Library plugin. Contains just `android.library=true` and the target SDK level.

### 8. `build.gradle`

**Location:** `GameWidget.androidlib/build.gradle`

**Purpose:** Required by Android Gradle Plugin (AGP) 8.x+ which demands a `namespace` for every library module. Without this file, the build fails with "Namespace not specified."

**Key settings:**
- `namespace` — used for R class generation (can differ from your app's package name)
- `compileSdk` and `buildToolsVersion` — must match what Unity has installed (check your Unity version's SDK)
- `sourceSets` — explicitly points AGP to the root-level `AndroidManifest.xml`, `res/` folder, and `src/main/java/` for Java source compilation

### 9. `proguard-rules.pro`

**Location:** `GameWidget.androidlib/proguard-rules.pro`

**Purpose:** Prevents Unity/ProGuard from stripping the `GameWidgetProvider` class during code optimization. Android calls the widget provider via reflection (based on the manifest), so without this rule, the build system may delete the class thinking it's unused.

### 10. `AndroidManifest.xml`

**Location:** `GameWidget.androidlib/AndroidManifest.xml`

**Purpose:** Registers the widget with Android. This manifest is merged into the final APK's manifest during the build.

**Key elements:**
- `<receiver>` — declares `GameWidgetProvider` as a broadcast receiver
- `android:exported="true"` — required so Android's AppWidgetManager can reach it
- `<intent-filter>` with `APPWIDGET_UPDATE` — tells Android this receiver handles widget updates
- `<meta-data>` pointing to `@xml/widget_info` — links to the widget metadata

### 11. `widget_layout.xml`

**Location:** `GameWidget.androidlib/res/layout/widget_layout.xml`

**Purpose:** Defines the widget's visual layout. Android widgets use `RemoteViews`, which only supports a limited set of views (no custom views, no RecyclerView).

**Structure:**
- `FrameLayout` (id: `widget_root`) — the tappable container, no background (character fills the whole widget)
- `ImageView` (id: `widget_image`) — full-bleed character image (`match_parent` both dimensions)
- `ImageView` (id: `widget_text_image`) — streak text rendered as bitmap, anchored to bottom-center

### 12. `widget_info.xml`

**Location:** `GameWidget.androidlib/res/xml/widget_info.xml`

**Purpose:** Metadata that tells Android how to handle the widget.

**Key attributes:**
- `minWidth="110dp"` / `minHeight="40dp"` — minimum size for 2x1 home screen cells
- `targetCellWidth="2"` / `targetCellHeight="1"` — preferred cell count (2 wide, 1 tall)
- `updatePeriodMillis` — 1800000ms = 30 minutes (Android's minimum). The widget also updates instantly when Unity sends a broadcast.
- `initialLayout` — points to `widget_layout.xml` (shown before the first update)
- `resizeMode` — allows horizontal and vertical resizing
- `previewLayout` — what the user sees in the widget picker

### 13. `widget_ok_1..4.png` and `widget_notok_1..4.png`

**Location:** `GameWidget.androidlib/res/drawable/`

**Purpose:** 8 character images total — 4 "ok" variants (logged in today, one per time slot) and 4 "not ok" variants (hasn't logged in, one per time slot). Replace these with your own game character art. The naming convention (`widget_ok_1` through `widget_ok_4`, `widget_notok_1` through `widget_notok_4`) must match the Java code.

### 14. `lilita_one.ttf`

**Location:** `GameWidget.androidlib/res/font/lilita_one.ttf`

**Purpose:** Custom font used by the Java provider to render the streak text as a bitmap. Loaded at runtime via `context.getResources().getFont()`. Falls back to the system bold font if loading fails.

### 15. `strings.xml`

**Location:** `GameWidget.androidlib/res/values/strings.xml`

**Purpose:** Contains the `widget_description` string shown in the widget picker to describe what the widget does.

### 16. `mainTemplate.gradle`

**Location:** `Assets/Plugins/Android/mainTemplate.gradle`

**Purpose:** Unity's custom Gradle template for the main `unityLibrary` module. No widget-specific changes needed — the Java source is compiled inside the `.androidlib` module via its own `build.gradle`.

### 17. `settingsTemplate.gradle`

**Location:** `Assets/Plugins/Android/settingsTemplate.gradle`

**Purpose:** Unity's custom Gradle settings template. We didn't change the default — it was only needed to be enabled (checked in Player Settings) because Unity auto-generates the `include` for `.androidlib` modules.

---

## How It All Connects at Build Time

1. Unity copies `Assets/Plugins/Android/` contents into the Gradle project at `Library/Bee/Android/Prj/.../Gradle/unityLibrary/`
2. `settingsTemplate.gradle` auto-includes `GameWidget.androidlib` as a Gradle submodule
3. The `.androidlib`'s `build.gradle` compiles `GameWidgetProvider.java` from `src/main/java/`
4. The `.androidlib`'s `AndroidManifest.xml` gets merged into the final APK manifest, registering the widget receiver
5. The `.androidlib`'s `res/` folder (drawables, layouts, fonts, XML metadata) gets merged into the APK's resources

---

## Key Gotcha: Path Mismatch

Unity's `Application.persistentDataPath` may resolve to either:
- **Internal:** `/data/data/<package>/files` (matches `context.getFilesDir()`)
- **External:** `/storage/emulated/0/Android/data/<package>/files` (matches `context.getExternalFilesDir(null)`)

The Java provider checks **both** paths to handle either case.

---

## Common Build Errors and Fixes

### "OBSOLETE - Providing Android resources in Assets/Plugins/Android/res was removed"
Unity 6.3 no longer allows loose `res/` folders. Move all resources into a `.androidlib` folder.

### "Namespace not specified"
AGP 8.x requires every library module to declare a `namespace`. Add a `build.gradle` inside your `.androidlib` with `namespace "com.yourcompany.yourapp.widget"` in the `android` block.

### "Failed to install SDK packages — licences have not been accepted" (build-tools version mismatch)
Your `.androidlib`'s `build.gradle` is requesting a different `buildToolsVersion` than what Unity has installed. Set `buildToolsVersion` to match Unity's version exactly (e.g., `"36.0.0"`). Check what Unity has at `<Unity Install>/Editor/Data/PlaybackEngines/AndroidPlayer/SDK/build-tools/`.

### Widget not appearing in widget picker
The `.androidlib`'s `AndroidManifest.xml` (containing the `<receiver>`) wasn't merged into the final APK. This happens when AGP can't find the manifest. Add `sourceSets` to your `.androidlib`'s `build.gradle`:
```gradle
sourceSets {
    main {
        manifest.srcFile 'AndroidManifest.xml'
        java.srcDirs = ['src/main/java']
        res.srcDirs = ['res']
    }
}
```
This explicitly tells AGP to use the root-level files instead of looking under `src/main/` for manifest and res.

### Widget shows but never updates after opening the game (path mismatch)
Unity's `Application.persistentDataPath` may resolve to external storage, while `context.getFilesDir()` in Java points to internal storage. The Java provider must check **both** `context.getFilesDir()` and `context.getExternalFilesDir(null)`.

### Widget class stripped (ClassNotFoundException at runtime)
Add a `proguard-rules.pro` file to your `.androidlib` with:
```
-keep class com.yourcompany.yourapp.GameWidgetProvider { *; }
```
Android calls the widget provider via reflection, so ProGuard may strip it thinking it's unused.

---

## Pinning the Widget from Inside the Game

Android 8.0+ (API 26) supports programmatically requesting to pin a widget to the home screen via `AppWidgetManager.requestPinAppWidget()`. This lets you add an in-game button that prompts the player to add the widget — much more discoverable than asking them to find it in the widget picker manually.

### How it works

1. The player taps a button in your game (e.g., "Add Widget to Home Screen")
2. Android shows a system confirmation dialog
3. If the player accepts, the widget is placed on their home screen automatically

### Implementation

The pin logic lives in `WidgetUtility.RequestPinWidget()` and is called by `AddWidgetButton.cs`. Key details:
- Checks the API level first (`Build.VERSION.SDK_INT >= 26`) to avoid crashes on older devices
- Calls `isRequestPinAppWidgetSupported()` to verify the launcher supports pinning
- Calls `requestPinAppWidget()` with a `ComponentName` pointing to `GameWidgetProvider`
- The third parameter (`null`) means we don't get a callback when the user confirms — pass a `PendingIntent` if you need to know the result
- `AddWidgetButton` auto-destroys on iOS since widgets are Android-only

**Important:** The class name string `"com.[COMPANY_NAME].[PRODUCT_NAME].GameWidgetProvider"` must exactly match the `package` + class name in your Java file and the `android:name` in your `AndroidManifest.xml`.

---

## Debugging Commands (via adb)

```bash
# Check if widget_data.json was written
adb shell run-as com.[COMPANY_NAME].[PRODUCT_NAME] cat files/widget_data.json

# View Unity logs
adb logcat -s Unity | findstr "WidgetDataWriter"

# Force widget update
adb shell am broadcast -a android.appwidget.action.APPWIDGET_UPDATE -n com.[COMPANY_NAME].[PRODUCT_NAME]/.GameWidgetProvider

# Check if widget provider is registered
adb shell dumpsys appwidget | findstr "[COMPANY_NAME]"
```
