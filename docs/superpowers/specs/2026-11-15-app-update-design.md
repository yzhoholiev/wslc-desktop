# In-app update: design

## Intent

Today a user updates WSLC Desktop by hand: exit from the tray, download the new executable from GitHub, replace the file. The app should find a new release, download it and install it itself, so that staying current takes one click. This must also work for an instance that Windows started at sign-in and that lives in the tray.

## Goals

- The app learns that a newer release exists, without the user asking.
- One action downloads the release, installs it and restarts the app into it.
- Autostart, the taskbar pin and the jump list keep working after an update, with no path change.
- A failed or interrupted update leaves the installed version working.

## Non-goals

- Silent installation: nothing is downloaded or installed until the user asks.
- Delta updates, rollback to an older version, or choosing a specific version.
- Updating WSL or `wslc` (the WSL page already does that).
- Store or MSIX packaging. If the app moves to the Store, this feature is removed there.
- Verifying a code signature. The executable is unsigned today; see Constraints.

## Anti-goals

- The app must never run a downloaded file that failed its checks.
- The app must never start or stop the runtime as a side effect of updating.
- An update must never downgrade the app.
- The check must not send anything that identifies the user or the machine.

## Constraints

- **Distribution.** One self-contained launcher executable per architecture (`WslcDesktop-win-x64.exe`, `WslcDesktop-win-arm64.exe`), which the user puts anywhere. It unpacks its payload to `%LOCALAPPDATA%\WslcDesktop\app\<version>` and starts the app from there, passing its own path in `WSLCDESKTOP_LAUNCHER`. The launcher exits once the app has started, so its file is not locked while the app runs. It already unpacks a new version beside the old one and deletes versions that are no longer running.
- **Releases.** A `v*` tag publishes a GitHub release with both executables and `SHA256SUMS.txt`. A tag with a suffix is marked as a pre-release.
- **Trust.** The executable is unsigned. What protects a download is HTTPS to GitHub and the checksum file from the same release, which guards against corruption but not against a compromised release.
- **Two sides.** A normal and an elevated instance can run at once, each with its own runtime, both started from the same launcher file and sharing `settings.json`.
- **Layering.** Core must not use files, processes or the network itself; a new external dependency is a port in Core, an adapter in Infrastructure and a fake in `tests/Shared/TestServices.cs`.
- **Native AOT.** JSON goes through a source-generated context; anything UI-facing is checked in the published app.
- **This is the app's first network access.** Until now it only talked to `wslc` and `wsl`.

## Approaches

| | A. App replaces the launcher file | B. Updates live in `%LOCALAPPDATA%` | C. Velopack |
| --- | --- | --- | --- |
| How | The app downloads the new launcher, checks it, swaps it for the launcher it was started from and restarts through it | The app downloads the new version into its data folder; the launcher starts the newest version found there | Adopt Velopack's installer and update format |
| Trade-off | Cannot update a launcher in a folder the app cannot write to | The user's executable becomes a stub that no longer says what runs; the launcher gains version-selection logic | Replaces the launcher, `publish.ps1`, the release job and the single-executable model |

**Chosen: A**, by the maintainer. It keeps the single-executable model and needs no launcher change. The read-only case falls back to opening the release page.

## Design

### What the user sees

- **Settings → Updates.** The running version, the state ("Up to date", "Version 1.2.0 is available", "Downloading… 40%", "Restart to finish updating", or the failure), **Check now**, **Update** when a version is available, a **What's new** link to the release page, and a **Check for updates automatically** switch.
- **Dashboard.** An attention item, "WSLC Desktop 1.2.0 is available", with **Update**. Dismissing it hides it until a newer version appears.
- **Tray.** A balloon the first time a version is found, and a menu entry "Update to 1.2.0" while one is available. This is the only surface an autostarted instance may ever show.
- **Installing.** Update shows progress, then the app restarts by itself: into the tray if its window was hidden, otherwise with the window open.

### Behaviour

1. **Check.** Once on start, unless the switch is off. Check now always works. The app asks GitHub for the latest release of `yzhoholiev/wslc-desktop`. Someone running a pre-release build is also offered newer pre-releases; someone on a release only gets releases.
2. **Compare.** Versions compare as SemVer, pre-release part included. Only a strictly newer version is offered.
3. **Download.** The asset for this process architecture, saved beside the launcher under a temporary name, with progress and cancel. Only `https` URLs on GitHub's hosts are followed, and a size limit applies.
4. **Verify.** The file's SHA-256 must match its line in the release's `SHA256SUMS.txt`. A release without that file, or without a line for the asset, is not installable.
5. **Install.** Swap the downloaded file for the launcher, keeping the old one as a backup until the swap succeeds, while holding the launcher's unpack lock so that no launch runs half-way through.
6. **Restart.** Start the launcher with the arguments that reproduce the current window state (`--tray`, or the page arguments of `MainWindow.CommandForPage`), then exit this instance through `ExitApplicationAsync(keepRuntime: true)`, the way the User | Administrator switch does, so the "stop the runtime on exit" setting does not apply. Before it starts the launcher, this instance releases its single-instance mutex, so the new instance never finds it held and quits; that works whatever version is started, including one without the updater. Switching sides never needed this, because each side has its own mutex. The launch also carries one more argument, which skips "start the runtime with the app" for that launch; a version that does not know the argument ignores it.
7. **The other side.** A second instance keeps running the old version. When it next checks, it sees that the launcher file is already newer than itself and offers only **Restart**. The same applies after a manual replacement of the file.

