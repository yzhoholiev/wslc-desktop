# In-App Update Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** WSLC Desktop finds a newer GitHub release, and on one click downloads it, verifies it, replaces the launcher it was started from and restarts into it.

**Architecture:** A Core use case, `AppUpdater`, owns the update state and the steps, over two ports: `IReleaseFeed` (GitHub) and `IInstalledApp` (the launcher file and this process). Infrastructure implements both. WinUI shows the state in Settings, the dashboard banner and the tray, and performs the restart, because only the window knows its state and holds the single-instance mutex.

**Tech Stack:** .NET 11 Native AOT, WinUI 3, CommunityToolkit.Mvvm, `HttpClient`, `System.Text.Json` source generation, xUnit v3.

**Spec:** `docs/superpowers/specs/2026-11-15-app-update-design.md`

## Global Constraints

- Nothing is downloaded or installed until the user asks. The app never runs a file that failed its checks, never downgrades, and never starts or stops the runtime as a side effect of updating.
- The check sends only the request and the user agent `WslcDesktop/<version>`. No other header, query value or body.
- Repository: `yzhoholiev/wslc-desktop`. Assets: `WslcDesktop-win-x64.exe`, `WslcDesktop-win-arm64.exe`, `SHA256SUMS.txt` with lines `<hex>  <asset name>`.
- Only `https` URLs whose host is `github.com`, or ends with `.github.com` or `.githubusercontent.com`, are requested. Redirects are followed by hand, at most 5, each hop checked.
- Check interval 24 hours. API timeout 30 s. Download timeout 10 min. Download size limit 200 MB.
- Core must not use files, processes, the registry or the network (`LayeringTests`). A port goes in Core, its adapter in Infrastructure, its fake in `tests/Shared/TestServices.cs`.
- AOT rules from `AGENTS.md`: `x:Bind` only, source-generated JSON, arrays rather than collection expressions for `IReadOnlyList<T>`, lambdas for WinRT delegates, `partial` projected classes.
- Comments: none by default; `//` only in method bodies; declarations use a multi-line `/// <summary>` block.
- Per task, build only the touched projects and run their tests. The full `AGENTS.md` checks and `.\publish.ps1` run in Task 10.
- Work on branch `feature/app-update`. Commit only with the maintainer's go-ahead; subjects are short, imperative, with no trailing period and no mention of AI tools. Nothing is pushed or tagged outside Task 11.

## Review Focus

1. The release has no asset for this architecture, or no `SHA256SUMS.txt`: the app says the version cannot be installed here and offers the release page, with no **Update** that fails later. (Task 3)
2. A release tag that is not a version (`nightly`, `v2`): ignored, state "Up to date", no exception. (Tasks 1, 2)
3. **Update** pressed twice, or from the tray and Settings at once: the second request does nothing while one runs. (Task 3)
4. The launcher was moved or deleted after the app started: message plus the release page, and no download. (Task 3)
5. GitHub answers 403 (rate limit), HTML, or truncated JSON: **Check now** shows a reason; an automatic check stays quiet and keeps the previous state. (Tasks 2, 4)

---

### Task 1: Version comparison and checksum parsing

**Files:**
- Create: `src/WslcDesktop.Core/Services/AppVersion.cs`, `src/WslcDesktop.Core/Services/ChecksumFile.cs`
- Test: `tests/WslcDesktop.Core.Tests/Services/AppVersionTests.cs`, `tests/WslcDesktop.Core.Tests/Services/ChecksumFileTests.cs`

**Interfaces:**
- Produces:
  - `public sealed record AppVersion : IComparable<AppVersion>` with `static bool TryParse(string? text, [NotNullWhen(true)] out AppVersion? version)`, `bool IsPreRelease`, `int CompareTo(AppVersion? other)`, operators `<`, `>`, `<=`, `>=`, and `ToString()` returning `1.2.0` or `1.2.0-rc.1`.
  - `public static class ChecksumFile` with `static string? Find(string text, string assetName)`: the lowercase 64-character hex for that asset, or null.

- [ ] **Step 1: Write the failing tests**

```csharp
[Theory]
[InlineData("1.1.1-10", "1.1.1-rc.1")]     // an untagged build is offered the rc
[InlineData("1.1.1-rc.1", "1.1.1-rc.2")]
[InlineData("1.1.1-rc.2", "1.1.1-rc.10")]  // numeric identifiers compare as numbers
[InlineData("1.1.1-rc.1", "1.1.1")]
[InlineData("1.1.0", "1.1.1-1")]
[InlineData("1.9.0", "1.10.0")]
public void CompareTo_OrdersBySemVer(string lower, string higher)   // Parse(lower) < Parse(higher), and not the reverse

[Theory]
[InlineData("v1.2.0", "1.2.0")]
[InlineData("1.2.0+Branch.main.Sha.abc", "1.2.0")]
[InlineData("1.1.1-8+Branch.main", "1.1.1-8")]
public void TryParse_DropsTagPrefixAndBuildMetadata(string text, string expected)

[Theory]
[InlineData(null)] [InlineData("")] [InlineData("nightly")] [InlineData("v2")] [InlineData("1.2")] [InlineData("1.2.x")] [InlineData("1.2.3-")]
public void TryParse_RejectsWhatIsNotAVersion(string? text)

[Fact] public void CompareTo_TreatsEqualVersionsAsEqual()   // "1.2.0" vs "v1.2.0+x" => 0
```

