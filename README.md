# SmartMacroAutomation

SmartMacroAutomation is a desktop **Macro Recording and Playback** application designed to automate repetitive mouse and keyboard tasks.

Unlike traditional macro recorders that blindly replay recorded actions at fixed coordinates, SmartMacroAutomation adds a **visual verification layer to mouse-click actions**. Before performing each recorded click during playback, the application checks whether the current screen still matches the visual state that was present when the macro was recorded.

The goal is to provide the simplicity of traditional macro automation while making playback safer and more reliable.

---

## Implementation

* **Language / stack:** C# on .NET 8, using Windows Forms for the UI.
* **Platform:** Windows only. It uses native Win32 APIs (`SetWindowsHookEx`, `SendInput`, `CopyFromScreen`) for global mouse/keyboard capture, input simulation, and screenshots, plus `EnumWindows`, `PrintWindow` and `PostMessage` for the window actions (find a window, capture it even when covered, click it in the background).
* **No database.** Every macro is a plain folder on disk:

  ```text
  Macros/
    MyMacro/
      actions.json     <- ordered list of recorded actions + settings
      last-run.log     <- log of the last "--play" command-line run (if any)
      images/
        click_0000.png <- reference screenshot for the first mouse click
        click_0003.png
        window_0001.png <- reference image for a WindowClick action
        ...
  ```

  The `Macros` folder sits next to `SmartMacroAutomation.exe`. In `actions.json`, enum values are written as names (`"Type": "WindowClick"`, `"Anchor": "TopRight"`); older files that use numbers still load.

* **Image comparison:** simple per-pixel RGB difference with a small color tolerance. No external image library is required.

### Per-macro playback settings

Each macro stores four settings in its `actions.json` (editable from the main window, or by hand via **Edit in Notepad**):

* **Similarity threshold** - default 99%, as described above.
* **Repeat count** - how many times Play runs the whole macro in a row. 1 to 999, default 1.
* **Speed** - a multiplier applied to every recorded delay, one decimal place, range 0.1 to 10.0:
  * `1.0` = exactly as recorded (default).
  * `1.5` = 1.5x faster (a recorded 3s wait becomes 2s).
  * `10.0` = all delays removed, macro runs as fast as possible.

* **Mouse move speed** - how fast the cursor travels to a recorded click position, in pixels per second (average over the move). Range 0 to 20000, default 1600. `0` means jump instantly to the coordinate (the old behaviour).

During recording, the time between every action is captured exactly as it happened - including how long the mouse hovered somewhere before a click, and how long a key was held down or how long the pause was between keystrokes. Speed scales all of that; it does not change *what* was recorded, only *how fast* it is replayed.

### Remove all delays (playback-only)

A **Remove all delays** checkbox sits above the **Play Macro** button. When checked, any recorded delay longer than 500ms is capped at 500ms for that playback run only - short delays (like the ~50ms between a key-down and key-up) are left untouched. This is applied in memory while playing; the macro's saved `actions.json` is never modified. Uncheck it and delays go back to their recorded values.

### Stopping playback with Esc

Pressing **Esc** at any point during playback stops the macro immediately - even if the currently focused window belongs to a different application - and shows a dialog asking whether to **Continue** or **Stop**. Choosing Continue resumes the macro exactly where it left off; choosing Stop ends playback for good, the same as when a visual verification mismatch is not confirmed.

Before verifying and clicking, the mouse is moved to the recorded position first and then the (speed-scaled) recorded wait is applied - reproducing any hover state the target UI element had at recording time (e.g. a button that only lights up while the mouse is over it) before the screenshot comparison happens.

### Human-like mouse movement

The cursor never teleports to a recorded coordinate. It travels there in a **straight line over time**, with a slow-fast-slow (ease-in-out) speed profile, updated about 100 times per second. So if the cursor sits at `(1000, 900)` and the next recorded click is at `(200, 400)`, it glides along the direct line between the two points instead of appearing at the target in one frame.

