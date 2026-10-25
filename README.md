<p align="center">
  <img src="src/WslcDesktop.WinUI/Assets/AppIcon.png" width="96" height="96" alt="WSLC Desktop icon">
</p>

<h1 align="center">WSLC Desktop</h1>

<p align="center">
  A native Windows desktop for <a href="https://learn.microsoft.com/windows/wsl/wsl-container"><code>wslc</code></a>, the WSL container CLI, in the spirit of Docker Desktop and Podman Desktop: containers, images, volumes, networks and WSL itself, from one window or the notification area.
</p>

<p align="center">
  <a href="https://github.com/yzhoholiev/wslc-desktop/actions/workflows/build.yml"><img src="https://github.com/yzhoholiev/wslc-desktop/actions/workflows/build.yml/badge.svg" alt="build"></a>
  <a href="https://github.com/yzhoholiev/wslc-desktop/releases/latest"><img src="https://img.shields.io/github/v/release/yzhoholiev/wslc-desktop" alt="latest release"></a>
  <a href="LICENSE"><img src="https://img.shields.io/badge/license-MIT-blue.svg" alt="MIT license"></a>
  <img src="https://img.shields.io/badge/Windows-11-0078D4.svg" alt="Windows 11">
  <img src="https://img.shields.io/badge/.NET_10-Native_AOT-512BD4.svg" alt=".NET 10 Native AOT">
</p>

## ✨ Features

- **Dashboard.** Counts of containers, images, volumes and networks; live CPU and memory of the container VM with 3-minute graphs; container storage; VM uptime; the busiest containers; quick actions.
- **Containers.** A live list with state, status, ports, CPU and memory. Start, stop, restart and delete; open an interactive shell in Windows Terminal; read the logs (tail, follow, timestamps) and the `inspect` output.
- **Images.** Size, age and usage. Pull; run or create a container with ports, volumes, variables, resource limits, GPUs and more; delete; prune dangling or all unused images.
- **Volumes and networks.** Named and anonymous volumes with their mountpoints; user-defined networks. Delete and prune unused ones.
- **WSL containers.** The container session VM's settings from wslc's `settings.yaml`, each with the default it gets on this machine when the file sets none (`.wslconfig` does not apply to it); what the running VM reports; wslc versions; sessions (open a shell, terminate); edit the settings.
- **WSL.** Platform versions; shut down or update WSL (optionally to a pre-release); open WSL Settings or edit `.wslconfig`; distributions (open a terminal, set the default, terminate).
- **Clean up.** The `docker system prune` that wslc lacks: pick stopped containers, unused networks, unused images and unused volumes, and see the space it reclaimed.
- **Notification area.** Closing the window keeps the app running in the tray. Its Fluent menu shows the runtime status and reaches every container, clean-up, live updates, WSL actions and Settings.
- **Jump list.** Right-click the taskbar button to open a page, start or stop the container runtime, or clean up.
- **Settings.** Keep running in the tray; start with Windows, hidden in the tray; start the container runtime with the app and stop it on exit; Acrylic, Mica or Mica Alt background.

Destructive actions always ask first. The **Live** switch in the title bar pauses updates. The app runs once per user and elevation: starting it again brings the running window forward, from the tray, minimized or behind other windows.

## 📦 Install

