# Runtime Host Deferral

This bundle must not implement:
- driver registry,
- runtime selector,
- DI registration,
- manager command,
- scheduler/workflow hook,
- process driver provider host.

A future host must define:
- permission enforcement,
- capability scopes,
- audit persistence,
- sandbox and command allowlist,
- timeout and output hash policy,
- secret masking,
- lifecycle ownership for any side effects.
