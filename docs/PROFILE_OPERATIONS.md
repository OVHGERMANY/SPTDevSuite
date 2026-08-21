# Profile operations

No profile operation is applied in this milestone.

Future write sequence is fixed: resolve one supported in-memory session, create timestamped backup, validate JSON plus SHA-256, compute dry-run, require explicit module request, apply to an isolated clone, validate invariants, replace atomically, persist through supported SPT services, append a redacted audit record, and retain deterministic rollback material.

`CompleteQuests` is dangerous. It is excluded from the default Developer Profile preset and must require a distinct future confirmation. Audit data may contain a safe alias and backup identifier; it must never contain credentials or a full profile.
