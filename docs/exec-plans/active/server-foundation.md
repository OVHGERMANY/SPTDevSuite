# Server foundation execution plan

Status: complete; exact 4.1.3 migration validation is recorded below.

1. Confirm official 4.1.3 assemblies and .NET 10 runtime metadata.
2. Target .NET 10 and exact `SPTushonka.*` packages 4.1.3 while retaining `SPTarkov.*` assembly and namespace identities.
3. Register an SPT `IHttpListener`, post-database loader, and read-only adapters.
4. Enforce loopback, dashboard token, and anti-CSRF policy.
5. Build bounded immutable item search and read-only profile projection.
6. Add dry-run unlock planning, audit contracts, and synthetic atomic backups.
7. Run focused Release tests, package owned assemblies only, and commit.

The original foundation exit gates passed on 2026-08-20 for SPT 4.1.2. The exact official SPT 4.1.3 migration was source-built and tested on 2026-09-01 against `SPT.Server/4.1.3-RELEASE+ddce41c.20260820`; 33/33 synthetic tests passed, Release built with 0 warnings and 0 errors, and the package contained exactly two SPTDevSuite-owned assemblies. Version-reporting and fail-closed write-routing regressions are covered. No install, runtime profile access, or runtime acceptance was performed.