```csharp
const string Sums = "AB12…(64 hex)  WslcDesktop-win-x64.exe\ncd34…(64 hex)  WslcDesktop-win-arm64.exe\n";
[Fact] public void Find_ReturnsTheLowercaseHashOfTheAsset()
[Fact] public void Find_AcceptsCrLfAndTheBinaryMarker()          // "<hex> *WslcDesktop-win-x64.exe\r\n"
[Fact] public void Find_ReturnsNullForAMissingAsset()
[Fact] public void Find_ReturnsNullForAHashOfTheWrongLength()
[Fact] public void Find_DoesNotMatchANameThatOnlyEndsWithTheAsset() // "…  evil-WslcDesktop-win-x64.exe"
```

- [ ] **Step 2: Run them and see them fail**

Run: `dotnet test tests/WslcDesktop.Core.Tests -p:Platform=x64 --filter-class "*AppVersionTests" --filter-class "*ChecksumFileTests"`
Expected: build error, the types do not exist.

- [ ] **Step 3: Implement both types**

SemVer 2.0 precedence: major, minor, patch as numbers; a version without a pre-release part is higher than one with; pre-release identifiers compare left to right, numeric ones as numbers and below alphanumeric ones, ordinal otherwise, and a shorter list is lower when all shared identifiers are equal.

- [ ] **Step 4: Run the tests and see them pass**
- [ ] **Step 5: Commit** — `Add version comparison and checksum parsing`

---

### Task 2: Ports, settings and the update check

**Files:**
- Create: `src/WslcDesktop.Core/Models/AppRelease.cs`, `src/WslcDesktop.Core/Models/UpdateState.cs`, `src/WslcDesktop.Core/Services/IReleaseFeed.cs`, `src/WslcDesktop.Core/Services/IInstalledApp.cs`, `src/WslcDesktop.Core/Services/UpdateException.cs`, `src/WslcDesktop.Core/Services/AppUpdater.cs`
- Modify: `src/WslcDesktop.Core/Models/AppSettings.cs`, `src/WslcDesktop.Infrastructure/Services/AppSettingsJsonContext.cs` only if `DateTimeOffset?` needs registering, `tests/Shared/TestServices.cs` (fakes only; `AppServices` is wired in Task 5)
- Test: `tests/WslcDesktop.Core.Tests/Services/AppUpdaterCheckTests.cs`, `tests/WslcDesktop.Infrastructure.Tests/Services/SettingsStoreTests.cs`

**Interfaces:**
- Consumes: `AppVersion` (Task 1).
- Produces:

```csharp
public sealed record AppRelease(AppVersion Version, Uri Page, Uri? Asset, long AssetSize, Uri? Checksums);

public enum UpdateStatus { Unavailable, NotChecked, Checking, UpToDate, Available, Downloading, RestartRequired, Failed }

// Failed keeps Version when an update is still on offer, so Update can be tried again.
public sealed record UpdateState(UpdateStatus Status, AppVersion? Version = null, double Progress = 0, string? Message = null, Uri? Page = null);

public interface IReleaseFeed
{
    Task<AppRelease?> FindLatestAsync(bool includePreReleases, CancellationToken ct);   // null: no usable release
    Task<string> ReadChecksumsAsync(AppRelease release, CancellationToken ct);
    Task DownloadAsync(AppRelease release, string path, IProgress<double> progress, CancellationToken ct);  // progress 0..1
}

public interface IInstalledApp
{
    AppVersion RunningVersion { get; }
    string AssetName { get; }                       // WslcDesktop-win-x64.exe or -arm64.exe
    bool HasLauncher { get; }                       // false for a build output
    AppVersion? ReadLauncherVersion();              // null when the file is gone or carries no version
    string ReserveDownloadPath();                   // beside the launcher; throws UpdateException when it cannot be written
    Task<(long Size, string Sha256)> InspectAsync(string path, CancellationToken ct);
    void Install(string downloadedPath);            // swap with backup; restores the old file on failure, then throws UpdateException
    void Discard(string path);                      // never throws
    void Restart(string[] arguments);
}

public sealed class UpdateException(string message, Exception? inner = null) : Exception(message, inner);

public sealed class AppUpdater
{
    public static Uri ReleasesPage { get; }         // https://github.com/yzhoholiev/wslc-desktop/releases
    public AppUpdater(IReleaseFeed feed, IInstalledApp app, ISettingsStore settings, TimeProvider time);
    public UpdateState State { get; }
    public event Action? Changed;
    public event Action? RestartRequested;          // Task 3
    public void Start(SynchronizationContext context);   // events are then posted to it; begins automatic checks
    public Task CheckAsync(bool isManual, CancellationToken ct = default);
    public AppVersion? TakeAnnouncement();          // the available version, once per version across restarts and sides
}
```

  - `AppSettings`: `bool CheckForUpdates { get; set; } = true;`, `DateTimeOffset? LastUpdateCheck`, `string? AvailableUpdate`, `string? AnnouncedUpdate`.
  - Fakes in `tests/Shared/TestServices.cs`: `FakeReleaseFeed` (`Latest`, `PreRelease`, `Checksums`, `Failure`, `DownloadFailure`, `Requests` list, `PendingDownload` task source) and `FakeInstalledApp` (settable `RunningVersion`, `HasLauncher = true`, `LauncherVersion`, `Downloaded` size and hash, `InstallFailure`, `ReserveFailure`, and `Installed`, `Discarded`, `Restarts` lists).

