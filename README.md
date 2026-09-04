# SmartMacroAutomation

SmartMacroAutomation is a desktop **Macro Recording and Playback** application designed to automate repetitive mouse and keyboard tasks.

Unlike traditional macro recorders that blindly replay recorded actions at fixed coordinates, SmartMacroAutomation adds a **visual verification layer to mouse-click actions**. Before performing each recorded click during playback, the application checks whether the current screen still matches the visual state that was present when the macro was recorded.

The goal is to provide the simplicity of traditional macro automation while making playback safer and more reliable.

---

## Implementation

* **Language / stack:** C# on .NET 8, using Windows Forms for the UI.
* **Platform:** Windows only. It uses native Win32 APIs (`SetWindowsHookEx`, `SendInput`, `CopyFromScreen`) for global mouse/keyboard capture, input simulation, and screenshots.
* **No database.** Every macro is a plain folder on disk:

  ```text
  Macros/
    MyMacro/
      actions.json     <- ordered list of recorded actions + settings
      images/
        click_0000.png <- reference screenshot for the first mouse click
        click_0003.png
        ...
  ```

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
6. Select the macro and click **Play Macro** to replay it. If a click's on-screen area no longer matches its reference image (below the similarity threshold), playback pauses and asks whether to continue.
7. Use **Edit in Notepad** to open a macro's `actions.json` directly for manual inspection or editing.

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
* Window-relative coordinates
* Multi-monitor support
* Different verification thresholds per action
* Automatic retry when a target is temporarily unavailable
* Timeout-based waiting for a visual state to appear
* Multiple reference images for a single action
* AI-based UI element recognition

The long-term goal is to evolve SmartMacroAutomation from a traditional macro recorder into a **reliable, visually aware desktop automation platform**.
