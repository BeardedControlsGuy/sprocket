# Changelog

## v3.1.3 — 2026-07-18

**Fixed: the Daemon button did nothing.** Registering a platform daemon runs
`bin\plat.exe installdaemon`, which creates a Windows service and so requires
administrator rights. Sprocket launched it without elevation, and because `plat.exe` is a
console program its window closed the instant it failed — so the click produced no service,
no error, and no visible sign anything had happened. It now elevates properly, and when
something does go wrong you get plat.exe's own message in a dialog instead of silence.
Declining the Windows administrator prompt is treated as "cancelled", not as a failure.

**Fixed: switching the daemon to another install could leave you with no daemon at all.**
Windows registers exactly one Niagara daemon service, pointing at whichever install last
registered it — there is no separate service per platform. The old "Switch daemon here"
stopped the running daemon and then tried to start a service for the target install that
had never existed, so it stopped one daemon without starting another. "Install daemon" and
"Switch daemon" are now a single **Move daemon here** action that re-registers the service
in one step, and it warns you first that the outgoing daemon's station will go down.

**Fixed: on machines with more than one Niagara version, Memory Settings and Nav Tree
import/export could read and write a different version's files.** The per-user home folder
was pinned to `Niagara4.15` regardless of the install, so for example a 4.14 install edited
the 4.15 home. It now follows the install's own version, and understands the AX 3.x layout.

Also: daemon status is now detected by matching the service's own `niagarad.exe` path
rather than a loose text match that could latch onto the wrong service, and status is
checked once per rescan instead of once per detected install.

## v3.1.2 — 2026-07-18

**Fixed the update checker itself** — it could silently never detect a newer release on
some machines. `HttpWebRequest` calls to GitHub's API weren't explicitly opting into TLS
1.2, and .NET Framework's default security protocol isn't consistent across Windows
installs; on a machine where it defaults below TLS 1.2 (which GitHub's API has required
since 2022), the connection failed with no visible error — the update check is designed
to fail silently so a flaky connection never interrupts the app, which meant this had no
symptom other than "no update notice ever appears." Confirmed the failure mode directly
(TLS 1.0/1.1-only fails against GitHub's API; TLS 1.2 succeeds) and fixed by explicitly
enabling `SecurityProtocolType.Tls12` before the request.

If you're on v3.1.1 or earlier, this same bug may be why you never saw an update notice —
you'll need to grab this one from the Releases page directly rather than the in-app link.
From v3.1.2 onward the checker itself is fixed, so future releases should notify normally.

## v3.1.1 — 2026-07-18

**Fixes found on a second machine's Honeywell install, plus a WorkPlace Launcher theme
picker.**

- **Fixed:** the platform dropdown, daemon messages, and tray tooltip could show a wrong,
  paragraph-length name (e.g. "Welcome to Optimizer Supervisor") instead of the actual
  install name. Some OEM `brand.properties` files set `workbench.title` to a splash-screen
  sentence rather than a short label; Sprocket was trusting it as the display name. Now
  always uses the install folder name, matching WorkPlace Launcher's own behavior — this
  also fixes Nav Tree / Memory Settings silently targeting the wrong path on affected
  installs, since the user-home folder falls back to the display name when a brand ID
  isn't set.
- **Fixed:** a platform whose daemon Windows service was never installed showed a
  disabled, unlabeled "Daemon" button with no way forward. It now reads **"Install
  daemon"** and runs the installer directly, so there's always one obvious action
  regardless of state.
- **Added:** a Workbench theme picker (new "Theme" quick action) — scans the platform's
  `\modules` folder for theme jars and sets the chosen one as the locked default in
  `brand.properties`, the same mechanism WorkPlace Launcher used.

## v3.1.0 — 2026-07-18

**Visual modernization + several feature additions, reviewed against a design handoff and
the original WorkPlace Launcher's own source.**

- Reskinned to a flatter, calmer dark palette — no more gradient/shimmer hero button or
  aurora-blob backdrop; flat fills, hairline borders, and a dedicated pending/amber state
  for in-progress daemon operations (separate from the ember brand accent).
- **One-click daemon switchover**: selecting a platform whose daemon isn't running, while
  a different platform's daemon is, now shows a banner and a single "Switch daemon here"
  button that stops the old one and starts the new one in sequence — no more manually
  stopping platform A before starting platform B.
- **Module manager**: new window to diff a source platform's `\modules` folder against
  a target platform and copy over anything missing, with per-jar version parsing.
- **Scan roots surfaced in the platform dropdown**: remembered extra folders (previously
  only visible in the Locations dialog) now show inline with quick add/remove.
- **Nav Tree import/export**: save a platform's `navTree.xml` to a file, or load one in —
  closes the biggest gap versus WorkPlace Launcher's nav-tree sync feature.
- **Launch Workbench (with console)**: right-click the Launch button for Workbench with
  its console window left visible, for troubleshooting — WorkPlace Launcher's
  `program-console` mode, previously missing here.
- **Rounded window corners** on every window, matching native Windows 11 chrome.
- **System tray**: minimizing now hides the taskbar entry and drops into the tray instead
  (closing with the X still exits normally); the tray icon's tooltip mirrors the current
  platform and daemon state.

## v3.0.0 — 2026-07-16

**Sprocket is now a standalone app** — no more third-party branding, so it can be handed
to any Niagara integrator.

- New independent "Forge" visual identity: warm graphite + ember/flame palette, a new
  flat chainring icon (replaces the old textured metallic gear, which was hard to tell
  apart from another unrelated app's icon at taskbar size), no lettering baked into the mark.
- Removed all references to its original branding from the UI text, installer, and file metadata.
- Dropped the Rajdhani/Inter font dependency in favor of plain Segoe UI — no custom
  fonts to install on any machine this runs on.
- **Fixed:** starting/stopping a platform's daemon after switching to a different
  platform in the dropdown could silently look like it did nothing. The daemon can take
  well over a minute to actually reach a running state, and the status check didn't
  recognize the intermediate "pending" state — it read that as unknown and immediately
  reverted the button. The toggle now shows a real "Starting…/Stopping…" state and
  waits for the service to actually settle before reporting the result.
- Version number is now shown in the app and is a single source of truth (previously a
  hand-typed string that could drift from the actual build).
- Added an update checker: on launch, Sprocket checks GitHub for a newer release and
  shows a link in the footer if one exists. It only notifies — it never downloads or
  installs anything without you clicking through and running the installer yourself.

## v2.0.0 "Aurora" — 2026-07-02

- Full UI redesign: dark glass-card look, animated gradient CTA button, custom pill
  dropdown, quick-action tiles with hover glow.
- Added Locations & Language dialog (extra scan folders, Workbench UI language).

## v1.0.0 — 2026-07-02

- Initial release. Replaces the third-party WorkPlace Launcher: scans for Niagara
  platform installs and launches Workbench / Alarm Portal / Console, controls the
  platform daemon service, and installs via a per-user MSI.