**Behaviour to implement:**
- Initial state: `Unavailable` without a launcher. Otherwise `RestartRequired(launcher version)` when the launcher is newer than the running version; else `Available(version, Page)` when `AvailableUpdate` parses and is newer than the running version; else `UpToDate` when `LastUpdateCheck` is set; else `NotChecked`.
- `CheckAsync`: does nothing when `Unavailable`, or while a check or install runs. First re-reads the launcher version (other-side case, no network). Then `FindLatestAsync(includePreReleases: RunningVersion.IsPreRelease)`. A release that is null or not strictly newer gives `UpToDate` and clears `AvailableUpdate`. Success saves `LastUpdateCheck` and `AvailableUpdate`.
- A failed manual check gives `Failed(Message: the exception's message)`, keeping `Version` when one was on offer. A failed automatic check restores the previous state and does not move `LastUpdateCheck`.
- `Start`: checks at once when due, then re-evaluates hourly through `time.CreateTimer`. Due means `CheckForUpdates` is on and `LastUpdateCheck` is null, 24 hours or more ago, or in the future.

- [ ] **Step 1: Write the failing tests** (`AppUpdaterCheckTests`, running version `1.1.0` unless stated, time from `FakeTimeProvider` or a small local `TimeProvider`)

```csharp
State_IsUnavailable_WithoutALauncher                       // and CheckAsync(true) makes no request
State_IsNotChecked_BeforeAnyCheck
State_RemembersTheVersionTheLastCheckFound                 // AvailableUpdate "1.2.0" => Available 1.2.0
State_IsUpToDate_WhenTheRememberedVersionIsNotNewer        // running 1.2.0, AvailableUpdate "1.2.0"
State_AsksForARestart_WhenTheLauncherIsAlreadyNewer        // LauncherVersion 1.2.0 => RestartRequired 1.2.0
CheckAsync_OffersANewerRelease                             // saves AvailableUpdate "1.2.0" and LastUpdateCheck, raises Changed
CheckAsync_IgnoresAnOlderOrEqualRelease                    // 1.1.0 and 1.0.0 => UpToDate, AvailableUpdate null
CheckAsync_AsksForPreReleasesOnlyFromAPreReleaseBuild      // feed.Requests: false for 1.1.0, true for 1.1.1-10
CheckAsync_OffersThePreReleaseToAnUntaggedBuild            // running 1.1.1-10, latest 1.1.1-rc.1 => Available
CheckAsync_ShowsTheReason_WhenAManualCheckFails            // Failure = UpdateException("GitHub is not reachable.") => Failed, same message
CheckAsync_StaysQuiet_WhenAnAutomaticCheckFails            // state unchanged, LastUpdateCheck null
CheckAsync_DetectsANewerLauncherWithoutTheNetwork          // LauncherVersion set after construction => RestartRequired, no request
Start_ChecksAtOnce_WhenNoCheckHasRun
Start_DoesNotCheck_WhenAutomaticChecksAreOff
Start_DoesNotCheck_WithinADayOfTheLastOne                  // LastUpdateCheck = now - 23 h
Start_ChecksAgain_AfterADay                                // advance 25 h => second request
Start_ChecksAgain_WhenTheLastCheckIsInTheFuture
TakeAnnouncement_ReturnsAVersionOnce                       // second call null; AnnouncedUpdate saved; a new AppUpdater over the same settings returns null
```

`SettingsStoreTests`: `Save_KeepsTheUpdateFields` round-trips the four new properties; `Load_TurnsAutomaticChecksOn_ForSettingsWrittenBeforeTheyExisted`.

- [ ] **Step 2: Run them and see them fail**

Run: `dotnet test tests/WslcDesktop.Core.Tests -p:Platform=x64 --filter-class "*AppUpdaterCheckTests"`

- [ ] **Step 3: Implement the models, ports, settings fields, fakes and the check half of `AppUpdater`**

`AppUpdater` guards a check or install with one busy flag taken through `Interlocked`; raise `Changed` through the context given to `Start`, or directly when `Start` was not called.

- [ ] **Step 4: Run the Core and Infrastructure tests and see them pass, `LayeringTests` included**
- [ ] **Step 5: Commit** — `Add the update check`

---

### Task 3: Download, verify and install

**Files:**
- Modify: `src/WslcDesktop.Core/Services/AppUpdater.cs`
- Test: `tests/WslcDesktop.Core.Tests/Services/AppUpdaterInstallTests.cs`

**Interfaces:**
- Consumes: everything from Task 2, `ChecksumFile.Find` (Task 1).
- Produces on `AppUpdater`: `Task InstallAsync()`, `void CancelInstall()`, `void RequestRestart()` (raises `RestartRequested` when the state is `RestartRequired`), `bool CanInstall` (state `Available`, or `Failed` with a version).

**Behaviour to implement**, in order, each failure ending in `Failed(Version kept, Message, Page = the release page)`:
1. No-op unless `CanInstall`, or while an install runs.
2. Without a cached `AppRelease` (remembered version), fetch the latest again; when it is no longer newer, `UpToDate`.
3. `release.Asset` or `release.Checksums` null → "Version X cannot be installed from the app." No download.
4. Launcher gone (`ReadLauncherVersion()` null) or `ReserveDownloadPath()` throws → its message. No download.
5. `ChecksumFile.Find(checksums, app.AssetName)` null → "The release has no checksum for this download."
6. Download with state `Downloading` and progress; `CancelInstall` returns to `Available` and discards the file.
7. `InspectAsync`: size must equal `AssetSize` and be at most 200 MB; hash must equal the checksum. Otherwise discard, "The download did not match its checksum."
8. `Install`; on `UpdateException` discard and report its message.
9. State `RestartRequired(version)`, clear `AvailableUpdate`, raise `RestartRequested`.

