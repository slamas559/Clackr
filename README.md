# Clackr

Clackr is a Windows desktop app that plays keyboard sounds as you type. Choose
a sound pack, adjust the volume, and enable or disable sounds from the app. It
runs in the system tray when its window is closed.

Repository: <https://github.com/slamas559/Clackr>

## Requirements

- Windows 10 or 11
- .NET 10 SDK

Clackr uses Windows keyboard hooks, WPF, and Windows audio APIs, so it does not
run on macOS or Linux.

## Run the app

From the repository folder:

```powershell
dotnet run --project KeySonic.UI
```

To build the full solution:

```powershell
dotnet build KeySonic.sln
```

The app starts with the first available sound pack, or restores the pack that
was active last time. Use the dashboard to preview or change the pack and
control volume and keyboard sounds. The Sound Packs page lists installed packs.
Open **Sound Lab** to see physical key presses highlighted or start the typing
game. The game colors typed characters in the prompt and only processes
keystrokes while Sound Lab and the Clackr window are active.
Use **Mouse clicks** to assign and tune separate left- and right-click sounds.
Save keyboard and mouse settings together as a sound profile from the dashboard.

Closing the window hides Clackr in the system tray; it does not exit the app.
Use **Exit** in the tray icon's menu to quit. In Settings, you can choose to
start Clackr with Windows or start minimized.

## Sound packs

The app loads bundled packs from a `packs` folder next to the application and
user-imported packs from `%AppData%\KeySonic\packs`. Each pack is a subfolder
containing one or more supported audio files. A `pack.json` file is optional;
without one, the folder name is used as the pack name.

```text
packs/
└── My Keyboard/
    ├── pack.json       # optional metadata
    ├── click-1.wav
    ├── click-2.wav
    ├── space.wav       # optional dedicated space sound
    ├── enter.wav       # optional dedicated Enter sound
    └── backspace.wav   # optional dedicated Backspace sound
```

All supported audio files other than `space`, `enter`, and `backspace` are
randomized default-key variations. The three named files are used for their
respective keys when present. At least one usable default sound is required for
a pack to load.

Example `pack.json`:

```json
{
  "name": "My Keyboard",
  "description": "A short description of the sound pack.",
  "author": "Your name",
  "license": "CC BY 4.0",
  "version": "1.0.0"
}
```

Add the license name or URL when known. The pack browser marks packs without a
declared license; verify the original terms before redistributing pack audio.

Open **Sound packs** and choose **Mechvibes folder** to import an unzipped pack
containing `config.json` and its referenced audio. The imported folder keeps the
selected source folder's name. Choose **Audio files** to turn selected WAV,
MP3, OGG, AIFF, or AIF files into a new keyboard pack. Imports are written to
`%AppData%\KeySonic\packs`, so they remain writable after installation. The
repository also includes bundled sample packs under `packs/`.

## Mouse click sounds

Mouse click sounds are configured separately from keyboard sound packs. Choose
one file each for the left and right mouse buttons, preview them, and adjust
their volumes independently. WAV and MP3 files are supported. Physical clicks
animate the matching side of the mouse diagram and play the assigned sound while
mouse sounds are enabled.

Use the **Mouse clicks** page or the **Mouse clicks** tab in the Sound Packs
browser. **Open folder** opens the persistent library at
`%AppData%\KeySonic\mouse-clicks`. Use **Add files** to copy downloads into the
library, or add them to the folder directly and select **Refresh**. The visible
list provides per-file preview and one-click left/right assignment. Selected
sounds, enablement, and per-button volumes are saved in
`%AppData%\KeySonic\settings.json`.

## Sound profiles

Save the current keyboard pack, keyboard volume and enablement, mouse sounds,
and per-button mouse volumes as a named profile. Profiles can be exported and
imported as `.ksprofile.json` files. The file stores settings and library paths,
not audio; the referenced keyboard pack and mouse sounds must also be installed
on the receiving device.

For automatic switching, enter a profile's target process name on the dashboard
(`code`, for example, not `code.exe`) and enable **Switch sound profiles
automatically** in Settings. Clackr checks the foreground application locally
and applies a matching profile. Automatic switching is off by default.

## Convert a Mechvibes pack

`KeySonic.PackConverter` remains available for command-line conversion. In the
app, use **Sound packs > Mechvibes folder** to import directly without choosing
an output folder. For the command line, pass the source folder containing
`config.json` and a destination folder:

```powershell
dotnet run --project KeySonic.PackConverter -- "C:\path\to\mechvibes-pack" "C:\path\to\KeySonic\packs\Converted Pack"
```

The converter supports sprite-style packs with a shared audio file and
multi-file packs. It puts most key sounds into the default variation pool;
space, Enter, and Backspace remain dedicated sounds. Check the output and test
it in Clackr before relying on a converted pack, as the converter may skip
unsupported or missing source sounds.

## Test harness

The optional console harness exercises the keyboard hook and audio engine
separately from the WPF app. It reads WAV files from the root `sounds/` folder
by default, or from a folder supplied as its first argument:

```powershell
dotnet run --project KeySonic.TestHarness
dotnet run --project KeySonic.TestHarness -- "C:\path\to\wav-files"
```

Switch to another application and type to test playback. Press `Ctrl+C` in the
console to stop the harness. Its flat `sounds/` input is separate from the
app's `packs/` format.

## Settings and privacy

Settings are saved to `%AppData%\KeySonic\settings.json`. They include sound
levels, keyboard and mouse enablement, saved profiles, window dimensions,
start-minimized preference, automatic profile switching, and the last active
pack.

Clackr keeps the existing `%AppData%\KeySonic` data folder so upgrades retain
access to settings, imported packs, mouse sounds, and logs. The `.ksprofile.json`
profile format is also retained for compatibility.

Clackr observes keyboard and mouse events locally to trigger sounds; it does
not record or transmit them. The typing game only processes keystrokes while
Sound Lab and the Clackr window are active. Game input stays in memory; it is
not saved to disk or transmitted. A standard Windows integrity-level
restriction applies: a non-elevated Clackr process may not receive keyboard
input from an administrator-elevated application.

## Projects

- `KeySonic.UI` - WPF desktop application
- `KeySonic.Core` - keyboard hook, audio engine, settings, and sound packs
- `KeySonic.PackConverter` - Mechvibes pack conversion utility
- `KeySonic.TestHarness` - console harness for the hook and audio engine