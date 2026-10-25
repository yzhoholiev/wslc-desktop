# Security policy

## Reporting a vulnerability

Please report security problems privately through GitHub's [private vulnerability reporting](https://github.com/yzhoholiev/wslc-desktop/security/advisories/new), not in a public issue. Include the version (**Settings → About**), what an attacker can do, and the steps to reproduce it.

You can expect a first answer within a week. Fixes ship in a new release, and the advisory is published once the release is out.

## Supported versions

Only the latest release gets security fixes.

## Scope

WSLC Desktop runs `wslc.exe` and `wsl.exe` with argument lists and runs commands in the wslc session's shell (see [AGENTS.md](AGENTS.md#input-safety)). Problems in `wslc`, WSL or the container images themselves belong to their projects; report WSL issues at https://github.com/microsoft/WSL/security.
