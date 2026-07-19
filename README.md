# Sprocket

A lightweight Niagara Workbench launcher for Windows. Scans your machine for Niagara
platform installs and gives you one-click access to Workbench, the Alarm Portal, the
console, platform daemon control, memory tuning, module install, and nav tree
import/export — everything the stock WorkPlace Launcher does, without the extra chrome.

## Install

Download the latest `Sprocket.msi` from [Releases](../../releases/latest) and run it.
Installs per-user — no admin rights needed. Sprocket checks for new releases on launch
and will show a small "Update available" link in the footer if one exists; it never
downloads or installs anything on its own, so upgrading is always your call.

### "Windows protected your PC"

The installer is not code-signed, so Windows SmartScreen will warn you the first time you
run it. To continue: click **More info**, then **Run anyway**.

This is expected and it is not a virus warning — SmartScreen shows it for any installer
whose publisher it doesn't yet recognize, regardless of what the installer does. A
certificate that removes the prompt outright is only issued to registered companies, and
Sprocket is deliberately published without a company behind it so any integrator can ship
it unbranded. If you'd rather verify before running, build it yourself from source with the
steps below — it takes about ten seconds and needs nothing installed but Windows itself.

Registering the platform daemon does prompt for administrator rights, separately and later.
That one is real: it creates a Windows service, which Windows will not let any program do
without consent.

## Build from source

No Visual Studio or .NET SDK required — everything is built with the in-box .NET
Framework compiler.

```powershell
.\build.ps1        # builds build\sprocket.exe
.\build.ps1 -Msi   # also builds build\Sprocket.msi (requires WiX Toolset v6)
```

## Project layout

- `src/` — the app (C# / WinForms, targets .NET Framework 4)
- `assets/` — icon source (`sprocket.ico`, `sprocket_gear.png`)
- `tools/gen_icon.cs` — procedurally generates the icon; rerun it if the mark ever needs a tweak
- `installer/Sprocket.wxs` — WiX v6 installer definition

See `CHANGELOG.md` for revision history.
