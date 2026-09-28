# KeySonic

KeySonic is a Windows desktop app that plays keyboard sounds as you type. Choose
a sound pack, adjust the volume, and enable or disable sounds from the app. It
runs in the system tray when its window is closed.

## Requirements

- Windows 10 or 11
- .NET 10 SDK

KeySonic uses Windows keyboard hooks, WPF, and Windows audio APIs, so it does not
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

Closing the window hides KeySonic in the system tray; it does not exit the app.
Use **Exit** in the tray icon's menu to quit. In Settings, you can choose to
start KeySonic with Windows or start minimized.

## Sound packs

The app loads packs from a `packs` folder next to the application. Each pack is
a subfolder containing one or more `.wav` files. A `pack.json` file is optional;
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

All WAV files other than `space.wav`, `enter.wav`, and `backspace.wav` are
randomized default-key variations. The three named files are used for their
respective keys when present. At least one usable default WAV file is required
for a pack to load.

Example `pack.json`:

```json
{
  "name": "My Keyboard",
  "description": "A short description of the sound pack.",
  "author": "Your name",
  "version": "1.0.0"
}
```

To add a pack, create its folder under the app's `packs` directory and restart
KeySonic. Open that directory from **Settings > Sound packs folder > Open**.
The repository also includes sample packs under `packs/`.

## Convert a Mechvibes pack

`KeySonic.PackConverter` converts supported Mechvibes packs into KeySonic's WAV
pack format. Pass the source folder containing `config.json` and a destination
folder:

```powershell
dotnet run --project KeySonic.PackConverter -- "C:\path\to\mechvibes-pack" "C:\path\to\KeySonic\packs\Converted Pack"
```

The converter supports sprite-style packs with a shared audio file and
multi-file packs. It puts most key sounds into the default variation pool;
space, Enter, and Backspace remain dedicated sounds. Check the output and test
it in KeySonic before relying on a converted pack, as the converter may skip
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

Settings are saved to `%AppData%\KeySonic\settings.json`. They include volume,
keyboard-sound enablement, start-minimized preference, and the last active pack.

KeySonic handles key events locally to trigger sounds. It does not record or
transmit the text you type. A standard Windows integrity-level restriction
applies: a non-elevated KeySonic process may not receive input from an
administrator-elevated application.

## Projects

- `KeySonic.UI` - WPF desktop application
- `KeySonic.Core` - keyboard hook, audio engine, settings, and sound packs
- `KeySonic.PackConverter` - Mechvibes pack conversion utility
- `KeySonic.TestHarness` - console harness for the hook and audio engine