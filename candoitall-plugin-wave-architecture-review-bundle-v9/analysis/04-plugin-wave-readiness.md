## Plugin-wave readiness
**Verdict: GO with guarded rollout**

The next large plugin wave can now proceed because the phase9 blockers were closed in code and revalidated in a real .NET environment:

1. The core node no longer persists binding/media/external-artifact carrier concerns.
2. Runtime binding composition no longer depends on legacy node-carrier fallback.
3. Marker truth is single-source through `MarkersJson`.
4. Provider/resource plugin editors are manifest-driven through shared connector-config state.
5. Custom plugin save flows keep plugin key authoritative and stop synthesizing fake legacy enum identity.
6. Reference semantics are open-world string rows with typed convenience helpers only at the edges.
7. Structure load paths are read-only and compatibility normalization is not persisted during reads.
8. A durable connector-command boundary now exists for retry/idempotency/replay/approval/audit before future write-side plugins land.

Guarded-rollout caveats remain:
- `CrmHrServices.cs` and `ProjectWorkbenchModels.cs` still trip advisory hotspot thresholds and should stay on the architecture cleanup backlog.
- Full-repo Playwright coverage was not used as the closure gate; the targeted phase9 proof set was used instead.
- Existing unrelated `NU1510` and `xUnit2031` warnings remain outside this bundle’s write scope.