Only the click *position* is recorded - the recorder does not capture the path the hand actually took. The movement during playback is generated from the recorded start and end points.

Details:

* Path: a straight line from the current cursor position to the target.
* Duration: `distance / mouse move speed`, clamped to 40 ms - 5000 ms.
* Speed curve: `0.5 - 0.5 * cos(pi * t)` - starts at zero speed, peaks in the middle, ends at zero speed.
* Moves shorter than 3 pixels are placed directly (nothing to animate).
* The **Speed** multiplier also scales the mouse move speed, so a 2.0x macro moves the cursor twice as fast. At `Speed = 10.0` ("as fast as possible") the cursor jumps instantly.
* The final position is always set exactly to the recorded coordinate, so rounding during the glide cannot shift the click by a pixel.

The implementation lives in `Native/HumanMouse.cs`, separate from the playback engine, so a more advanced path model (curves, small jitter, overshoot) can replace it later without touching `MacroPlayer`.

### Window actions: find a window and click inside it

Two action types work with a specific window instead of fixed screen coordinates. They are not recorded; you add them with the **Window Tool** (see below) or by editing `actions.json`.

**`FindWindow`** finds a visible top-level window and makes it the target of the next `WindowClick` actions. It writes the window's position, size and monitor to the playback log:

```text
Found window 'NVIDIA Broadcast' (NVIDIA Broadcast): x=530 y=25 w=540px h=802px, monitor 2 (primary), position on that monitor x=530 y=25
```

* `x`/`y` are in virtual-screen pixels (all monitors form one coordinate space), so they are enough to locate the window on any monitor.
* The monitor number is the `n` of Windows' `\\.\DISPLAYn` device name. If the window overlaps more than one monitor, the log says `spans monitors 1, 2` instead.
* The size is the visible frame. The invisible resize border that Windows 10/11 adds is not counted.
* A minimized window is found too, and the log says `is minimized`. A hidden window (for example an app that sits in the system tray) is not found.

| Field | Meaning |
| --- | --- |
| `WindowTitle` | Text the title must contain (case-insensitive). An exact title match wins over a partial one; otherwise the topmost matching window wins. |
| `ProcessName` | Optional. Process name without `.exe`, e.g. `NVIDIA Broadcast`. |
| `TimeoutMs` | How long to keep looking before giving up. Default 3000. Use a large value (e.g. 300000 = 5 minutes) to wait for an app that is still starting. |
| `IfNotFound` | What to do if no window is found in time: `Ask` (default) pauses and asks - **Continue** skips the window clicks that need it, **Stop** ends playback. `Continue` goes on without asking. `Stop` ends playback quietly, without a dialog (good for unattended runs at Windows startup). |

**`WindowClick`** clicks a point inside the window found by the last `FindWindow`. The window position is read again just before the click, so it still works if the window moved.

| Field | Meaning |
| --- | --- |
| `Anchor` | The window corner that `X`/`Y` are measured from: `TopLeft` (default), `TopRight`, `BottomLeft`, `BottomRight`. |
| `X`, `Y` | Distance inward from that corner, in pixels. `TopRight`, `X=62`, `Y=15` = 62px left of the right edge and 15px below the top edge. A title-bar button stays hit even if the window is resized. |
| `ClickMode` | `Background` (default) or `Foreground`, see below. |
| `Button` | `Left` (default), `Right`, `Middle`. |
| `ImageFile`, `RefOffsetX/Y`, `RefWidth/Height` | Optional reference image, relative to the click point, like a normal click. |
| `WaitForMatchMs` | If the reference image does not match yet, keep checking (every 250ms) for up to this many ms before showing the mismatch dialog. Useful while an app is still drawing its window. Default 0 (check once). |
| `RepeatUntilHiddenMs` | For close / minimize / "to tray" buttons: after the click, check that the window really went away (hidden, minimized or closed). If it is still visible after 2 seconds, click again, for up to this many ms in total. An app that is still starting can drop the first click. Default 0 (click once). |
| `DelayMs` | Wait before the action, like any other action. |

Click modes:

