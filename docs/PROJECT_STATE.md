# Project state

Foundation milestone: implemented and validated on 2026-08-20.

- Compatibility: exact SPT 4.1.2, checked by metadata, server binary reference, and startup policy.
- Runtime changes: none to SPT databases or profiles.
- Functional pages: Overview, Items, Profile, Settings.
- Deferred pages: Unlocks, Traders, Quests, Skills, Hideout, Raids, Backups.
- Item source: immutable bounded index built once from `TemplateTable.Items` after database load.
- Profile source: supported in-memory `ProfileHelper.GetPmcProfile(sessionId)` projection.
- Backups: interface and atomic synthetic-file implementation, test-only in this milestone.
- Writes: no HTTP write endpoint and no startup profile mutation.
- Validation: 26/26 Release tests passed; build completed with 0 warnings and 0 errors.
