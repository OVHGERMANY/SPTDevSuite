# Architecture

`SPTDevSuite.Server` is the SPT-loadable .NET 10 library. `ModMetadata` declares exact 4.1.2 compatibility. `FoundationLoader` runs after `OnLoadOrder.PostLoad`, checks `ProgramStatics.SPT_VERSION()`, and builds the item index from already-loaded tables.

`DashboardHttpListener` implements SPT 4.1.2's `IHttpListener`. It receives the existing ASP.NET `HttpContext` and the session `MongoId` resolved by SPT from `PHPSESSID`. It owns only `/devsuite` and starts no listener.

`SPTDevSuite.Contracts` contains bounded DTOs, planning models, audit records, and the backup interface. Runtime adapters project SPT models into these contracts; the dashboard never serializes a full database or profile.

The item index is immutable, deterministically ordered by template ID, and capped at 100,000 records. Query responses are capped at 200 records. The profile adapter serializes only the in-memory PMC object to an ephemeral JSON element so projection code cannot mutate it.

The backup implementation writes a temporary file, validates JSON and SHA-256, and atomically renames it. Its interface also produces deterministic retention and validated rollback plans; replacement remains unimplemented and it is not registered as an HTTP operation.