- [ ] **Step 1: Write the failing tests**

```csharp
InstallAsync_DownloadsVerifiesInstallsAndAsksForARestart   // app.Installed has the reserved path; RestartRequested raised once; state RestartRequired 1.2.0
InstallAsync_ReportsProgress                               // feed reports 0.4 => a Changed with Downloading, Progress 0.4
InstallAsync_RejectsAChecksumMismatch                      // nothing installed, download discarded, state Failed with Version 1.2.0
InstallAsync_RejectsADownloadOfTheWrongSize
InstallAsync_RejectsADownloadOverTheSizeLimit              // AssetSize and size 201 MB
InstallAsync_DiscardsADownloadThatWasCutOff                // DownloadFailure
InstallAsync_DoesNotDownload_WithoutAChecksumFile          // Checksums null
InstallAsync_DoesNotDownload_WithoutALineForTheAsset
InstallAsync_DoesNotDownload_WithoutAnAssetForThisArchitecture
InstallAsync_DoesNotDownload_WhenTheLauncherFolderIsNotWritable   // ReserveFailure; Page set
InstallAsync_DoesNotDownload_WhenTheLauncherIsGone
InstallAsync_KeepsTheVersionOnOffer_WhenTheSwapFails       // InstallFailure => Failed, CanInstall true, download discarded
InstallAsync_IgnoresASecondRequestWhileOneRuns             // PendingDownload held: one download
InstallAsync_FetchesTheRelease_ForARememberedVersion
InstallAsync_DoesNothing_WhenNoUpdateIsOnOffer
InstallAsync_NeverInstallsAnOlderVersion                   // remembered 1.2.0, feed now answers 1.0.0 => UpToDate, nothing downloaded
CancelInstall_ReturnsToAvailableAndDiscardsTheDownload
RequestRestart_RaisesTheEvent_OnlyWhenARestartIsPending
```

- [ ] **Step 2: Run them and see them fail**
- [ ] **Step 3: Implement the install half**
- [ ] **Step 4: Run all Core tests and see them pass**
- [ ] **Step 5: Commit** — `Add update download and install`

---

### Task 4: GitHub release feed

**Files:**
- Create: `src/WslcDesktop.Infrastructure/Services/GitHubReleaseFeed.cs`, `src/WslcDesktop.Infrastructure/Services/GitHubUrl.cs`, `src/WslcDesktop.Infrastructure/Models/GitHubReleaseDtos.cs`, `src/WslcDesktop.Infrastructure/Services/GitHubJsonContext.cs`
- Test: `tests/WslcDesktop.Infrastructure.Tests/Services/GitHubReleaseFeedTests.cs`, `tests/WslcDesktop.Infrastructure.Tests/Services/InputValidationTests.cs`

**Interfaces:**
- Consumes: `IReleaseFeed`, `AppRelease`, `UpdateException`, `AppVersion`.
- Produces: `internal sealed class GitHubReleaseFeed(HttpMessageHandler handler, string assetName, AppVersion runningVersion) : IReleaseFeed`; `internal static class GitHubUrl { static bool IsAllowed(Uri url); }`; DTOs `GitHubReleaseDto` (`tag_name`, `html_url`, `draft`, `prerelease`, `assets`) and `GitHubAssetDto` (`name`, `size`, `browser_download_url`).

**Behaviour to implement:**
- Releases only: `GET https://api.github.com/repos/yzhoholiev/wslc-desktop/releases/latest`; a 404 means no release. With pre-releases: `GET …/releases?per_page=20`, drafts skipped, the highest parseable version wins.
- Headers: `User-Agent: WslcDesktop/<runningVersion>` and `Accept: application/vnd.github+json`, nothing else. The handler has automatic redirects and cookies off.
- A tag that is not a version is skipped. `Page` falls back to `AppUpdater.ReleasesPage` when `html_url` is not allowed. `Asset` and `Checksums` are null when missing or when their URL is not allowed.
- `HttpRequestException`, a timeout, a non-success status and `JsonException` all become `UpdateException` with a plain message: 403 or 429 → "GitHub is limiting requests. Try again later."; otherwise "GitHub is not reachable." or "GitHub answered with an error (<status>)."
- Download: write to `path` with `FileMode.CreateNew`; stop with `UpdateException` once the bytes exceed `AssetSize` or 200 MB.

- [ ] **Step 1: Write the failing tests** (a recording `HttpMessageHandler` answering from a URL → response map)

