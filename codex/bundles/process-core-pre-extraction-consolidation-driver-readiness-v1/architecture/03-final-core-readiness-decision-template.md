# Final Core Readiness Decision Template

Codex must fill this file during final closure.

## Decision

Choose one:

- [ ] Ready for narrow Process Core proposal next.
- [ ] Defer Core; blockers remain.

## If ready, list exact first extraction candidates

Candidate | Why pure | Forbidden dependencies absent? | Tests
---|---|---|---

## If deferred, list exact blockers

Blocker | File(s) | Required next action
---|---|---

## Must remain outside Core

- EF-backed hydration.
- Claim lifecycle.
- Workspace/storage/filesystem.
- AgentFramework execution.
- Finalizer application.
- Process state mutation.
- Driver API/runtime dispatch.

## Driver readiness decision

Choose one:

- [ ] Keep driver readiness documentation-only.
- [ ] Prepare a future driver-contract proposal, but do not implement production APIs yet.
