# Keyboard Layout Indicator

[Русская версия](README.rus.md)

A portable (no-install) Windows 10 app that:

- tracks the keyboard layout of the active window;
- when the layout changes, shows a semi-transparent colored **border**
  of a configurable thickness/color or a full-screen **color overlay**
  (or shows nothing) — the mode is set in `settings.yaml` separately for
  each layout;
- on each key press, plays a short click sound if enabled for the current
  layout (also configured in `settings.yaml`);
- disables the border/overlay/sound while fullscreen apps (games) are running;
- lives in the system tray with "Open settings file" (in Notepad) and "Exit"
  menu items.

You can edit `settings.yaml` while the app is running — changes are picked
up automatically after you save the file; no restart is required.

## Build (portable app)

Requires **.NET 8 SDK** installed on Windows (must be built on Windows itself,
since the project uses Win32 API via P/Invoke).

From the project folder, run:

```
dotnet publish -c Release -r win-x64 -o publish
```

The `publish` folder will contain `KeyboardLayoutIndicator.exe` (~**2–4 MB**)
— copy it anywhere (USB drive, any folder) and run without installing .NET.
`settings.yaml` is created next to the exe on first launch.


The project can also be opened and built directly in Visual Studio 2022
(File → Open → Project/Solution → select the .csproj).

## settings.yaml format

```yaml
en-US:
  mode: none          # none | border | overlay — nothing / border / full-screen tint
  color: "0,120,215"  # color as R,G,B
  thickness: 12        # border thickness in pixels (for mode: border)
  sound: false          # click sound on key presses in this layout
  soundFile: ""         # optional: path to your own .wav instead of the built-in click

ru-RU:
  mode: border
  color: "255,120,0"
  thickness: 12
  sound: true
  soundFile: ""

options:
  pollIntervalMs: 120       # how often to poll layout, ms
  disableInFullscreen: true # disable indicator/sound in fullscreen apps
  borderOpacity: 0.55        # border opacity, 0.0–1.0
  overlayOpacity: 0.12       # full-screen overlay opacity, 0.0–1.0

capsLock:
  enabled: false      # Caps Lock indicator, drawn on top of the layout indicator
  mode: border        # none | border | overlay
  color: "255,255,255"
  thickness: 6         # for mode: border; if the layout is also border, drawn inside it
```

Sound plays only for keys whose output depends on the layout (letters, the
top number row, and adjacent punctuation); other keys do not trigger sound.

The layout name is the Windows input language name (en-US, ru-RU, uk-UA,
de-DE, fr-FR, etc.).

## Known limitations

- If a game/app is running **as administrator** but the indicator is not,
  the global keyboard hook will not see key presses in that app (Windows UIPI
  protection). In that case, run the indicator as administrator too.
- Fullscreen detection is heuristic (comparing the active window size to the
  monitor size). In rare cases, borderless fullscreen windows may not be
  recognized as a game immediately.
- Layout is determined from the active window's thread layout; for apps with
  their own IME, this may differ from the system language indicator in the
  Windows tray.
- Sound is played directly via `winmm.dll` (`PlaySound`, no external
  dependencies); with very fast typing, short clicks may slightly overlap —
  this is a characteristic of the chosen simple playback approach, not a bug.