* **Background** - sends mouse messages (`WM_MOUSEMOVE`, then button down/up) straight to the window with `PostMessage`. The real cursor does not move, focus does not change, and **it works even when another window covers the target**. A few hover moves are sent first, because Chromium/Electron apps ignore a press without hover. If the window is minimized or hidden, the click is skipped (and logged), because there is nothing on screen to click.
* **Foreground** - restores the window if needed, brings it to the front, glides the real cursor there (Mouse move speed setting) and clicks with `SendInput`. Use it for apps that ignore posted messages.

Visual verification for window clicks uses `PrintWindow`: it compares the reference image with what the *window itself* draws, not with the screen. So a window lying on top does not cause a false mismatch. The macro's similarity threshold (or the action's own `Threshold`) applies, and a mismatch shows the same Continue / Stop dialog.

Tip: some apps draw a button in another color while the mouse is over it. Chromium/Electron apps also stop repainting while they are fully covered, so the captured frame can still show an old hover state. For such buttons, crop the reference image down to just the icon (with **Review Screenshots**), so it matches in both states. The example macro below does this.

Both window actions run per-monitor DPI aware, so the numbers are real pixels even on scaled monitors.

### Window Tool

The **Window Tool (find / click)** button opens a helper dialog:

1. Type part of the window title (and optionally the process name), click **Find**. It shows the position, size, monitor, class name and handle, plus a picture of the window (taken with `PrintWindow`, so it works when the window is covered).
2. Click on the picture to pick a point. The nearest corner is chosen as anchor; the label shows the point in window coordinates, its offset from the anchor and its screen position.
3. Pick **Mode** and **Button**, then **Test Click** to try it on the real window.
4. **Add to Selected Macro** appends a `FindWindow` (skipped if the macro already looks for this window last) and a `WindowClick` with a 24x24 reference image to the macro selected in the main window.

### Example macro: close NVIDIA Broadcast to the tray

NVIDIA Broadcast is an Electron app with a custom title bar, so the normal Windows commands do not work on it. Its own close button (X) does not quit the app: it hides the window to the system tray, and the app keeps running. `examples/Macros/Close NVIDIA Broadcast to tray/` clicks that button in the background:

```json
"Actions": [
  { "Type": "FindWindow", "WindowTitle": "NVIDIA Broadcast", "ProcessName": "NVIDIA Broadcast",
    "TimeoutMs": 300000, "IfNotFound": "Stop" },
  { "Type": "WindowClick", "X": 21, "Y": 15, "Anchor": "TopRight", "ClickMode": "Background", "Button": "Left",
    "ImageFile": "window_0001.png", "RefOffsetX": -1, "RefOffsetY": 0, "RefWidth": 2, "RefHeight": 2,
    "WaitForMatchMs": 60000, "RepeatUntilHiddenMs": 30000, "DelayMs": 1000 }
]
```

* It waits up to 5 minutes for the Broadcast window to appear. If it never appears (for example, Broadcast already started in the tray), the macro ends quietly.
* The click point is 21px from the right edge and 15px from the top: the middle of the X.
* The reference image is only the 2x2 px bright center of the X. These pixels do not mix with the button background, so they match even if the button is drawn in a hover color.
* It waits up to 60 seconds for the X to be drawn, and clicks again (for up to 30 seconds) if the window does not go away.

Tested at 100% display scaling, with the window covered by Chrome: found, verified at 100%, window hidden, all Broadcast processes still running.

To use it, copy the folder into the `Macros` folder next to `SmartMacroAutomation.exe`. If your title bar layout is different (for example with display scaling other than 100%), recreate the click with the Window Tool.

### Playing a macro from the command line

```powershell
SmartMacroAutomation.exe --play "Close NVIDIA Broadcast to tray" [--remove-delays]
```

This plays one macro without opening the main window, for example from a desktop shortcut or Task Scheduler. Esc and the Continue / Stop dialogs work as usual. The log is saved to `Macros\<name>\last-run.log`. Exit code: `0` = finished, `1` = stopped or error, `2` = macro not found.