```csharp
FindLatestAsync_MapsTheLatestRelease                 // version 1.2.0, the x64 asset URL and size, the checksums URL
FindLatestAsync_PicksTheHighestVersion_WithPreReleases    // list of v1.1.1-rc.1, v1.1.0, v1.1.1-rc.2 => rc.2
FindLatestAsync_SkipsDraftsAndTagsThatAreNotVersions
FindLatestAsync_ReturnsNull_WhenThereIsNoRelease     // 404
FindLatestAsync_LeavesTheAssetOut_WhenItsUrlIsNotOnGitHub
FindLatestAsync_SendsOnlyTheUserAgentAndAccept       // request.Headers has exactly those two; no body, no query on /latest
FindLatestAsync_ExplainsARateLimit                   // 403 => UpdateException, "GitHub is limiting requests. Try again later."
FindLatestAsync_ExplainsAnAnswerThatIsNotJson        // "<html>" and "{\"tag_name\":" => UpdateException
DownloadAsync_FollowsARedirectToGitHubsFileHost      // 302 to https://release-assets.githubusercontent.com/… => file written, progress ends at 1
DownloadAsync_RefusesARedirectElsewhere              // 302 to https://example.com/x => UpdateException, example.com never requested
DownloadAsync_RefusesMoreThanFiveRedirects
DownloadAsync_StopsWhenTheBodyIsLargerThanAnnounced
```

`InputValidationTests`:

```csharp
[Theory]
[InlineData("https://github.com/a/b", true)]
[InlineData("https://api.github.com/x", true)]
[InlineData("https://release-assets.githubusercontent.com/x", true)]
[InlineData("http://github.com/a/b", false)]
[InlineData("https://github.com.evil.example/x", false)]
[InlineData("https://evilgithub.com/x", false)]
[InlineData("https://githubusercontent.com.evil.example/x", false)]
[InlineData("https://user:pw@github.com/x", false)]
[InlineData("file:///C:/x.exe", false)]
public void GitHubUrl_AllowsOnlyHttpsOnGitHubHosts(string url, bool allowed)
```

- [ ] **Step 2: Run them and see them fail**

Run: `dotnet test tests/WslcDesktop.Infrastructure.Tests -p:Platform=x64 --filter-class "*GitHubReleaseFeedTests" --filter-class "*InputValidationTests"`

- [ ] **Step 3: Implement the feed, the URL rule and the JSON context**
- [ ] **Step 4: Run the Infrastructure tests and see them pass**
- [ ] **Step 5: Commit** — `Add the GitHub release feed`

---

### Task 5: Installed launcher adapter and wiring

**Files:**
- Create: `src/WslcDesktop.Infrastructure/Services/InstalledApp.cs`
- Modify: `src/WslcDesktop.Core/Services/AppServices.cs`, `src/WslcDesktop.Infrastructure/Services/DefaultServices.cs`, `tests/Shared/TestServices.cs`
- Test: `tests/WslcDesktop.Infrastructure.Tests/Services/InstalledAppTests.cs`