1. Install or update WSL to 2.9.3 or later on Windows 11; `wslc` (`C:\Program Files\WSL\wslc.exe`) ships with it.
2. Download `WslcDesktop-win-x64.exe`, or `WslcDesktop-win-arm64.exe` on an Arm PC, from the [latest release](https://github.com/yzhoholiev/wslc-desktop/releases/latest). `SHA256SUMS.txt` lists their checksums.
3. Put it anywhere and run it. There is no installer, MSIX or runtime to install.

The download is one self-contained executable. On first run it unpacks the app to `%LOCALAPPDATA%\WslcDesktop\app\<content hash>` and starts it; later runs start it directly. To update, exit the app from the tray menu and replace the executable: the new version unpacks beside the old one, and versions that are no longer running are deleted. While the old version is still running, starting the new executable only brings the running window forward.

> [!NOTE]
> The executable is not code-signed, so SmartScreen may warn on first run. Choose **More info → Run anyway**, or first compare `(Get-FileHash .\WslcDesktop-win-x64.exe).Hash` with its line in `SHA256SUMS.txt`. Smart App Control, when it is on, blocks unsigned apps outright and offers no way to run them.

To uninstall, turn off **Settings → Start with Windows**, exit from the tray menu, and delete the executable and `%LOCALAPPDATA%\WslcDesktop` (settings, crash log and the unpacked app). The app also names itself for notifications under `HKCU\Software\Classes\AppUserModelId\WslcDesktop` (`WslcDesktop.Administrator` for an elevated instance), which you can delete too. Windows keeps the taskbar jump list until you unpin the app or it falls out of the recent list.

## 🖱️ Command line

The jump list starts the app with these arguments, and they work from a shortcut or a terminal too. When the app is already running, it acts on the command instead of starting a second instance; `--tray` then does nothing, since the app is already there.

| Argument | Does |
| --- | --- |
| `--tray` | Starts hidden in the notification area, as sign-in does |
| `--containers`, `--images`, `--volumes`, `--wsl` | Opens that page |
| `--start-runtime`, `--stop-runtime` | Starts or stops the container runtime |
| `--cleanup` | Opens the clean-up dialog |

## ⚙️ How it works

- **Reads.** Lists, stats and image details come from the Docker Engine API inside the wslc session, over one long-lived shell. When the engine cannot be used, reads fall back to `wslc --format json` for a minute.
- **Writes.** Every change (run, create, start, stop, remove, pull, prune, terminate) goes through `wslc.exe` or `wsl.exe`, which turn Windows paths into bind mounts, publish ports on Windows `localhost` and own the session lifecycle.
- **Never boots the VM by itself.** The app checks for the session with `wslc info`, which does not start one. Container, stats and VM queries run only while the session exists, and they name it. Starting the runtime is a user action, or the opt-in "start with the app" setting.
- **Cheap while stopped.** A running runtime is sampled every 3 seconds. While it is stopped, the app watches for a new `wslcsession.exe`, the process each wslc session runs in, which starts nothing; it asks `wslc` only when that set of processes changes, when you open the window or the tray menu, and every 30 seconds as a fallback.
- **Sessions and elevation.** wslc gives elevated and normal processes of one user separate default sessions, each with its own VM and containers. The app works with the session matching its own elevation and lists the other running sessions.
- **VM usage.** CPU, memory and uptime come from `/proc` inside the session, and storage from `df` on `/var/lib/docker`.

## 🛠️ Development

| Path | Contents |
| --- | --- |
| `src/WslcDesktop.Core` | Use cases, models and the ports the adapters implement; no WinUI, processes, files or registry |
| `src/WslcDesktop.Infrastructure` | Adapters: wslc, wsl.exe, the Docker Engine over a session shell, settings, the shell launcher |
| `src/WslcDesktop.Interop` | Win32 and COM wrappers (taskbar, jump list, window messages) through CsWin32 |
| `src/WslcDesktop.WinUI` | The WinUI 3 app: view models, pages, controls and the composition root |
| `src/WslcDesktop.Launcher` | The single-file launcher that carries and unpacks the app |
| `tests/` | xUnit v3 suites for each project, plus UI and end-to-end tests that drive the published app |

Building needs the .NET 10 SDK and Visual Studio 2026 (or 2022) with the **Desktop development with C++** workload, because the Native AOT linker uses MSVC.

```powershell
dotnet run --project src/WslcDesktop.WinUI                      # debug build (JIT)
dotnet build -c Release -p:Platform=x64                        # must report 0 warnings
dotnet test -c Release -p:Platform=x64 --no-build              # unit tests
.\publish.ps1                                                  # Native AOT → publish\win-x64\WslcDesktop.exe
.\publish.ps1 -Runtime win-arm64                              # another runtime

$env:WSLC_INTEGRATION = '1'; dotnet test --project tests/WslcDesktop.Infrastructure.Tests   # against the installed wslc; starts the VM
$env:WSLC_UI_TESTS = '1'; dotnet test --project tests/WslcDesktop.UiTests                  # drives the published exe; close the app first
$env:WSLC_E2E = '1'; dotnet test --project tests/WslcDesktop.UiTests --filter-class "*RunImageTests"   # every run-dialog option, end to end
```

The integration, UI and end-to-end tests are skipped unless their variable is set. The UI tests refuse to run while WSLC Desktop is open and restore your `settings.json` afterwards; the end-to-end tests start the runtime when it is stopped, stop it again, and remove everything they created. Set `WSLC_DESKTOP_EXE` to test another build.

[`AGENTS.md`](AGENTS.md) describes how the code is organised and the rules for changing it: where reads and writes go, input safety, and the Native AOT pitfalls.

Every push to `main` and every pull request builds, runs the unit tests and checks formatting in GitHub Actions, then publishes both executables as workflow artifacts. GitVersion stamps every build from the latest `v*` tag and the commits since (`GitVersion.yml`), so builds between releases carry a pre-release version. A `v*` tag also creates a GitHub release with the executables, `SHA256SUMS.txt`, `LICENSE.txt` and `THIRD-PARTY-NOTICES.txt`; a tag with a suffix, such as `v1.1.0-rc.1`, is marked as a pre-release. Dependabot proposes weekly NuGet and Actions updates once a release is 7 days old.

## 🩺 Troubleshooting

- **"wslc is not installed".** `wslc.exe` ships with WSL 2.9.3 and later. Choose **Update WSL** on the dashboard or run `wsl --update`; the app finds it without a restart.
- **The containers are missing when run as administrator.** wslc gives elevated and normal processes separate sessions, each with its own VM and containers. An elevated WSLC Desktop shows **Administrator** in the title bar and works with the elevated session.
- **Settings and logs.** Settings live in `%LOCALAPPDATA%\WslcDesktop\settings.json`, and unhandled exceptions are appended to `crash.log` in the same folder. **Settings → Open app data folder** opens it. A settings file that cannot be read is kept as `settings.json.bak` before the app replaces it with defaults.
- **Reporting a problem.** Open an [issue](https://github.com/yzhoholiev/wslc-desktop/issues) with your Windows and WSL versions (the **WSL** page lists them) and, for a crash, the end of `crash.log`. Report security problems as described in [SECURITY.md](SECURITY.md).

## 🔒 Privacy

WSLC Desktop collects no telemetry and makes no network requests of its own. It talks only to `wslc`, `wsl.exe` and the Docker Engine inside your wslc session; what those reach, such as a registry when you pull an image, is up to them. Links you click, such as a published port or the documentation, open in your browser.

## 📄 License

[MIT](LICENSE). The components the app ships with, among them the .NET runtime and the Windows App SDK, are listed with their license terms in [THIRD-PARTY-NOTICES.txt](THIRD-PARTY-NOTICES.txt), which **Settings → Licenses** also opens.

WSLC Desktop is an independent project. It is not made, endorsed or supported by Microsoft; Windows, WSL and Windows Subsystem for Linux are trademarks of the Microsoft group of companies.
