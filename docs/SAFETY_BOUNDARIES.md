# Safety boundaries

- Bindings: SPT's existing server only; no new socket, listener, CORS policy, or outbound connection.
- Access: `IPAddress.IsLoopback` on the immediate remote endpoint. Forwarding headers are ignored.
- Authentication: random 256-bit process-lifetime token in an HttpOnly strict cookie. API calls without the exact token are rejected.
- CSRF: future state-changing requests must also supply the exact anti-CSRF header matching the strict cookie.
- Compatibility: incompatible startup leaves the catalog uninitialized and dashboard API unavailable. No database or profile mutation occurs.
- Profiles: no startup reads, no file-path access, no writes, and no credentials in projections or audit records.
- Validation: tests use synthetic values and temporary directories. Installed `user\profiles` and `SPT_Data` are forbidden test targets.
- Packaging: only SPTDevSuite-owned assemblies are copied into `Build\SPTDevSuite`.
