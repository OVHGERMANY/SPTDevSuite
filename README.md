# SPTDevSuite

SPTDevSuite 0.2.0 is a server-only development dashboard for exactly official SPT 4.1.3 on .NET 10.

The server-only package compiles against the exact official 4.1.3 server assemblies and is designed to load through the SPT mod loader without adding a client DLL. Automated compatibility and safety checks pass; this release-preparation run did not start SPT or apply changes to a live profile. NuGet package IDs use `SPTushonka.*`; the published assemblies and C# namespaces remain `SPTarkov.*`.

The mod uses SPT's existing `IHttpListener` pipeline at `/devsuite`; it does not create another listener. Dashboard and API requests are accepted only from loopback. The HTML bootstrap sets a random, process-lifetime dashboard token in an HttpOnly `SameSite=Strict` cookie and exposes a separate anti-CSRF value for future state-changing requests. No CORS policy is added.

Overview, Items, Profile, Unlocks, and Settings are implemented and synthetically validated. The Unlocks endpoint supports preview-first `ExamineAllItems`, `UnlockFlea`, `MaxProfileLevel`, `MaxTraders`, and `MaxSkills` operations. `CompleteQuests` is separate and requires `COMPLETE_ALL_QUESTS`. Apply requests use the current in-memory profile through supported SPT services, persist rollback data under the mod's own profile-data keys, validate identity and inventory roots, save through `SaveServer`, and append a redacted audit entry. Traders, Skills, Hideout, Raids, and Backups remain unimplemented as separate pages.

## Build and test

```powershell
dotnet test .\SPTDevSuite.slnx --configuration Release
dotnet build .\SPTDevSuite.slnx --configuration Release
```

To validate against the exact official SPT 4.1.3 runtime instead of the published 4.1.3 packages, pass its runtime directory explicitly:

```powershell
$serverBin = 'C:\path\to\SPT_Runtime'
dotnet test .\SPTDevSuite.slnx --configuration Release -p:SptServerAssemblyRoot=$serverBin
dotnet build .\SPTDevSuite.slnx --configuration Release -p:SptServerAssemblyRoot=$serverBin
```

Release builds assemble the runtime files at `Build\SPTDevSuite`. Run `tools\New-SptDevSuiteRelease.ps1` to produce the deterministic end-user ZIP. The ZIP contains one instruction file plus the two SPTDevSuite assemblies under their final `SPT_Runtime\user\mods\SPTDevSuite` path. Building and packaging do not install the mod. The separate `tools\Deploy-SptDevSuite.ps1` maintainer gate verifies exact runtime and package identities, blocks while SPT processes are active, preserves a rollback directory, and refuses to overwrite an unknown installation.

## Installation

Download `SPTDevSuite-0.2.0-SPT-4.1.3.zip`, stop SPT Server, SPT Launcher, and EFT, then extract the ZIP into the SPT installation directory that contains `SPT_Runtime`. The resulting runtime files are:

```text
SPT_Runtime\user\mods\SPTDevSuite\SPTDevSuite.Contracts.dll
SPT_Runtime\user\mods\SPTDevSuite\SPTDevSuite.Server.dll
```

Back up any existing `SPTDevSuite` directory before replacement. Use a disposable or separately backed-up profile for the first Apply. Do not Apply until the dashboard Preview is correct.

## Runtime use

Start exact official SPT 4.1.3 and browse to the configured local server origin with `/devsuite`. The dashboard must be opened locally. Profile operations require the normal SPT `PHPSESSID` cookie; state-changing requests additionally require the process-lifetime dashboard token and anti-CSRF value. Successful startup and live profile mutation were not exercised during this release-preparation run.

## Safety

Do not point tests at a real SPT profile. Tests use synthetic model objects and temporary directories; validation must not read or write an installed profile. See `docs/SAFETY_BOUNDARIES.md`, `docs/PROFILE_OPERATIONS.md`, and the active unlock plan before changing any mutation.

## License

Copyright (c) 2026 jbnel. All Rights Reserved. This release is distributed for end-user download and use; no open-source license is granted.
