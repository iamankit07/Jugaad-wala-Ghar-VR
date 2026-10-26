# How to play Jugaad Wala Ghar on Meta Quest

The game is a standalone Android APK for **Meta Quest 2, 3, 3S and Pro**. To play the APK you install it by **sideloading** it from a PC. It does **not** run on Android phones.

## What you need
- Meta Quest 2, 3, 3S or Pro
- A Windows or Mac PC
- A USB-C cable (a data cable, not a charge-only one)
- The game file: `JugaadWalaGharVR.apk`

## Step 1: Turn on Developer Mode (one time only)
1. Create a free developer account and organization at [developers.meta.com/horizon](https://developers.meta.com/horizon).
2. On your phone, open the **Meta Horizon** app, go to **Devices**, choose your headset, then **Headset settings > Developer Mode**, and turn it **on**.
3. Restart the headset.

## Step 2: Connect the Quest to the PC
1. Install **Meta Quest Developer Hub (MQDH)** on the PC.
2. Connect the Quest to the PC with the USB-C cable.
3. Put on the headset. When it asks **"Allow USB debugging?"**, tick *Always allow from this computer* and press **Allow**.
4. The headset should now show up in MQDH under **Device Manager**.

## Step 3: Install the game
- **MQDH (easiest):** drag and drop `JugaadWalaGharVR.apk` onto the MQDH device window. Wait for the "installed" message.
- **SideQuest:** click *Install APK file from folder on computer* and choose the APK.
- **ADB (command line):** `adb install -r JugaadWalaGharVR.apk`

## Step 4: Start the game
1. In the headset, open the **App Library**.
2. Open the filter dropdown at the top right and choose **Unknown Sources**.
3. Launch **Jugaad Wala Ghar VR** (the app name on the headset).

## Tips before you play
- Play **standing**, with a clear Guardian boundary (2 m x 2 m or more is ideal).
- Language: **Main Menu > Settings > English / Hinglish**.
- Controls:

| Input | Action |
|---|---|
| Grip (side button) | Grab objects, pull door handles |
| Trigger | Press the button of a remote in your hand |
| Left stick | Move (teleport mode: push to aim, release to jump) |
| Right stick | Turn |
| Finger | Touch switchboard and menu buttons to press them |
| B (right) | Take out / put away the phone (hints) |
| A (right) | Get in / out of the car |
| Menu (left) | Pause menu and settings |

## Troubleshooting
| Problem | Fix |
|---|---|
| Game not in the App Library | Choose **Unknown Sources** in the filter dropdown |
| Quest not showing in MQDH | Check that Developer Mode is on, try another cable, and accept "Allow USB debugging" in the headset |
| Install fails (signature or `INSTALL_FAILED_UPDATE_INCOMPATIBLE`) | Uninstall the older version of the game from the headset first, then install again |
| Black screen or freeze on start | Close the app and open it again, or restart the headset |
