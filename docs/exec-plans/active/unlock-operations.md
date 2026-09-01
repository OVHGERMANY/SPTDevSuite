# Unlock operations

Status: in progress (source and synthetic validation only)

## Scope

Implement the dashboard's state-changing operations for exact official SPT 4.1.3:
`ExamineAllItems`, `UnlockFlea`, `MaxProfileLevel`, `MaxTraders`, and
`MaxSkills`. Every request is previewed first and must name its selected
modules. `CompleteQuests` is separate from the default profile preset and
requires the exact confirmation text `COMPLETE_ALL_QUESTS`.

## Safety sequence

1. Require loopback dashboard token plus anti-CSRF header.
2. Resolve exactly the current in-memory SPT session through `ProfileHelper`.
3. Reject unsupported, empty, duplicate, or quest-completion module requests.
4. Clone the PMC profile using SPT's `ICloner`, apply the requested mutations
   to the clone, and validate the clone serializes and preserves required
   profile identity and inventory roots.
5. On apply, serialize the live PMC, validate JSON, SHA-256 it, and persist
   that rollback payload and its audit record through SPT `ProfileDataService`
   under the mod's own profile-data key. No user profile file is opened.
6. Apply the same validated operation to the live in-memory PMC, revalidate,
   and persist it through `SaveServer.SaveProfileAsync`.
7. Append a redacted audit entry containing module names, timestamps, backup
   key, hash, result, and warnings. A failed save restores the clone-derived
   snapshot before returning an error.
8. For `CompleteQuests`, mark every tracked quest and every current quest
   template as successful. Do not replay rewards, mail, reputation changes,
   or mutually-exclusive branch failures; they are unrelated side effects and
   would make a bulk completion non-deterministic.

## Deployment gate

Build and synthetic tests must pass. The installed package is not overwritten
while EFT, SPT server, launcher, or the database is running. Runtime acceptance
will be user-led after the deployment gate is satisfied.

## Exact 4.1.3 validation record

On 2026-09-01, the 0.2.0 source passed 33/33 synthetic Release tests both with
the published `SPTushonka.*` 4.1.3 packages and against the official staged
`SPT.Server/4.1.3-RELEASE+ddce41c.20260820` runtime. The explicit runtime build
completed with 0 warnings and 0 errors and assembled only the two SPTDevSuite
DLLs. Incompatible runtimes are rejected independently by state-changing HTTP
routing and the unlock service before profile dependencies are accessed. The
deployment gate passed a non-mutating `-WhatIf` check; no deployment, profile
access, or live runtime acceptance was performed.