**Interfaces:**
- Consumes: `IInstalledApp`, `GitHubReleaseFeed`, `AppUpdater`, `Launcher.Path`.
- Produces:
  - `internal sealed class InstalledApp(string? launcherPath, AppVersion runningVersion, Architecture architecture) : IInstalledApp`, with `static AppVersion ReadRunningVersion()` (the entry assembly's informational version; `0.0.0` when unreadable).
  - `AppServices` constructor gains `IReleaseFeed releases, IInstalledApp installed` after `settings`, and the property `AppUpdater Updater`.
  - `TestServices` gains `Releases` (`FakeReleaseFeed`) and `Installed` (`FakeInstalledApp`).

**Behaviour to implement:**
- `ReadLauncherVersion`: `FileVersionInfo.GetVersionInfo(path).ProductVersion` through `AppVersion.TryParse`; null when the file is missing.
- `ReserveDownloadPath`: `<launcher folder>\<launcher name>.<guid:N>.download`; proves the folder is writable by creating and deleting the file; `UnauthorizedAccessException` and `IOException` become `UpdateException("WSLC Desktop cannot write to <folder>.")`. Leftover `*.download` and `*.old` files of this launcher older than a day are deleted here.
- `Install`: take the launcher's unpack mutex `Local\WslcDesktop.Launcher.Unpack` for up to 10 s (continue without it on `UnauthorizedAccessException` or a timeout; tolerate `AbandonedMutexException`), then `File.Replace(downloaded, launcher, launcher + ".old")`, 5 attempts 250 ms apart on `IOException`. `File.Replace` leaves the launcher in place when it fails; when the launcher is nevertheless missing afterwards, move the backup back. Delete the backup on success.
- `Restart`: `ProcessStartInfo(launcher)` with `ArgumentList` and `UseShellExecute = false`.

- [ ] **Step 1: Write the failing tests** (files in a per-test temp folder; the "launcher" is any small file)

```csharp
ReadLauncherVersion_ReturnsNull_WhenTheFileIsGone
ReadLauncherVersion_ReadsTheProductVersion              // against the test host's own exe: equals AppVersion.TryParse of its ProductVersion
ReserveDownloadPath_IsBesideTheLauncher
ReserveDownloadPath_Throws_WhenTheFolderDoesNotExist    // UpdateException
ReserveDownloadPath_RemovesLeftoversOlderThanADay       // and keeps a fresh one
InspectAsync_ReturnsSizeAndLowercaseSha256              // known bytes "abc" => ba7816bf…15ad, size 3
Install_ReplacesTheLauncherAndLeavesNoBackup
Install_KeepsTheLauncher_WhenTheDownloadIsMissing       // UpdateException, old content intact
Install_Retries_WhileTheLauncherIsBrieflyLocked         // hold a FileShare.None handle for 400 ms
AssetName_FollowsTheArchitecture                        // X64 => WslcDesktop-win-x64.exe, Arm64 => WslcDesktop-win-arm64.exe
HasLauncher_IsFalse_ForABuildOutput                     // launcherPath null
```

- [ ] **Step 2: Run them and see them fail**
- [ ] **Step 3: Implement `InstalledApp`, then wire `AppServices`, `DefaultServices` (`new SocketsHttpHandler { AllowAutoRedirect = false, UseCookies = false }`) and `TestServices`**
- [ ] **Step 4: Build Release and run every unit test project**

Run: `dotnet build -c Release -p:Platform=x64` then `dotnet test -c Release -p:Platform=x64 --no-build`
Expected: 0 warnings, all pass.

- [ ] **Step 5: Measure what `HttpClient` costs**

Run `.\publish.ps1` on this branch and compare `publish\win-x64\WslcDesktop.exe` with the 19.9 MB before. Record both sizes in the task report. Over 2 MB more: stop and ask the maintainer whether to switch the adapter to WinHTTP behind the same port.

- [ ] **Step 6: Commit** — `Wire the updater into the app services`

---

### Task 6: Restart into the new version

**Files:**
- Create: `src/WslcDesktop.WinUI/MainWindow.Update.cs`
- Modify: `src/WslcDesktop.Core/Services/LaunchCommand.cs`, `src/WslcDesktop.WinUI/Program.cs`, `src/WslcDesktop.WinUI/App.xaml.cs`, `src/WslcDesktop.WinUI/MainWindow.xaml.cs`
- Test: `tests/WslcDesktop.Core.Tests/Services/LaunchCommandsTests.cs`, `tests/WslcDesktop.WinUI.Tests/MainWindowTests.cs`

**Interfaces:**
- Consumes: `AppUpdater.RestartRequested`, `IInstalledApp.Restart`, `MainWindow.CommandForPage`, `ExitApplicationAsync(keepRuntime: true)`.
- Produces:
  - `LaunchCommands.UpdatedArgument = "--updated"`.
  - `Program.ReleaseInstance()`: releases and disposes the single-instance mutex; safe to call twice.
  - `App.IsUpdateRestart` (static, set in `Program.Main` like `StartInTray`).
  - `internal static string[] MainWindow.RestartArguments(bool isHidden, string currentTag)`.

**Behaviour to implement:**
- `RestartArguments`: `["--tray", "--updated"]` when the window is hidden; otherwise the page argument when `CommandForPage` has one, followed by `"--updated"`.
- `MainWindow` subscribes to `Services.Updater.RestartRequested` and, on the UI thread: refuses while `Services.Monitor.Operation` is set (same message shape as the side switch: "Restart after the current operation"); calls `Program.ReleaseInstance()`; `Services.Installed.Restart(arguments)`; `ExitApplicationAsync(true)`. A `Win32Exception` or `InvalidOperationException` from the start reports "Could not restart WSLC Desktop" and leaves the app running.
- `App.OnLaunched` skips `StartRuntimeInBackgroundAsync` when `IsUpdateRestart`.
- `App.OnLaunched` calls `Services.Updater.Start(context)` after the monitor starts.

- [ ] **Step 1: Write the failing tests**

```csharp
// LaunchCommandsTests
Parse_IgnoresTheUpdatedArgument                    // ["--updated"] => None; ["--updated", "--images"] => ShowImages
// MainWindowTests
RestartArguments_ReopenInTheTray_WhenTheWindowWasHidden     // ["--tray", "--updated"]
RestartArguments_ReopenThePage                              // (false, "images") => ["--images", "--updated"]
RestartArguments_CarryOnlyTheMarker_ForAPageWithoutACommand // (false, "dashboard") => ["--updated"]
```

- [ ] **Step 2: Run them and see them fail**
- [ ] **Step 3: Implement**
- [ ] **Step 4: Run the Core and WinUI tests and see them pass**
- [ ] **Step 5: Commit** — `Restart the app after an update`

---

### Task 7: Settings → Updates

**Files:**
- Modify: `src/WslcDesktop.WinUI/ViewModels/SettingsViewModel.cs`, `src/WslcDesktop.WinUI/Views/SettingsPage.xaml`, `src/WslcDesktop.WinUI/Views/SettingsPage.xaml.cs`, `src/WslcDesktop.WinUI/ViewModels/Labels.cs`
- Test: `tests/WslcDesktop.WinUI.Tests/ViewModels/SettingsViewModelTests.cs`, `tests/WslcDesktop.WinUI.Tests/ViewModels/LabelsTests.cs`

**Interfaces:**
- Consumes: `AppUpdater` state, `CheckAsync(true)`, `InstallAsync`, `CancelInstall`, `RequestRestart`, `IShellLauncher.OpenUrl`.
- Produces: `Labels.UpdateStatus(UpdateState state) : string`, and on `SettingsViewModel` (which becomes an `ObservableObject` for these): `UpdateStatusText`, `IsUpdateSectionEnabled`, `CanCheck`, `CanUpdate`, `CanCancelUpdate`, `CanRestart`, `IsDownloading`, `DownloadProgress` (0–100), `CheckForUpdates`, and commands `CheckNowCommand`, `UpdateCommand`, `CancelUpdateCommand`, `RestartCommand`, `OpenReleasePageCommand`.

**Exact copy** (`Labels.UpdateStatus`):

| State | Text |
| --- | --- |
| `Unavailable` | Updates are available only in the released app. |
| `NotChecked` | Not checked yet |
| `Checking` | Checking for updates… |
| `UpToDate` | Up to date |
| `Available` | Version 1.2.0 is available |
| `Downloading` | Downloading… 40% |
| `RestartRequired` | Restart to finish updating to 1.2.0 |
| `Failed` | the state's message |

The section sits above About, built from `SettingRow`: a row with the status and the buttons **Check now**, **Update**, **Cancel**, **Restart**, each visible only when it applies, a `ProgressBar` while downloading, a **What's new** hyperlink button (the state's page, else `AppUpdater.ReleasesPage`), and the row **Check for updates automatically** with a `ToggleSwitch`. The view model subscribes to `Updater.Changed` in the page's `Loaded` and unsubscribes in `Unloaded`.

