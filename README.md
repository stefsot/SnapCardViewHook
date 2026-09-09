# SnapCardViewHook

SnapCardViewHook is an unofficial Windows tool for changing how cards and game boards are displayed in MARVEL SNAP. It injects a selector into the running game and overrides the values used when card and board views are initialized.

[Download the latest prebuilt release](https://github.com/stefsot/SnapCardViewHook/releases/latest)

> [!WARNING]
> This is an experimental code-injection tool. MARVEL SNAP updates may break it or cause the game to crash. Use it at your own risk. No support or compatibility guarantee is provided.

The overrides are local and visual only. They do not change your collection, account data, server state, or gameplay rules. This project is not affiliated with or endorsed by Second Dinner, Nuverse, Marvel, or Disney.

## Features

- Replace the card shown by CardView instances.
- Change displayed cost and power values.
- Override art variant, surface effect, reveal effect, border, card back, and faction.
- Force the 3D card presentation or flip the current card-details view.
- Replace card descriptions globally while the option is enabled. TextMeshPro rich-text tags such as `<b>` and `<color>` are supported.
- Override the displayed game board.
- Browse and export the in-game card catalog.
- Capture card images and videos with optional transparent backgrounds.

Most changes are applied when a card or board view is initialized. Settings are not saved between launches, and closing the selector disables its active overrides.

## Download and usage

Requirements:

- The Windows x64 PC version of MARVEL SNAP.
- [.NET Framework 4.8 Runtime](https://dotnet.microsoft.com/en-us/download/dotnet-framework/net48).
- The latest [Microsoft Visual C++ Redistributable v14 for x64](https://learn.microsoft.com/en-us/cpp/windows/latest-supported-vc-redist#latest-supported-redistributable-version).

Two prebuilt downloads are available:

- `snapcardviewhook_release.zip` — without FFmpeg; video recording requires your own `ffmpeg.exe`.
- `snapcardviewhook_release_with_ffmpeg.zip` — includes FFmpeg for video recording.

To install and run it:

1. Close MARVEL SNAP.
2. Download your preferred ZIP from the [latest release](https://github.com/stefsot/SnapCardViewHook/releases/latest). Do not download GitHub's automatically generated source archive unless you intend to build the project yourself.
3. Before extracting it, right-click the downloaded ZIP, select **Properties**, enable **Unblock** if that option is present, and select **Apply**.
4. Extract the entire ZIP into its own folder. Keep `Launcher.exe` and all included DLLs together; do not run the launcher from inside the ZIP.
5. Start MARVEL SNAP.
6. Run `Launcher.exe`. If the game is not running, the launcher will ask you to start it and press a key to retry.
7. Select the values you want and enable their corresponding checkboxes. An unchecked option leaves the game's original value unchanged.

Running the launcher as administrator is normally unnecessary, but it may help if Windows denies access to the game process.

## Card text

The replacement text is sent to the game's TextMeshPro component, so standard TextMeshPro rich-text markup can be used. For example:

```html
<b>Ongoing:</b> This is <color=#ff2c2c>custom text</color>.
```

Unity Smart String expressions are not evaluated in replacement text.

## Troubleshooting

| Problem | What to try |
| --- | --- |
| Windows reports that the injected assembly is blocked | Close MARVEL SNAP, right-click the **original downloaded ZIP**, choose **Properties > Unblock**, and extract it again. Unblocking files after extraction may not be sufficient. |
| `Launcher.exe` or a required DLL is missing | Extract the complete release again and keep every file together. Security software may quarantine injection tools; review its history and use only files downloaded from this repository. Do not disable security software blindly. |
| The launcher cannot find the game | Start the Windows PC version of MARVEL SNAP and wait for its window to appear, then press a key in the launcher to retry. |
| Injection fails with an access error | Retry after running `Launcher.exe` as administrator. |
| The selector does not open, reports missing game methods, or the game crashes after an update | The current MARVEL SNAP update may be incompatible. Check for a newer SnapCardViewHook release. |
| The game crashes after entering a custom variant | Restart the game without the launcher and use a known variant ID. Keep **Ensure variant matches card** enabled unless you know the selected combination is valid. |

Injector diagnostics are written to `%APPDATA%\Snoop\SnoopLog.txt`.

## Building from source

You will need:

- Visual Studio 2022.
- The **.NET desktop development** workload and .NET Framework 4.8 targeting pack.
- The **Desktop development with C++** workload, v143 build tools, C++/CLI support, and a Windows 10 SDK.

Then:

1. Clone this repository and open `SnapCardViewHook.sln` in Visual Studio.
2. Allow Visual Studio to restore the NuGet packages.
3. Select the `Release | x64` solution configuration.
4. Build the solution.
5. Start MARVEL SNAP and run `Launcher\bin\Release\Launcher.exe`. Keep the generated DLLs and configuration file beside the executable.

The solution contains the launcher and managed UI, the native injector, IL2CPP metadata wrappers, and the MinHook-based detour layer.

## License and third-party code

SnapCardViewHook is provided under the [Apache License 2.0](LICENSE.txt). Bundled MinHook code and injector-derived code retain their original license notices in the source files.
