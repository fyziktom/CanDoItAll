# Final Core Readiness Decision Template

Filled during `SB036` final closure.

## Decision

- [x] Ready for narrow Process Core proposal next.
- [ ] Defer Core; blockers remain.

## If ready, list exact first extraction candidates

Candidate | Why pure | Forbidden dependencies absent? | Tests
---|---|---|---
Route stage order and route eligibility descriptors | Deterministic route-stage descriptors and eligibility rules are source-payload-free after this bundle. | Yes for the proposed cutline: no EF, workspace/storage/filesystem, claim lifecycle, AgentFramework execution, finalizer application, or driver runtime dependency may enter Core. | `bundle://proof/SB006/manifest.md`, `bundle://proof/SB027/manifest.md`, `bundle://proof/SB034/manifest.md`
Subprocess artifact source mapping and lifecycle status rules | Pure mapping/status facts can be proposed without child-run observation, projection persistence, gap journals, or parent finalizer calls. | Yes for pure resolver/status facts only; orchestration remains module-local. | `bundle://proof/SB018/manifest.md`, `bundle://proof/SB027/manifest.md`, `bundle://proof/SB034/manifest.md`
Artifact expectation snapshots and pure matching rules | Snapshot and matcher behavior is deterministic; projection writes, validation orchestration, storage, and workspace IO remain out of Core. | Yes for snapshots/matchers only; recovery lineage persistence and provider-native imports remain module-local. | `bundle://proof/SB024/manifest.md`, `bundle://proof/SB034/manifest.md`

## If deferred, list exact blockers

Blocker | File(s) | Required next action
---|---|---
Broad extraction still blocked by EF/database, workspace/storage/filesystem, AgentFramework execution, claims/transitions, and finalizer coupling. | `repo://codex/bundles/process-core-pre-extraction-consolidation-driver-readiness-v1/reviews/02-final-red-team-review.md` | Move only one pure family at a time in a future proposal; keep side-effectful orchestration module-local.

## Must remain outside Core

- EF-backed hydration.
- Claim lifecycle.
- Workspace/storage/filesystem.
- AgentFramework execution.
- Finalizer application.
- Process state mutation.
- Driver API/runtime dispatch.

## Driver readiness decision

- [ ] Keep driver readiness documentation-only.
- [x] Prepare a future driver-contract proposal, but do not implement production APIs yet.

Driver decision notes:

- Current bundle introduced no production driver API, registry, DI registration, runtime selector, manager command, or execution-capable helper implementation.
- A future driver-contract proposal must stay proposal-only until explicit approval; it must not be bundled into the first Core cutline.
