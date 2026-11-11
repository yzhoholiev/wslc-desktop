# Changelog

What changed for people who use WSLC Desktop, newest first. The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and versions follow [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Changed

- **Published ports show the hand cursor**, like other links.

## [1.1.0] - 2026-11-11

### Added

- **User | Administrator switch** in the title bar. `wslc` gives elevated and normal processes separate runtimes, each with its own containers; the switch reopens the app on the other side and shows whether that side is running. The elevated app marks its tray icon and taskbar button with a shield in its runtime's state color.
- **Right-click menus** on container, image, volume and network rows, also from the keyboard with Shift+F10: the row's actions, plus copying its name, ID, image reference or mount point.
- **Keyboard shortcuts**: Ctrl+1 to Ctrl+7 switch pages, Ctrl+F searches, F5 refreshes, Delete removes the selected row after confirming, Esc closes the details pane.
- **Needs attention** on the dashboard: containers that exited with an error or keep restarting, and space held by unused images, each with its action. An item can be dismissed and returns when it happens again.
- **Reachable now** on the dashboard: the ports that running containers publish, as links.
- **Copy** on the container's Inspect tab.

### Changed

- **The dashboard is rebuilt.** CPU and memory share one graph, each on its own scale, with the top consumers beneath it; storage shows what the images take and how much can be reclaimed; a stopped runtime and a runtime without any image each get one panel with the next step.
- **Rows keep one button** for their main action, such as Start or Run, and a "⋯" button with the rest, which leaves more room for the columns.
- **A row shows a spinner** while its container starts, stops, restarts or is removed, and ignores a second request meanwhile.
- **Logs and Inspect reload** when the selected container starts or stops.
- **Row actions stay visible** on the selected row, so they no longer depend on hovering.
- **Pages are denser**: smaller margins and shorter rows. Search boxes have the same width and wording on every list.
- **Clean up** opened from the unused-images warning starts with only unused images selected.
- **A missing `wslc`** shows as an error in the tray, as it already did in the window.
- The app runs on .NET 11 and Windows App SDK 2.6.5.

### Fixed

- **Settings are no longer lost** when `settings.json` is briefly locked at startup, or when the normal and elevated instances both save.
- **The app always closes on exit**, even when stopping the runtime fails.
- **The elevated window ignores launch commands from non-elevated processes**, which could start its runtime without asking.
- **The launcher recovers** from a failed unpack or a removed executable without leaving partial copies behind.
- **Text typed into the pull box during a pull** is kept when the pull finishes.
- **Image totals** on the dashboard are summed from exact sizes, not from rounded ones.
- **Stop all containers** on the dashboard is disabled while nothing is running.
- A dialog requested just before leaving a page no longer crashes the app.
- The window title keeps its "(Administrator)" suffix.

## [1.0.0] - 2026-10-25

The first release: dashboard, containers, images, volumes, networks, the WSL containers and WSL pages, the tray icon and the jump list.

[Unreleased]: https://github.com/yzhoholiev/wslc-desktop/compare/v1.1.0...HEAD
[1.1.0]: https://github.com/yzhoholiev/wslc-desktop/compare/v1.0.0...v1.1.0
[1.0.0]: https://github.com/yzhoholiev/wslc-desktop/releases/tag/v1.0.0
