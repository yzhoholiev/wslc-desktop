# AGENTS.md

Guidance for coding agents (and humans) working on WSLC Desktop: a WinUI 3 desktop UI for `wslc`, the WSL container CLI, published as a .NET 10 Native AOT app. Read `README.md` for what the app does; this file is about how the code is meant to be changed.

## Build, test, verify

Run from the repository root. A change is done when all of these pass.

```powershell
dotnet build -c Release -p:Platform=x64                       # must report 0 warnings
dotnet test -c Release -p:Platform=x64 --no-build             # unit tests; integration and UI tests skip
dotnet build -p:Platform=x64                                  # format loads projects in Debug and needs the libraries built
dotnet format whitespace WslcDesktop.slnx --verify-no-changes
dotnet format style WslcDesktop.slnx --verify-no-changes
.\publish.ps1                                                 # Native AOT build -> publish\win-x64\WslcDesktop.exe
```

- The version comes from GitVersion (`GitVersion.yml`, `GitVersion.MsBuild` in every `src` project): a `v1.2.3` tag releases 1.2.3, a `v1.2.3-rc.1` tag a pre-release of it (commits after it build as `rc.2`), and later commits build as pre-releases of the next patch (`1.2.4-N`, whose file version `1.2.4.5500N` sorts below the release's `1.2.4.60000`). Do not pass `-p:Version`; it is overwritten. Builds need the git history, so CI checks out with `fetch-depth: 0`.
- AOT-only failures exist (see [Native AOT](#native-aot-and-winui)). Anything touching UI, WinRT delegates, resources or collections must be checked in the **published** app, not only in a Debug run.
- Integration tests (`WSLC_INTEGRATION=1`) start the session VM; UI tests (`WSLC_UI_TESTS=1`) drive the published exe; end-to-end tests (`WSLC_E2E=1`, `RunImageTests`) run every run-dialog option through the published app and check the container with `wslc inspect` and the Docker Engine. All are opt-in. The end-to-end fixture starts the runtime when it is stopped and stops it again afterwards, and removes the containers, volumes and network it created, all named `wslcdesktop-e2e-…`; it pulls `busybox:latest`, `nginx:alpine` and `alpine:3` when missing and keeps them for the next run. Note that `wslc list` and `images` start a stopped runtime, so never use them to check state afterwards.
- Never close or kill a WSLC Desktop instance you did not start: the user may be using it. Back up `%LOCALAPPDATA%\WslcDesktop\settings.json` before driving the app and restore it afterwards. Capture windows with `PrintWindow`, not screen grabs.
- A shell running as Administrator starts the app elevated, which talks to a different session (see [Sessions](#sessions-and-elevation)). Start it through `explorer.exe` or `runas /trustlevel:0x20000` to test the normal case.

## Layout and responsibilities

The logic is kept apart from WinUI so another presentation (WinForms, a tray-only host) can reuse it. Anything that depends on WinUI, or is used only by the WinUI app, stays in `WslcDesktop.WinUI` (its exe is still `WslcDesktop.exe`, the name the launcher, Run key and UI tests use); only code another presentation or process would use moves to a library. `LayeringTests` fails if Core gains a WinUI, WinRT, CommunityToolkit or adapter-project reference, or uses processes, files or the registry itself.

| Project and folder | Holds | Must not |
| --- | --- | --- |
| `WslcDesktop.Core` `Services/` | Use cases (`RuntimeMonitor` with its sampler, `RuntimeActions`, `ContainerActions`, `SystemCleanup`, `Notifier`); the ports adapters implement (`IRuntimeReader`, `IRuntimeSession`, `IRuntimeCommands`, `IRuntimeDetails`, `IWslPlatform`, `IWslConfig`, `IWslcSettings`, `IShellLauncher`, `ISettingsStore`, `ISessionHosts`); `AppServices`, which holds one of each; the exceptions logic catches; the `CrashLog` facade; pure helpers and launch commands | Reference WinUI, WinRT or another project of this solution; start processes or touch files and the registry |
| `WslcDesktop.Core` `Models/` | DTOs and options records | Contain behaviour beyond small derived properties |
| `WslcDesktop.Core` `Glyphs.cs` | Segoe Fluent Icons code points used from code | |
| `WslcDesktop.Core` `Formatting/` | Parsing of text any UI renders the same way: `LogParser`, `JsonTokenizer` | |
| `WslcDesktop.Infrastructure` | The adapters: `Cli`, `WslcCli` and `WslCli`, `DockerEngine` over a `SessionShell`, `CliRuntimeReader`, `FallbackRuntimeReader` (with `IRuntimeConnection`), `SettingsStore`, `WslConfigFile`, `WslcSettingsFile`, `ShellLauncher`, `CrashLogFile`, `Launcher`; Docker Engine DTOs and the source-generated JSON contexts; `DefaultServices.Create()`, which builds the real `AppServices`. Only `DefaultServices`, `CrashLogFile`, `Launcher` and `WslcCli` (for the static `CreateArguments` the run dialog previews; its constructor is internal) are public | Decide what the UI shows, which belongs to a use case in Core; make an adapter public so the UI can bypass `AppServices` |
| `WslcDesktop.Interop` | Every Win32/COM call, wrapped in managed types (`TrayIcon`, `Taskbar`, `JumpList`, `WindowMessageHook`, `WindowMessages`, `Foreground`, `PopupWindow`, `WindowGeometry`, `ProcessMemory`, `ShellProperties`, `AppIdRegistration`); window message hooks go through `WindowSubclass`. It references only Core and takes app IDs, names and paths as arguments | Decide app identity (names, icons, the launcher path); leak pointers or CsWin32 types in public signatures; grant `InternalsVisibleTo` to its users (each assembly generates its own internal CsWin32 types, which would then clash) |
| `WslcDesktop.WinUI` `ViewModels/` | Page state and commands on CommunityToolkit.Mvvm, their list item records, `Labels` for state texts, `EmptyText`, and `Bindings.cs` with the x:Bind helpers that return WinUI types | Call `wslc` for reads directly; touch controls |
| `WslcDesktop.WinUI` `Views/`, `Controls/` | XAML pages and controls. Resource list pages supply only a toolbar, column headers and a row template to `ResourceListView`; dashboard and settings cards are `StatCard`, `MetricCard`, `SettingRow`. Dialogs built in code (`CleanupDialog`, `RunImageDialog` with its `RunImageForm`) use the form pieces in `FormParts` and `PairRows` | Hold logic that can be tested without UI |
| `WslcDesktop.WinUI` `MainWindow.*.cs` | The window, split by concern: lifetime, navigation, placement, tray | Grow a new concern without its own partial file |
| `WslcDesktop.WinUI` `Interop/` | The app's own values for the Interop wrappers: `JumpListTasks`, and `AppIdentity` with the app ID, display name and launcher that the taskbar and jump list get | Call Win32 itself; WinUI has no `NativeMethods.txt`, and new calls get a wrapper in `WslcDesktop.Interop` |
| `WslcDesktop.Launcher` | The single-file launcher that unpacks and starts the app | Depend on the app project |

### Services and wiring

- `App` is the composition root: it creates one `AppServices` with `DefaultServices.Create()` and passes it to `MainWindow`, which passes it to the pages it creates, which pass it to their view models.
- `App.Services` exists only for controls that XAML creates without arguments (`ResourceListView`, `InspectPanel`, `PortChips` inside row templates). Everything else takes `AppServices` in its constructor; Core, Infrastructure, Interop and view models never reach for it.
- Interop types get what they need as arguments, failures included (`onFailure` callbacks), instead of app services.
- A new external dependency (a process, file, registry key or service) gets a port in Core and an adapter in Infrastructure, and a fake in `tests/Shared/TestServices.cs`.

### Single responsibility, without over-splitting

- One reason to change per type. When a class starts mixing concerns (polling *and* parsing *and* history), split along those seams, as `RuntimeMonitor` → `RuntimeSampler`, `InventoryReader`, `MetricsHistory`.
- Do not split for its own sake: a cohesive class with several small members is fine. Prefer a new partial file or a nested private class before a new public type.
- Put decisions in pure, testable members and keep the edges thin: `WslcCli.CreateArguments`, `ShellWords`, `ImageConfigDto.StartsShell`, `LaunchCommands.Parse` are tested without a VM or UI; code-behind only wires them up.
- New shared behaviour goes where its siblings live (a list feature into `ResourceListViewModel`, a runtime-wide action into `RuntimeActions`), not into the page that first needed it.

## Talking to the runtime

### Reads: Docker Engine first

All reads go through `AppServices.Reader`, a `FallbackRuntimeReader`:

1. **Docker Engine API** inside the session (`DockerEngine`): `curl --unix-socket /var/run/docker.sock http://localhost/v1.41/...` over one long-lived `SessionShell`. Preferred: one round trip, structured JSON, no process per query.
2. **`wslc` CLI** (`CliRuntimeReader`) when the engine is unusable. `EngineUnavailableException` switches reads to the CLI for a minute.

When you add a read:

- Add it to `IRuntimeReader` with both implementations, the `FallbackRuntimeReader` pass-through and the test fakes (`FakeRuntimeReader` in `tests/Shared`, `FallbackRuntimeReaderTests.FakeReader`). Cover the engine side with `FakeShell` in `DockerEngineTests`.
- Throw `EngineUnavailableException` only when the engine itself is broken. An answer such as "not found" (HTTP 404) is a `CliException`, otherwise one missing object disables the engine for every read.
- Pin response shapes with DTOs in Infrastructure's `Models/DockerEngineDtos.cs` and map them to the CLI DTOs in `DockerEngineMapper`, so pages never know which source answered.

Existing CLI-only reads (`wslc info`, `inspect` text in the details panels, container logs) are deliberate; do not copy that pattern for new list or detail data.

### Writes: always `wslc`

Every change to the runtime (run, create, start, stop, remove, pull, prune, session terminate) goes through `IRuntimeCommands`, implemented by `WslcCli`, never through Engine `POST`/`DELETE`. `wslc` does work the raw API does not: it turns Windows paths like `D:\data` into bind mounts, publishes ports on Windows `localhost`, and owns the session lifecycle.

- Every CLI call has a timeout, from the static fields in `WslcCli` (query, operation, start, bulk, pull) or `WslCli`. A hung VM must not block the app.
- Long or runtime-wide operations go through `RuntimeActions.RunExclusiveAsync`, so two of them never overlap, and report outcomes through `Notifier`. The running one is `RuntimeMonitor.Operation`, which every page shows as busy, whichever of them (or the tray, or a jump-list command) started it. In view models, `PageViewModel.RunRuntimeOperationAsync` does both; `RunOperationAsync` only reports.

### Sessions and elevation

- `wslc` gives elevated and normal processes of one user **separate default sessions** (`wslc-cli-admin-<user>` / `wslc-cli-<user>`), each its own VM and containers. Elevated instances also get their own taskbar identity, tray name and single-instance mutex.
- The app must never boot the VM on its own: `wslc info` does not start a session, `list`/`images` do. Starting it is a user action (or the opt-in "start with the app" setting).
- Each session runs in its own `wslcsession.exe`, started by wslservice and kept for up to a minute after the session ends; that held for the normal and elevated default sessions and for a restart within that minute, which starts a new one. SDK-created named sessions were not checked. While the runtime is stopped, `RuntimeSampler` asks wslc again only when that set of processes changes (`ISessionHosts`), and for three more samples after a change, since a host can appear before its session is listed. `RuntimeMonitor` forces a check every 30 s in case one is missed. Do not add per-sample `wslc` calls for a stopped runtime.
- `wslc` needs a usable stdin; started without one it fails with `ERROR_INVALID_HANDLE`. `Cli` redirects it.

## Input safety

- Start processes with `ProcessStartInfo.ArgumentList` and `UseShellExecute = false` (`Cli.CreateStartInfo`). Never build a command line by string concatenation. With an argument list, `;`, `&`, `$`, quotes and spaces reach the program literally.
- `wslc` reads the token after an option as its value even when it starts with `-`, and everything after the image as the container's command, so dialog values cannot inject options. Positional values typed by the user (an image to pull) are checked with `ImageReference.IsValid` first, because `--help` would otherwise be read as an option.
- Commands run inside the session (`SessionShell`) are shell command lines. Only fixed text, IDs from the engine's own output, or values that passed a whitelist check (`ImageReference.IsValid`) may appear in them, and inside single quotes.
- Anything written into the Windows Run key or a taskbar relaunch command is quoted; `Launcher.Accept` rejects paths that are relative, missing or contain a quote.
- Add a test for each rule above when you touch it (`InputValidationTests`, `DockerEngineTests`, `ContainerCreateArgumentsTests`).

## Native AOT and WinUI

These fail only in the published AOT build, often with misleading errors:

- **`x:Bind` only.** No `{Binding}`, no `IValueConverter`; binding functions live in `ViewModels/Bindings.cs`.
- **Bound collections:** every `ObservableCollection<T>` used as an `ItemsSource` needs a `[GeneratedWinRTExposedExternalType]` line in `WinRTExposedTypes.cs`, or WinUI throws `E_INVALIDARG`.
- **Collection expressions** assigned to `IReadOnlyList<T>`/`IEnumerable<T>` trigger CsWinRT1032; use arrays (`T[]`) for record properties and returned lists, and `ReadOnlySpan<T>` for parameters that take one.
- **WinRT delegates:** pass a lambda, never a method group bound to a WinRT object (`TryEnqueue(() => element.StartBringIntoView())`, not `TryEnqueue(element.StartBringIntoView)`): CsWinRT throws `InvalidCastException` when marshalling it. Decline the IDE's "convert to method group" there.
- **Resources:** only brushes and styles cast back from `Application.Current.Resources`; colors, font families and doubles come back as bare `IInspectable`. Read colors from a `SolidColorBrush`, build `FontFamily` in code.
- **Theme changes:** a brush read from resources keeps the theme it was read in, so a light/dark or contrast switch leaves it stale. Colors chosen in code or by an `x:Bind` function apply a style whose setters use `{ThemeResource}` (`SuccessDotStyle`, `NeutralPillStyle`, `TertiaryTextStyle`); only UI rebuilt on each open (dialogs, flyouts) may take a brush.
- **Presenter:** `AppWindow.Presenter as OverlappedPresenter` is `null`; create and keep an `OverlappedPresenter`.
- **JSON:** source-generated contexts only (`WslcJsonContext`, `DockerEngineJsonContext`, `AppSettingsJsonContext`); register every new DTO there.
- **Classes CsWinRT projects** (implementing WinRT interfaces, or nested in such types) must be `partial` (CsWinRT1028). This applies only to `WslcDesktop.WinUI` and its tests: the libraries and the other test projects target `net10.0-windows`, without the WinRT projection, so they cannot take a WinRT dependency.
- **ContentDialog** measures its content at full height and clips it. Long dialogs wrap their content in a `ScrollViewer` with an explicit `MaxHeight` tied to the window (`RunImageForm.TrackWindow`).
- **Async UI events:** `async void` only for event handlers, and only around calls that report their own errors; anything invoked from native code (window subclass procedures, tray callbacks) catches and logs to `CrashLog`.

## Dependencies

- Prefer what .NET, the Windows App SDK and Microsoft packages already provide; check for an existing framework API before writing one.
- Win32 and COM go through **CsWin32** (`NativeMethods.txt`, `allowMarshaling: false`). Every call gets a managed wrapper in `WslcDesktop.Interop`, which owns the only app-side `NativeMethods.txt` (the launcher has its own); the options are shared in `src/NativeMethods.json`. Interop builds as AnyCPU with `CsWin32PlatformTarget` x64, whose 64-bit declarations ARM64 shares. Handles the app owns (the tray icon) are held in a `SafeHandle`; borrowed HWNDs stay plain. Third-party UI helper libraries (WinUIEx and similar) were tried and removed; keep our own small wrappers.
- Versions are central in `Directory.Packages.props`; shared build settings in `Directory.Build.props`, with `src/Directory.Build.props` (target framework, root namespace, AOT analysis, and CsWin32 for any project with a `NativeMethods.txt`) and `tests/Directory.Build.props` (xUnit, the linked `tests/Shared` helpers) layered on top. A project file holds only what is specific to it.
- `Windows.UI.StartScreen.JumpList` and other package-identity APIs do not work in this unpackaged app; that is why `WslcDesktop.Interop`'s `JumpList` uses `ICustomDestinationList`.
- Notifications are tray balloons, named through `AppIdRegistration` in `WslcDesktop.Interop`. `AppNotificationManager` was tried and removed: elevated processes cannot show app notifications, and in this self-contained build it showed nothing even unelevated.

## Code style

`.editorconfig` and `WslcDesktop.sln.DotSettings` are the source of truth (the maintainer formats with Rider). In short:

- File-scoped namespaces, `var`, usings sorted alphabetically (`System` not first), a blank line after multi-line statements, local functions after the code that uses them.
- `CancellationToken` is the last parameter.
- Sizes of memory and disk use IEC units through `ByteSize` (KiB, MiB, GiB).
- Segoe Fluent Icons glyphs used from code are named `\uXXXX` constants in `Glyphs.cs`; XAML writes them as `&#xXXXX;`. Never raw private-use characters.
- Templates that use `x:Bind` and are shared by pages live in `Views/Templates.xaml` (a dictionary with a class behind it); `App.xaml` cannot hold them.
- User-facing text: plain, sentence case, says what happens; destructive actions confirm first.

### Comments

- Default to no comment; make names carry the meaning. A comment must answer a question the code raises: why this choice, where a magic value comes from, a gotcha (AOT, wslc behaviour, Windows quirks).
- Comments state facts about the code as it is: no narration of changes, no history, no ticket keys.
- `//` only inside method bodies. Declarations use a multi-line `/// <summary>` block, with summary text indented four spaces after `///`.
- A doc comment with `<param>`, `<returns>` or `<exception>` also has a `<summary>`, and documents every parameter.

## Tests

- xUnit v3 on Microsoft.Testing.Platform (`global.json`). Unit tests use fakes instead of a VM: `TestServices` in `tests/Shared` builds an `AppServices` over a fake of every port (`FakeRuntimeCommands` records the commands it ran), and `FakeShell` answers for the engine.
- Each project has its test project: `WslcDesktop.Core.Tests` (use cases and helpers, with `LayeringTests`), `WslcDesktop.Infrastructure.Tests` (adapters and the opt-in integration tests) and `WslcDesktop.WinUI.Tests` for what stays in the app. Helpers they share live in `tests/Shared` and are linked in.
- Before restructuring code whose behaviour is not pinned, add characterization tests in a separate commit, then refactor with them green.
- Parameterised tests use `TheoryData`/`MemberData` with readable case names; guard tests that enumerate a type's members (as `CreateArguments_MapsEveryOptionProperty`) catch additions that skip a mapping.

## Commits

- Short imperative subject, no trailing period, no issue key, and no mention of AI tools.
- Never revert or drop uncommitted edits you did not make; commit them along, separately when they are unrelated.
