# Server foundation execution plan

Status: complete.

1. Confirm installed 4.1.2 assemblies and official v4.1 examples.
2. Target .NET 10 and exact SPT packages 4.1.2.
3. Register an SPT `IHttpListener`, post-database loader, and read-only adapters.
4. Enforce loopback, dashboard token, and anti-CSRF policy.
5. Build bounded immutable item search and read-only profile projection.
6. Add dry-run unlock planning, audit contracts, and synthetic atomic backups.
7. Run focused Release tests, package owned assemblies only, and commit.

Exit gates passed on 2026-08-20: 26/26 tests passed; Release built with 0 warnings and 0 errors; installed profile hashes and database metadata were unchanged across validation; the package contains exactly two SPTDevSuite-owned assemblies; no install was performed.
