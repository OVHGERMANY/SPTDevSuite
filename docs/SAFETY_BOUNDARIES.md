# Safety boundaries

- Bindings: SPT's existing server only; no new socket, listener, CORS policy, or outbound connection.
- Access: `IPAddress.IsLoopback` on the immediate remote endpoint. Forwarding headers are ignored.
- Authentication: random 256-bit process-lifetime token in an HttpOnly strict cookie. API calls without the exact token are rejected.
- CSRF: state-changing requests must also supply the exact anti-CSRF header matching the strict cookie.
- Compatibility: incompatible startup leaves the catalog uninitialized and dashboard API unavailable. Every state-changing HTTP method and the unlock service entry point independently fail closed before profile access. No database or profile mutation occurs.
- Profiles: no startup mutation and no direct profile-file access. Apply requests operate only on the current supported in-memory session after preview, clone validation, rollback persistence, and exact confirmation; credentials and full profiles are excluded from audit records.
- Validation: tests use synthetic values and temporary directories. Installed `user\profiles` and `SPT_Data` are forbidden test targets.
- Packaging: only SPTDevSuite-owned assemblies are copied into `Build\SPTDevSuite`.
