# Project state

Foundation milestone: implemented on 2026-08-20. Exact official SPT 4.1.3 migration: source-validated on 2026-09-01.

- Compatibility: exact official SPT 4.1.3 on .NET 10, checked by metadata, `SPTarkov.*` assembly identities `4.1.3.0`, and fail-closed startup, HTTP write-routing, and unlock-service policies.
- Release metadata: SPTDevSuite 0.2.0, `All Rights Reserved`.
- Build dependencies: exact `SPTushonka.Common`, `SPTushonka.DI`, and `SPTushonka.Server.Core` NuGet packages 4.1.3. Assemblies and namespaces remain `SPTarkov.*`.
- Runtime changes during migration validation: none. No server, database, or profile was started, opened, or modified.
- Functional pages: Overview, Items, Profile, Unlocks, Settings.
- Deferred pages: Traders, Quests, Skills, Hideout, Raids, Backups. Quest completion is available only as the separately confirmed `CompleteQuests` unlock module.
- Item source: immutable bounded index built once from `TemplateTable.Items` after database load.
- Profile source: supported in-memory `ProfileHelper.GetPmcProfile(sessionId)` projection.
- Unlock safety: loopback token, anti-CSRF, preview-first requests, exact confirmation text, clone validation, profile-data rollback payload, supported save service, and redacted audit records.
- Validation: 33/33 synthetic Release tests passed against `SPT.Server/4.1.3-RELEASE+ddce41c.20260820`; build completed with 0 warnings and 0 errors. Regression coverage verifies the dashboard reports the declared mod version and rejects POST, PUT, PATCH, and DELETE plus direct unlock-service calls before profile dependencies are accessed on an incompatible runtime.

## Acceptance boundary

- The runtime package contains exactly `SPTDevSuite.Server.dll` and `SPTDevSuite.Contracts.dll`; the end-user ZIP adds only `README.txt` and places the DLLs under `SPT_Runtime/user/mods/SPTDevSuite` for one-extract installation. No client DLL, test binary, source file, third-party assembly, profile, or database is packaged.
- No 4.1.3 deployment or live runtime acceptance was performed. First Apply remains a user-led verification on a disposable or separately backed-up profile.
- The earlier EFT 46777 port evidence is historical and is not compatibility evidence for this official 4.1.3 package.
