# Durable inherited planning review owner

Status: qualified owner checkpoint; workspace and Process integration remain next.

The prerequisite is signed commit `ef754ab00`, with 56 passing guarded-publication
cases. This implements the Agent owner portion of the
[current manager design](../plan/P07-current-manager-actions.md).

Core owns the exact approval intent and the approve/reject transition. The intent
digest binds the original action, choice, displayed expectation, proposal, parent,
execution metadata, original target and the owning module's trusted actor/Process digest. No new
table or dependency is introduced. A retained matching approval is required for
replay; terminal run state alone cannot authorize it. Replay never dispatches again,
including when approval committed but live work remains open after a crash.

Manager authority is separate from original parent authority and current disclosure.
The guarded file writer checks it after selected publication-key waits. The Project
publisher checks it after the Process transaction fence, both before staging and
after SQL flush. An unavailable observation cannot authorize publication. Existing
manual manager publication fixtures now supply this explicit capability.

The original interactive publication acknowledgement/classification/settlement and
cancellation transitions move into small shared owners. The journal loses the pure
cancellation body; background cancellation preserves a null chat. Closed actions
also recheck current read authority after waiting for the dispatch lease.

Current manager authority is not injected into internal outcome accounting reads.
An accepted effect can settle even if the manager loses permission before the
response. Returning or replaying its data still requires current disclosure.
The caller's cancellation is not reused for the existing bounded settlement window.
The lifecycle callback runs after releasing the Agent dispatch lease and before
final disclosure, with its own 20-second cancellation budget. This gives the Process
owner a real accounting boundary without adding a Process dependency to Core.
The Core convenience method uses the same lifecycle with an identity result.

Validation covers both snapshot modes and choices; fresh owner replay; altered
actor/action/expectation; competing choices; revoked permission during either lock
wait; missing manager capability; durable-write acknowledgement loss; open-work
replay; and revocation after the effect. Actual PostgreSQL cases prove rollback
before staging and after flush. Existing interactive, inherited saved-effect and
waiting-result suites remain required regressions.

The candidate is `.artifacts/concurrent-mutations/validation/c4-manager-review-owner`.
It reuses standard Release outputs and the existing isolated PostgreSQL fixture.
Ownership, discovery, frozen source/runtime identity, resource observation,
architecture comparison and final portability enforcement are required.
The selection contains 147 cases in ten lanes, with the 26 new owner cases first.

All 147 cases passed. Standard Release builds passed for Models, Core, Workbench,
Unit and Integration. The final test-only cleanup correction was rebuilt before
discovery and execution. Full portability enforcement passed with 15,663 unchanged
reviewed findings; no baseline update was needed. Validation-tool, secret-scanner,
documentation rendering and whitespace checks also passed.

All 9,864 source inputs and 3,516 runtime files remained unchanged throughout
execution. The source manifest SHA-256 is
`C65CE255D1769BBBB23964D1DC96C3B5EBC0D18C2226EF6A568A3A36957C2465`;
the runtime manifest SHA-256 is
`4A4A7AA35B19FB73A186BD60D5C005A48E11DCAD8A62A0E2DCAACD24A9A11B47`.
The observer covered all ten commands and closed successfully after 1,610 seconds.
Final reconciliation found zero surviving observed processes, database-name changes
or retained temporary entries. Earlier denied resources remain baseline-only.
These are functional results, not sustained capacity measurements.

Local proof is retained at
`proof/2026-10-19-planning-authority/c4-manager-review-owner.json`:
267,684 bytes, SHA-256
`3C4C702485527641588311991FBD41A45E41F4557308FE5B23272837246C4677`,
with zero secret findings.

Core/Persistence snapshots `snap-20261021133932-803d411d` and
`snap-20261021141712-803d411d` retain the same project references and four existing
cycles. There are five new cohesive types and thirteen internal dependency edges.
The one added informational finding concerns the nine-member review-intent validator;
its construction and matching checks belong together. The journal loses a member
and the interactive coordinator delegates its existing completion behavior.
All 189 collector diagnostics are pre-existing; direct builds pass.

This does not yet expose the manager operation through the workspace facade or HTTP,
bind its trusted Process/actor digest at the Web entry, activate the Process executor,
or implement parent refresh/cancel, Workflow/settings/SPA or sustained capacity.
