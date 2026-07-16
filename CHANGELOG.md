# Changelog

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