### When it cannot update

| Situation | What the app does |
| --- | --- |
| Started without the launcher (a build output) | Hides the Updates section's actions and never checks |
| Offline, or GitHub unreachable or rate-limited | Automatic checks fail quietly and try again next time; Check now shows the reason |
| The launcher's folder is not writable, or the launcher was moved or deleted | Says so and offers the release page |
| Checksum mismatch, wrong size, download cut off | Deletes the download, says so, leaves the installed version untouched |
| The swap fails half-way | Restores the backup |

### Structure

- **Core.** A use case, `AppUpdater`, holding the update state and the steps above, and raising a change event the view models and the tray listen to. Two ports: one for the release feed (latest release, its checksum file, downloading an asset to a path) and one for the installed launcher (its version, whether it can be replaced, replacing it, restarting through it). Version comparison and checksum-file parsing are pure helpers. `AppServices` gains the updater.
- **Infrastructure.** Adapters for both ports, DTOs for the GitHub release response in a source-generated JSON context, and their registration in `DefaultServices`.
- **WinUI.** The Settings section in `SettingsViewModel` and `SettingsPage`; a new attention kind in `DashboardAttention`; the tray entry and balloon in `MainWindow.Tray.cs`; the restart beside the existing switch-side restart.
- **Settings.** `AppSettings` gains the automatic-check switch (on by default), the time of the last check, the version that check found, and the version the tray balloon last announced. An instance that starts inside the 24 hours shows the remembered version if it is still newer than itself, and otherwise "Up to date"; the balloon appears once per version across both sides and restarts. Before any check has run, the state is "Not checked yet".
- **Dashboard item.** It is shown whatever the runtime's state, unlike the other attention items, and `DashboardAttention.Forget` keeps its dismissal.
- **Versions.** The running and the launcher's versions are the product version up to the `+`, as `SettingsViewModel.VersionText` reads it. Every untagged build is a pre-release (`1.1.1-N`), which SemVer sorts below `1.1.1-rc.1`. Lines of `SHA256SUMS.txt` are `<hex>  <asset name>`.
- **Tests.** `AppUpdater` against fakes of both ports: every row of the failure table, no downgrade, pre-release rules, the other-side case. Adapter tests for response parsing and URL validation. `InputValidationTests` gains the rule that only GitHub `https` URLs are downloaded.
- **Docs.** `CHANGELOG.md` (Added), `README.md` (the update paragraph, and that the app contacts GitHub to check for updates and how to turn that off), `AGENTS.md` (the layout table and the network rule).

## Calls made here, not by the maintainer

- The automatic check is **on by default**. It sends GitHub the request and a generic `WslcDesktop/<version>` user agent, nothing else.
- One automatic check per start; a running app does not check again on its own.
- Release notes are a link to the release page, not rendered in the app.
- Pre-releases are offered only to pre-release builds; there is no switch for it.
- The other-side and manual-replacement case is detected from the launcher file's version.
- A release without `SHA256SUMS.txt` cannot be installed.
- The found version and the announced version are remembered in settings, shared by both sides.
- The update restart never starts the runtime, even with "start the runtime with the app" on.
- The updating instance gives up its single-instance mutex itself, rather than the new instance waiting for it, so a restart does not depend on the version it lands in.
- The dashboard item shows while the runtime is stopped.

## Left to the builder

- The HTTP client: `HttpClient` unless it costs too much in the published size, in which case a smaller Windows API behind the same port. Report the size difference.
- Exact names of types, settings and attention keys, following the existing ones.
- How dismissal of the attention item is remembered per version in the existing dismissal store.
- Whether the unpack lock is reachable from both elevation levels; if it is not, the swap retries instead.
- Timeouts, the size limit and the temporary file names.

## Verification

- Full checks from `AGENTS.md`, with 0 warnings.
- The published app, unelevated and elevated: check, download, install and restart, from the window and from an autostarted tray instance. This needs a real release that is newer than the build under test. The maintainer chose a temporary pre-release of today's `main`: `v1.1.1-rc.1` on the commit the feature branches from, with no feature code pushed.
    - The tag is created on GitHub only, never locally, so local builds stay `1.1.1-N`, which sorts below `1.1.1-rc.1` and is offered the update.
    - The test runs on a copy of the published launcher in a scratch folder, not on the file the Run key starts.
    - It proves check, download, verification, swap, restart, and the other side's **Restart**. It does not prove restarting into a build that has the updater, since the release is built from code without it.
    - "Start the runtime with the app" must be off while testing: the release build does not know the argument that skips it.
    - Afterwards the release and its tag are deleted from GitHub. Nothing is tagged, pushed or deleted without the maintainer's go-ahead at that moment.
- Settings are backed up before driving the app and restored afterwards; the runtime is not started.
