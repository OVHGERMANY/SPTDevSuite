# SPTDevSuite operating rules

- Target only official SPT 4.1.3 and .NET 10. Fail closed for every other server version.
- Keep the installed SPT runtime read-only during development and validation.
- Never read or write installed profile files directly. Runtime profile reads use supported in-memory SPT services.
- Do not add profile mutation routes without a backup, validation, dry-run, audit, and rollback design approved in the active execution plan.
- Keep `/devsuite` loopback-only. Do not add CORS, external listeners, remote calls, URL tokens, or repository secrets.
- Build Release with warnings as errors. Package only SPTDevSuite-owned assemblies under `Build/SPTDevSuite`.
- Tests use synthetic templates and profiles in temporary directories. Any test that reaches `E:\Games\SPT\SPT_Runtime\user\profiles` is a defect.
- Human-readable logs must be terse and factual; a light `voilà` is acceptable, but never soften a failure.
