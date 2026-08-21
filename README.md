# SPTDevSuite

Private, server-only development dashboard foundation for exactly SPT 4.1.2.

The mod uses SPT's existing `IHttpListener` pipeline at `/devsuite`; it does not create another listener. Dashboard and API requests are accepted only from loopback. The HTML bootstrap sets a random, process-lifetime dashboard token in an HttpOnly `SameSite=Strict` cookie and exposes a separate anti-CSRF value for future state-changing requests. No CORS policy is added.

This milestone is read-only at runtime. Overview, Items, Profile, and Settings are functional. Unlocks, Traders, Quests, Skills, Hideout, Raids, and Backups show `Not implemented in this foundation milestone` and have no write endpoints.

## Build and test

```powershell
dotnet test .\SPTDevSuite.slnx --configuration Release
dotnet build .\SPTDevSuite.slnx --configuration Release
```

Release builds assemble the install-ready package at `Build\SPTDevSuite`. It contains only `SPTDevSuite.Server.dll` and `SPTDevSuite.Contracts.dll`. This repository does not install the package.

## Runtime use

After a separately approved installation, start SPT 4.1.2 and browse to the configured local server origin with `/devsuite`. The dashboard must be opened locally. Profile overview requires the normal SPT `PHPSESSID` cookie and reads the in-memory PMC profile through `ProfileHelper`.

## Safety

Do not point tests at a real SPT profile. Backup tests create a synthetic JSON file under an operating-system temporary directory. See `docs/SAFETY_BOUNDARIES.md` and `docs/PROFILE_OPERATIONS.md` before adding any mutation.
