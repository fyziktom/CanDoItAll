# External Agent actual-target preconditions

The [design](../plan/P05-external-agent-preconditions.md) is implemented. **54/54 focused
cases passed**. External update/archive now compare actual Agent identity, revision and
configuration in the original workspace context. Mutable editor fields are captured
before awaiting storage. Catalog-only publication excludes execution journals. Historical
fingerprints/receipts remain replayable; no-op updates preserve the Agent revision.
CRM refresh carries original workspace identity and retains committed receipts on failure.
See [operator guidance](../../../../docs/agent-external-provisioning.md).

This does not close the E2E bundle, full writer perimeter, Process/Workflow activation,
retention, profile-activation fencing, namespace restoration or capacity qualification.

## Validation

Standard Release builds passed for Models, Core, CRM/HR, AgentFramework module, Web,
Unit and Integration projects, using local sibling libraries. Existing NuGet advisory
warnings remain. Discovery confirmed 36 Unit and 18 Integration cases. All passed with
no skips, covering nested input capture, ABA and current target/context preconditions,
legacy fingerprint/receipt compatibility, no-op revision, generation changes around
reads/callback/publication, definite refusal versus uncertain cleanup, pinned projection
and receipt retention, real HTTP ordinary/policy conflicts and explicit replacement,
parallel replay/archive, real file policy preservation/publication races/deletion recovery,
and catalog-only versus execution-journal publication dependencies.

No UI changed; no new browser proof is claimed. The CRM change is an existing bridge
overload with no projection-store implementation change. The selected tests cover its
behavior and actual API composition.

CodeAnalytics before `snap-20261019133951-d3c48a03`, after
`snap-20261019135410-d3c48a03`: four projects, 1,982 types unchanged, 16,667 → 16,671
members, 75 DI registrations unchanged, six existing cycles and 221 diagnostics
unchanged. Findings decreased 656 → 655 with no new finding identity. Existing blocking
dependency/DI diagnostics remain; this is delta review, not a clean architecture claim.
No new project edge, type, DI service, registry or production partial was introduced.

Portability scanned all candidate inputs, including untracked files. Final enforcement
without baseline writing passed: **15,611 reviewed executable-source findings unchanged**.
Rendering register (1,209 inputs), documentation and diff checks passed. No baseline
refresh was needed.

## Preparation, identity and resources

The first `c4-external-agent-preconditions` freeze stopped before tests because a copied
helper retained old component/browser command names after a CRLF replacement mismatch.
No source/runtime changed in the repair. Its observer ended; reconciliation found zero
observed processes/database delta and preserved one unattributed TEMP delta. This was
a preparation failure, not a test run. Retained proof SHA-256:
`8C3FB0A34B28D094E26B6F2616618D30C5541AFE4753554594269491F1D3CE5D`.

Successor `c4-external-agent-preconditions-ready` corrected that list and explicitly
reused completed builds, discovery and static commands. Frozen inventories:

- 9,382 source inputs: `6777AA7F7DA669A7D2F804C31E71F1C1EAEECD99DFD5C37595EE65A54CC5BD5C`.
- 4,340 runtime inputs: `D9144E1698F5E1166C780C01E6B53AB68F5F4B41B6A287378300B49AE3520311`.
- Retained 54-case proof: `9B76E0509D79EC6465E80EE97A64F6A69A2D4ABC25E442017E8C54B761D1B17F`.

Complete source/runtime identity was verified unchanged after execution. The observer
covered both entire commands. Final reconciliation found zero remaining observed
processes and zero new database delta; six unattributed TEMP deltas were preserved.
The existing PostgreSQL 18 fixture was reused, with no replacement Docker stack.
The freeze was released after proof retention. Later documentation/commit metadata
are outside the executed identity. Raw proof follows the existing ignored local
`proof/2026-10-19-planning-authority` convention.

Earlier automatically rejected cleanup remains blocked as recorded in
[resources](../resources.md). Zero new deltas do not close baseline residue. SPA stays
clean on `codex/concurrent-mutations` in `C:/repositories/CanDoItAll.SPA-concurrent-mutations`;
the separate main checkout was untouched. Nothing was merged or pushed.