- [ ] **Step 1: Write the failing tests**

```csharp
// LabelsTests: one case per row of the table above (TheoryData<UpdateState, string>)
UpdateStatusText_FollowsTheUpdater                 // check finds 1.2.0 => "Version 1.2.0 is available", PropertyChanged raised
CanUpdate_IsTrueOnlyWhenAVersionIsOnOffer
CanRestart_IsTrueOnlyWhenARestartIsPending
UpdateSection_IsDisabled_WithoutALauncher          // CanCheck false too
CheckForUpdates_IsOnByDefaultAndSaved              // setting false => Settings.Saves == 1
OpenReleasePage_OpensTheReleaseOfTheOfferedVersion // Shell.Opened contains "url https://github.com/yzhoholiev/wslc-desktop/releases/tag/v1.2.0"
```

- [ ] **Step 2: Run them and see them fail**
- [ ] **Step 3: Implement the labels, the view model members and the XAML**
- [ ] **Step 4: Run the WinUI tests and see them pass; build `WslcDesktop.WinUI`**
- [ ] **Step 5: Commit** — `Add the Updates section to Settings`

---

### Task 8: Dashboard attention item

**Files:**
- Modify: `src/WslcDesktop.WinUI/ViewModels/DashboardAttention.cs`, `src/WslcDesktop.WinUI/ViewModels/DashboardViewModel.cs`, `src/WslcDesktop.WinUI/Views/DashboardPage.xaml`, `src/WslcDesktop.WinUI/Views/Templates.xaml` if the item template lives there
- Test: `tests/WslcDesktop.WinUI.Tests/ViewModels/DashboardAttentionTests.cs`, `tests/WslcDesktop.WinUI.Tests/ViewModels/DashboardViewModelTests.cs`

**Interfaces:**
- Consumes: `AppUpdater.State`, `Changed`, `InstallAsync`, `RequestRestart`.
- Produces: `AttentionKind.AppUpdate`; `DashboardAttention.UpdateKeyPrefix = "update:"`; `static AttentionItem? DashboardAttention.ForUpdate(UpdateState state, IReadOnlyDictionary<string, long> dismissed)`; `Task DashboardViewModel.UpdateAppAsync()`.

**Behaviour to implement:**
- `ForUpdate` returns an item for `Available` ("WSLC Desktop 1.2.0 is available") and `RestartRequired` ("Restart to finish updating to 1.2.0"), key `update:1.2.0`, tone Caution, dismissable, and null when that key is dismissed or for any other state.
- `ApplyAttention` puts the update item first and shows it whatever the runtime's state; the container and image items keep requiring a running runtime. With only the update item the banner uses the single-item layout, whose action button reads **Update** or **Restart**.
- `DashboardAttention.Forget` never removes keys with the `update:` prefix. Dismissing an update item removes every other `update:` key.
- The view model refreshes the banner on `Updater.Changed`.

- [ ] **Step 1: Write the failing tests**

```csharp
ForUpdate_OffersTheAvailableVersion                 // Kind AppUpdate, Key "update:1.2.0", Text "WSLC Desktop 1.2.0 is available"
ForUpdate_OffersTheRestart
ForUpdate_ReturnsNull_ForADismissedVersion
ForUpdate_ReturnsAgain_ForANewerVersion             // dismissed "update:1.2.0", state 1.3.0
ForUpdate_ReturnsNull_WhenThereIsNothingToInstall   // UpToDate, Checking, Downloading, Failed
Forget_KeepsTheDismissedUpdate
Attention_ShowsTheUpdate_WhileTheRuntimeIsStopped   // view model: HasAttention, SingleAttention.Kind == AppUpdate
Attention_ListsTheUpdateFirst                       // with one failed container
Dismiss_HidesTheUpdateUntilANewerVersion            // DismissedAttention has only "update:1.2.0" among update keys
UpdateAppAsync_InstallsTheUpdate                    // Installed.Installed.Count == 1
```

- [ ] **Step 2: Run them and see them fail**
- [ ] **Step 3: Implement**
- [ ] **Step 4: Run the WinUI tests and see them pass**
- [ ] **Step 5: Commit** — `Offer an app update on the dashboard`

---

### Task 9: Tray entry and balloon

**Files:**
- Modify: `src/WslcDesktop.WinUI/MainWindow.Tray.cs`, `src/WslcDesktop.WinUI/MainWindow.Update.cs`, `src/WslcDesktop.WinUI/ViewModels/Labels.cs`
- Test: `tests/WslcDesktop.WinUI.Tests/ViewModels/LabelsTests.cs`

