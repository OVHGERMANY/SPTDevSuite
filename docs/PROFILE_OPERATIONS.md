# Profile operations

Profile operations are exposed only through the authenticated loopback dashboard. Preview is the default; apply requires an exact confirmation and a persisted rollback payload before the live in-memory profile is changed.

The write sequence is fixed: resolve one supported in-memory session, serialize and SHA-256 the current PMC into the mod's profile-data rollback key, compute a dry-run, require an explicit module request and confirmation, apply to an isolated clone, validate identity and inventory-root invariants, apply the same operation to the live in-memory PMC, persist through `SaveServer.SaveProfileAsync`, append a redacted audit record, and retain deterministic rollback material. A failed save restores the clone-derived snapshot before returning an error. No installed profile file is opened directly.

`CompleteQuests` is dangerous. It is excluded from the default Developer Profile preset and requires `COMPLETE_ALL_QUESTS`. Audit data may contain a safe alias and backup identifier; it must never contain credentials or a full profile.
