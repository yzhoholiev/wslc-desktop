#Requires -Version 7.0

param(
    [ValidateSet('win-x64', 'win-arm64')]
    [string]$Runtime = 'win-x64',
    [string]$Output = "$PSScriptRoot\publish\$Runtime"
)

$ErrorActionPreference = 'Stop'

# The Native AOT linker locates MSVC through vswhere.exe, which the VS installer does not put on PATH.
$installerDir = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer'
if ((Test-Path "$installerDir\vswhere.exe") -and -not ($env:PATH -split ';' -contains $installerDir)) {
    $env:PATH = "$installerDir;$env:PATH"
}

$appDir = "$PSScriptRoot\artifacts\app\$Runtime"
$payload = "$PSScriptRoot\artifacts\payload-$Runtime.tar.zst"

Remove-Item $appDir -Recurse -Force -ErrorAction SilentlyContinue
dotnet publish "$PSScriptRoot\src\WslcDesktop.WinUI\WslcDesktop.WinUI.csproj" -c Release -r $Runtime -o $appDir
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Remove-Item $payload -Force -ErrorAction SilentlyContinue
# Packed by the SDK rather than by this shell, whose own .NET may predate Zstandard support.
dotnet run "$PSScriptRoot\tools\PackPayload.cs" -- $appDir $payload
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

# The launcher unpacks each distinct payload into its own folder named after this tag.
$payloadVersion = (Get-FileHash $payload -Algorithm SHA256).Hash.Substring(0, 16).ToLowerInvariant()

Remove-Item $Output -Recurse -Force -ErrorAction SilentlyContinue
dotnet publish "$PSScriptRoot\src\WslcDesktop.Launcher\WslcDesktop.Launcher.csproj" -c Release -r $Runtime -o $Output -p:PayloadPath=$payload -p:PayloadVersion=$payloadVersion
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$exe = Get-Item "$Output\WslcDesktop.exe"
Write-Host ("Published {0} ({1:N1} MB)" -f $exe.FullName, ($exe.Length / 1MB))
