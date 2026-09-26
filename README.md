# Jugaad Wala Ghar VR

A VR puzzle game for **Meta Quest 3 / 3S** where nothing in the house works properly, and you fix everything the Indian way: with **jugaad**.

Made solo in Unity for a 48-hour game jam (theme: *JUGAAD*).

## Gameplay
- 7 hands-on, physics-based tasks: tie a loose remote with a rubber band, open a lock with a safety pin, wedge a self-closing door with a book, fish a key out with a magnet, and more.
- Real door latch and handle physics, haptic feedback, finger-poke buttons.
- Speed-based scoring (up to 1000 points per task, hints cost 20%). Finish all 7 to become a **JUGAAD MASTER**.
- Play in **English** or **Hinglish** (Main Menu > Settings).

## Controls
| Input | Action |
|---|---|
| Grip | Grab objects, pull door handles |
| Trigger | Press the button of a remote in your hand |
| Left stick | Move |
| Right stick | Turn |
| Finger | Press switchboard / menu buttons |

## Open the project
1. Install **Unity 6000.0.74f1** with **Android Build Support** (OpenJDK, Android SDK & NDK).
2. Unity Hub > Add > Add project from disk > select this folder. The first import takes a while.
3. Switch platform to **Android** (File > Build Profiles).
4. Scenes: `Assets/Scenes/MainMenu.unity` (start) and `Assets/Scenes/SampleScene.unity` (game).

> Note: `Packages/manifest.json` references a local editor-only package (`com.coplaydev.unity-mcp`) by file path. If Unity reports it missing, delete that line from `manifest.json`. The game does not depend on it.

## Build
File > Build Profiles > Android > Build. Install the APK on Quest via Meta Quest Developer Hub or SideQuest. Step-by-step guide: [Sideloading the APK on Meta Quest (PDF)](docs/Meta-Quest-APK-Sideloading-Guide.pdf).

Tech: Unity 6 (URP), XR Interaction Toolkit 3, OpenXR, IL2CPP ARM64, Vulkan.

## Credits
- **Ankit Kumar**: game design, programming, level design, VR interaction, UI, audio integration
- Sound effects: [Kenney](https://kenney.nl) Interface Sounds & Impact Sounds (CC0)
- Unity XR Interaction Toolkit & Starter Assets (Unity Technologies)
- Remote control 3D model: Sebastian Torres
- Other third-party 3D assets belong to their respective authors and remain under their original licenses.
