# SPTDevSuite

SPTDevSuite 0.2.0 is a server-only development dashboard for exactly official SPT 4.1.3 on .NET 10.

The server-only package compiles against the exact official 4.1.3 server assemblies and is designed to load through the SPT mod loader without adding a client DLL. Catalog initialization and loopback dashboard behavior are covered by synthetic tests; live 4.1.3 loading and profile-operation acceptance remain pending. NuGet package IDs use `SPTushonka.*`; the published assemblies and C# namespaces remain `SPTarkov.*`.

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

Release builds assemble the install-ready package at `Build\SPTDevSuite`. It contains only `SPTDevSuite.Server.dll` and `SPTDevSuite.Contracts.dll`. Building does not install the package. The explicit `tools\Deploy-SptDevSuite.ps1` gate verifies exact runtime and package identities, blocks while SPT processes are active, preserves a rollback directory, and refuses to overwrite an unknown installation.

## Installation candidate

SPTDevSuite 0.2.0 is not yet a stable public release. For an approved experimental installation, stop SPT and EFT, then place the two packaged assemblies at:

```text
SPT_Runtime\user\mods\SPTDevSuite\SPTDevSuite.Contracts.dll
SPT_Runtime\user\mods\SPTDevSuite\SPTDevSuite.Server.dll
```

The target directory must contain only those two files. Back up any existing `SPTDevSuite` directory before replacement. Verify the release ZIP against its `.sha256` sidecar, and use a disposable profile for the first runtime pass. Do not use an Apply operation until the dashboard has passed Preview on that profile.

## Runtime use

After a separately approved installation, start SPT 4.1.3 and browse to the configured local server origin with `/devsuite`. The dashboard must be opened locally. Profile operations require the normal SPT `PHPSESSID` cookie; state-changing requests additionally require the process-lifetime dashboard token and anti-CSRF value. Successful startup and mutation behavior are acceptance checks, not claims established by the offline test suite.

## Safety

Do not point tests at a real SPT profile. Tests use synthetic model objects and temporary directories; validation must not read or write an installed profile. See `docs/SAFETY_BOUNDARIES.md`, `docs/PROFILE_OPERATIONS.md`, and the active unlock plan before changing any mutation.