**Interfaces:**
- Consumes: `AppUpdater.State`, `Changed`, `TakeAnnouncement`, `InstallAsync`, `RequestRestart`, `TrayIcon.ShowBalloon`.
- Produces: `Labels.UpdateMenuText(UpdateState state) : string?` — "Update to 1.2.0" for `Available` and for `Failed` with a version, "Restart to finish updating" for `RestartRequired`, "Downloading update… 40%" for `Downloading` (shown disabled), otherwise null.

**Behaviour to implement:**
- The tray menu shows the entry, with `Glyphs.Update`, above **Settings** while `UpdateMenuText` is not null. It runs `InstallAsync` or `RequestRestart`.
- On `Changed` with state `Available`: when `TakeAnnouncement()` returns a version, show the balloon "WSLC Desktop 1.2.0 is available" / "Open the notification area menu and choose Update to 1.2.0."
- A `Failed` state that follows an install started from the tray shows its message as an error balloon, since no window may be open.
- The handler catches and logs to `CrashLog`, as the other tray callbacks do.

- [ ] **Step 1: Write the failing `LabelsTests` cases** for `UpdateMenuText`, one per state.
- [ ] **Step 2: Run them and see them fail**
- [ ] **Step 3: Implement**
- [ ] **Step 4: Run the WinUI tests and build `WslcDesktop.WinUI`**
- [ ] **Step 5: Commit** — `Offer an app update from the tray`

---

### Task 10: Documentation and full checks

**Files:**
- Modify: `CHANGELOG.md`, `README.md`, `AGENTS.md`

- [ ] **Step 1: `CHANGELOG.md`**, under `## [Unreleased]` → Added: "The app checks GitHub for a new version once a day and can download and install it itself, from Settings, the dashboard or the notification area menu. Turn the check off under Settings → Updates."
- [ ] **Step 2: `README.md`**: replace the manual update paragraph with the in-app flow, keep the manual steps as the fallback for a read-only folder, and state that the app contacts `api.github.com` only to check for updates, what it sends (a version in the user agent), and how to turn it off.
- [ ] **Step 3: `AGENTS.md`**: add `AppUpdater`, `IReleaseFeed`, `IInstalledApp` to the Core row, `GitHubReleaseFeed`, `InstalledApp`, `GitHubJsonContext` to the Infrastructure row, `MainWindow.Update.cs` as a concern, the JSON context to the AOT list, and an Input safety line: downloads follow only `https` GitHub hosts (`GitHubUrl.IsAllowed`), checked on every redirect, and a downloaded file is installed only when its SHA-256 matches the release's `SHA256SUMS.txt`.
- [ ] **Step 4: Run the full checks**

```powershell
dotnet build -c Release -p:Platform=x64
dotnet test -c Release -p:Platform=x64 --no-build
dotnet build -p:Platform=x64
dotnet format whitespace WslcDesktop.slnx --verify-no-changes
dotnet format style WslcDesktop.slnx --verify-no-changes
.\publish.ps1
```

Expected: 0 warnings, every test passes or is skipped as before, no format changes, `publish\win-x64\WslcDesktop.exe` produced.

- [ ] **Step 5: Look at the published app** on a copy of the launcher in a scratch folder, started through `explorer.exe`, with `settings.json` backed up first: Settings → Updates renders, **Check now** reaches GitHub and answers "Up to date", the switch persists, the dashboard and tray show nothing. Capture with `PrintWindow`. Restore `settings.json`. Do not close an instance you did not start.
- [ ] **Step 6: Commit** — `Document in-app updates`

---

### Task 11: Verify against a real release (maintainer's go-ahead required at Step 2 and Step 7)

- [ ] **Step 1: Prepare.** Confirm the published test build's product version sorts below `1.1.1-rc.1` (`AppVersion` test from Task 1 covers `1.1.1-N`; a branch-named pre-release label must start with a letter before `r`, otherwise publish the test build from `main` after a local fast-forward). Copy `publish\win-x64\WslcDesktop.exe` to a scratch folder. Back up `settings.json`. Confirm "Start the container runtime with the app" is off and note the runtime's state.
- [ ] **Step 2: Ask the maintainer, then create the pre-release on GitHub only:** `git push origin 47c714a:refs/tags/v1.1.1-rc.1`. No local tag. Wait for the release job to publish both executables and `SHA256SUMS.txt`.
- [ ] **Step 3: Unelevated, window open:** start the copy through `explorer.exe`; the check offers 1.1.1-rc.1 in Settings, the dashboard and the tray, with one balloon. **Update** shows progress and restarts on the same page; the launcher copy's product version is now `1.1.1-rc.1`, no `.download` or `.old` file remains, and the runtime's state is unchanged.
- [ ] **Step 4: Tray instance:** restore the test copy, start it with `--tray`, update from the tray menu; it comes back in the tray with no window.
- [ ] **Step 5: The other side:** restore the test copy, start it unelevated and elevated, update from one; the other shows only **Restart** after its next check, and restarting it works.
- [ ] **Step 6: Failure paths that need the real thing:** a copy in a folder made read-only for the user reports the folder and offers the release page; with the network off, **Check now** shows a reason and the automatic check stays quiet.
- [ ] **Step 7: Ask the maintainer, then remove the release and tag:** `gh release delete v1.1.1-rc.1 --cleanup-tag --yes`; confirm with `gh release list` and `git ls-remote --tags origin` that neither remains. Restore `settings.json`, delete the scratch copies, and leave the runtime in the state noted in Step 1.
- [ ] **Step 8: Report** what each step showed, with the captures, and what the test could not prove: restarting into a build that has the updater.