### Running a macro at Windows startup

Select a macro and check **Run this macro at Windows startup**, or use the command line:

```powershell
SmartMacroAutomation.exe --startup-add "Close NVIDIA Broadcast to tray"
SmartMacroAutomation.exe --startup-remove "Close NVIDIA Broadcast to tray"
```

This adds (or removes) a value named `SmartMacroAutomation: <macro name>` under `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`. No admin rights are needed. At logon, Windows runs `"<path to this exe>" --play "<macro name>"`. The path is the exe that made the entry, so the macro must be in the `Macros` folder next to that exe. If you move the exe, register again. Deleting a macro also removes its startup entry.

To run a macro *after another app has started* (like NVIDIA Broadcast), begin it with a `FindWindow` that has a long `TimeoutMs`, as in the example above: the macro starts at logon and waits until the app's window appears.

### Building from source

Requires the [.NET 8 SDK](https://dotnet.microsoft.com/download) on Windows.

```powershell
dotnet build SmartMacroAutomation.sln -c Release
```

### Compiling a standalone .exe

```powershell
dotnet publish src/SmartMacroAutomation/SmartMacroAutomation.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish
```

This produces `publish/SmartMacroAutomation.exe`, a single file that runs on Windows without requiring .NET to be installed.

### Using the app

1. Click **New Macro**, give it a name.
2. Select it in the list and click **Start Recording**. Perform your mouse clicks and keyboard actions in any application.
3. Click **Stop Recording** (in the SmartMacroAutomation window) to save the macro.
4. Optionally click **Review Screenshots** to inspect or crop the reference image captured for each click.
5. Adjust **Similarity threshold**, **Repeat count**, **Speed**, and **Mouse move speed** for the selected macro as needed - changes save immediately.
6. Optionally check **Remove all delays** to cap every recorded delay over 500ms down to 500ms for the next playback run only (short delays such as key-down/key-up are left alone). This is not saved with the macro.
7. Optionally use **Window Tool** to add window actions (find a window by title, click a point inside it in the background). See [Window actions](#window-actions-find-a-window-and-click-inside-it).
8. Select the macro and click **Play Macro** to replay it. If a click's on-screen area no longer matches its reference image (below the similarity threshold), playback pauses and asks whether to continue. Press **Esc** at any time to stop playback immediately and choose whether to continue or stop for good. The **Playback log** under the macro list shows progress, such as the position and size of windows found.
9. Use **Edit in Notepad** to open a macro's `actions.json` directly for manual inspection or editing.

Note: because start/stop/play are controlled from the app window (no global hotkeys), clicks on the SmartMacroAutomation window itself are automatically ignored while recording.

---

## Core Concept

SmartMacroAutomation records two main types of user actions:

* **Mouse actions**, especially clicks
* **Keyboard actions**, including typing and key presses

Mouse clicks receive additional visual information during recording. Keyboard actions are recorded normally and do not require visual verification during playback.

The fundamental playback logic is:

**Record → Replay → Visually verify clicks → Execute**

Instead of blindly following coordinates, SmartMacroAutomation verifies the screen before executing each mouse click.

---

## Macro Recording

When the user starts recording, SmartMacroAutomation monitors the user's mouse and keyboard actions.

### Mouse Recording

When the user performs a mouse click:

1. The click position is recorded.
2. A screenshot is captured at the exact time of the click.
3. The screenshot is initially captured as a **50×50 pixel area centered around the mouse cursor**.
4. The screenshot is associated with that specific click action.

For example, if the user clicks at `(X, Y)`, the application records:

* Mouse position: `(X, Y)`
* Click type: Left / Right / Middle
* Screenshot: 50×50 pixels centered on `(X, Y)`
* Timing information

The screenshot acts as the visual reference for that click during playback.

### Keyboard Recording

Keyboard actions are also recorded as part of the macro.

For example, the user may perform the following sequence:

1. Click a username textbox.
2. Type `john@example.com`.
3. Press `Tab`.
4. Type a password.
5. Press `Enter`.

The macro should preserve the keyboard actions and their order so they can be replayed later.

Keyboard input does **not** require screenshot verification.

---

## Screenshot Review

After recording is finished, the user can review the screenshots associated with the recorded mouse clicks.

Each screenshot can be displayed alongside its corresponding macro action.

The user can optionally crop a screenshot to remove unnecessary areas.

For example, a 50×50 screenshot might contain a button surrounded by irrelevant UI elements. The user could crop it to a smaller region containing only the important part of the interface.

The cropped image becomes the final visual reference used during playback.

This allows the user to control how much visual context should be used when verifying each click.

---

# Macro Playback

When the user starts playback, SmartMacroAutomation replays the recorded actions in their original order.

However, mouse clicks and keyboard actions are handled differently.

## Mouse Click Playback

Before every recorded mouse click, SmartMacroAutomation performs a visual verification step.

The process is:

1. Move the cursor to the recorded click position, gliding there in a straight line at a human-like speed.
2. Capture a new screenshot of the corresponding area.
3. Compare the new screenshot with the reference screenshot recorded earlier.
4. Calculate the visual similarity.
5. If the similarity is **99% or higher**, execute the recorded click.
6. If the similarity is **below 99%**, pause playback and ask the user whether the macro should continue.

The default similarity threshold is **99%**, but this should ideally be configurable.

### Example

Suppose the user recorded a click on a "Save" button.

During recording:

```text
Click at (850, 620)
Reference screenshot captured
```

During playback:

```text
Capture current screenshot
        ↓
Compare with reference screenshot
        ↓
Similarity = 99.6%
        ↓
Similarity ≥ 99%
        ↓
Perform click
```

If the current screen instead produces:

```text
Similarity = 93.4%
        ↓
Pause playback
        ↓
Ask user for confirmation
```

This prevents the application from blindly clicking a potentially different UI element.

---

# Keyboard Playback

Keyboard actions are replayed normally without visual verification.

For example:

```text
Click username field
        ↓
Verify screenshot
        ↓
Click
        ↓
Type "john@example.com"
        ↓
Press Tab
        ↓
Type password
        ↓
Press Enter
```

Only the **click** is visually verified.

Once the click has been successfully verified and executed, SmartMacroAutomation assumes that the keyboard input should be sent to the currently focused element.

There is no need to capture or compare screenshots while typing.

This keeps keyboard playback fast and avoids unnecessary visual checks for every keystroke.

---

# Verification Failure and User Override

If the current screen does not sufficiently match the recorded reference image, playback must be paused.

The application should clearly explain the situation to the user, for example:

> The current screen does not match the recorded state.
> Similarity: 94.2%
> Required: 99%
>
> Do you want to continue?

The user can choose to continue or stop the macro.

If the user chooses **Continue**, the application executes the pending click and resumes playback.

Importantly, continuing does **not** disable visual verification.

Every subsequent mouse click must still go through the same verification process.

This means the user is overriding only the current verification failure, not turning off the safety mechanism for the entire macro.

---

# Example Workflow

A typical recorded workflow could look like this:

```text
1. Click "Open Application"
   └─ Screenshot captured

2. Click "Username" textbox
   └─ Screenshot captured

3. Type "john@example.com"

4. Press Tab

5. Type password

6. Click "Login"
   └─ Screenshot captured

7. Click "Dashboard"
   └─ Screenshot captured

8. Click "Create New"
   └─ Screenshot captured

9. Type customer information

10. Click "Save"
    └─ Screenshot captured
```

During playback:

```text
Click "Open Application"
    ↓
Visual verification
    ↓
Click

Click "Username"
    ↓
Visual verification
    ↓
Click

Type username
    ↓
No verification

Press Tab
    ↓
No verification

Type password
    ↓
No verification

Click "Login"
    ↓
Visual verification
    ↓
Click

Click "Dashboard"
    ↓
Visual verification
    ↓
Click

...
```

This approach combines the speed of normal macro playback with additional protection against unexpected screen changes.

---

# Action Model

Each recorded action should contain the information required to reproduce it.

For example, a mouse click action may contain:

```text
Action Type: Mouse Click
X: 850
Y: 620
Button: Left
Timestamp: ...
Reference Image: ...
Original Image Size: 50×50
Cropped Image: ...
Verification Threshold: 99%
```

A keyboard action may contain:

```text
Action Type: Keyboard
Key: A
Modifier: None
Timestamp: ...
```

or, where appropriate, a sequence of text input may be represented as a text-entry action for more efficient playback.

---

# Key Features

* Mouse action recording
* Keyboard action recording
* Mouse click playback
* Keyboard playback
* Automatic screenshot capture for mouse clicks
* Default 50×50 pixel screenshot area
* Screenshot review after recording
* Manual screenshot cropping
* Per-click visual reference images
* Configurable image similarity threshold
* Default 99% similarity threshold
* Visual verification before every mouse click
* Playback pause when visual verification fails
* User confirmation to continue after a mismatch
* Verification remains enabled after a user override
* Normal keyboard playback without visual verification
* Preservation of action order and timing
* Macro save and load functionality
* Ability to inspect recorded actions and their reference images
* Configurable repeat count (run a macro multiple times in a row)
* Configurable playback speed, scaling all recorded delays (0.1x-10x)
* Mouse moved to the recorded position before verification, so hover-triggered UI changes are reproduced
* Human-like cursor movement: straight-line glide with ease-in-out speed instead of an instant jump
* Configurable mouse move speed in pixels per second (0 = instant jump)
* Direct macro editing via Notepad
* "Remove all delays" playback option: caps recorded delays over 500ms to 500ms in memory only, without touching short delays or the saved macro
* Esc stops playback immediately from anywhere, with a Continue/Stop confirmation
* `FindWindow` action: find a window by title / process and log its position, size and monitor number
* `WindowClick` action: click relative to a window corner, in the background (`PostMessage`, works when the window is covered) or in the foreground
* Window click verification against the window's own rendering (`PrintWindow`), not the screen
* Window Tool: inspect a window, pick a point on its picture, test-click it, add it to a macro
* `--play` command line to run a macro without the main window
* Run a macro at Windows startup (checkbox or `--startup-add` / `--startup-remove`)
* Window clicks can wait for the expected picture (`WaitForMatchMs`) and repeat until the window is hidden (`RepeatUntilHiddenMs`)

---

# Design Philosophy

SmartMacroAutomation is designed around a simple principle:

> **A macro should not blindly trust the screen to remain unchanged.**

Traditional macro automation essentially says:

**"Click this coordinate."**

SmartMacroAutomation says:

**"Check that this location still looks like the expected screen state, then click it."**

Keyboard actions remain simple and fast because they do not require visual verification.

The resulting system combines:

**Macro Recording + Macro Playback + Visual Verification + Human Override**

This provides a safer form of desktop automation while preserving the simplicity and speed expected from a traditional macro recorder.

---

# Future Extensibility

The visual verification system should be designed independently from the macro recording and playback engine so that more advanced recognition techniques can be introduced later.

Potential future capabilities include:

* Curved / jittered mouse paths and overshoot for even more human-like movement
* Template matching
* Perceptual image comparison
* Feature-based image matching
* OCR-based verification
* Automatic target location detection
* DPI and display scaling compensation
* Window-relative coordinates for recorded clicks (window actions already have them)
* Recording window actions directly instead of adding them with the Window Tool
* Multi-monitor support
* Different verification thresholds per action
* Automatic retry when a target is temporarily unavailable (window clicks already have `RepeatUntilHiddenMs`)
* Timeout-based waiting for a visual state to appear, for recorded clicks (window clicks already have `WaitForMatchMs`)
* Multiple reference images for a single action
* AI-based UI element recognition

The long-term goal is to evolve SmartMacroAutomation from a traditional macro recorder into a **reliable, visually aware desktop automation platform**.
