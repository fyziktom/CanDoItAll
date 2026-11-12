# Original inherited task-plan publication data

Status: implemented and [validated](../validation/2026-10-20-inherited-task-publication-codec.md)
with 124 passing selected cases after the 75-case receipt identity proof. The preceding
Core/MAF signed checkpoint is still waiting for local Pinentry; do not replace or bypass
that signing operation. This slice prepares the Workbench adapter without activating it.

Keep interactive target decoding strict. Add a separate inherited target envelope holding
the original admitted session, exact Project lifetime, complete Process dispatch source,
original executor tool/task-write ceiling and child-specific finite write context. The canonical
child/material binding already belongs to `InheritedOrigin.ParentBinding`; decode it there
instead of introducing another independently mutable copy. Original source and executor
ceilings are distinct because the launching Agent can differ from the executing Agent.
Preserve `ProjectStructureAccessState` governed semantics: the Process source grants its
exact Project scope; the executor contributes its tool and task-write permissions. Ordinary
`AllowAllProjects` and personal Project lists neither widen nor replace that Process scope.
Capture only those relevant executor permission booleans in a small typed ceiling, not
the executor's unrelated mutable configuration and Project lists.

Workbench owns a stable Agent execution contract `a8a1bce7-dfbd-40a2-8b5c-6c15f1558790`
version 1 and an independent task-basis content contract
`53294440-a9fa-4964-af95-96605ec88217` version 1. The shared content is exactly the neutral
`ProjectTaskPlanBasisCodec` JSON. A Workflow implementation may later support the same
content through its own execution contract. No execution provider is registered by this
data change. Preserve the original Process material stamp, scope, namespace, capture time
and grant; inherited Agent basis IDs use the enrolled material ID.

Validate exact session/profile/executor, original source fingerprint and step identity,
Process run/root/claim, enrolled child ID equal to Agent execution ID, original Project
admission/readiness and original task-write permissions. Validate original parent policy
and allocation against the canonical binding and the child decision's resolved policy.
Neither current settings nor a caller-provided replacement target may reinterpret it.
The finite write context belongs to the child caller and is retained in its target; it
must not be inserted into the shared material or renewed on resume.

The inherited basis envelope contains the unwrapped shared JSON. Require its explicit
format/version, exact original Agent digest and capture identity, canonical task-basis
encoding and target/profile/namespace. Reconstruct the neutral Process material value
from the original scope/reference/content contract/time/bytes and compare its full stamp.
This independently catches replacement bytes even if a different Agent digest is supplied.
Apply existing provider input and target envelope byte bounds before dispatch.

The first 96-case Unit run exposed a wire mismatch: `AgentToolSessionReference`
intentionally omits default chat/authority properties for background sessions, while
strict constructor decoding requires those parameters. Retain a dedicated background
session wire containing execution, executor, profile and source. Reconstruct only the
governed background session from those required fields. Keep the shared decoder strict
and reject chat/authority fields in this wire. Preserve the failed proof and rerun all
109 selected cases after this production correction.

The second pass then reached canonical source comparison and exposed the existing
`ProcessStepInstanceId` default serializer limitation. Reuse the explicit converter
already owned by the Workbench definition codec, extracting it unchanged into one
shared implementation. Do not modify the shared Process identifier or its external
protocols. Add the definition codec's existing cases and byte-identity compatibility
checks to the repeated candidate.

Reuse existing preparation/restoration, approved step/basis selection, source fingerprints
and result validation. A small typed resolved publication-target value carries the explicit
session, Project, caller, namespace and source kind into common pure codec methods. The
interactive and inherited adapters construct it only after their own strict decoding.
Common code receives an original-basis reader, never a current-capture callback. No generic
origin flag, fabricated governance or second provider claiming the same mutation tool.

Tests use in-memory original decision/source/material records for these pure contracts.
Prove the same shared bytes can serve separate children with distinct write contexts and
publication identities; exact proposal restoration and receipt/result classification;
cross-origin refusal; target/session/profile/executor/Project/source/child/allocation/
policy/namespace/context substitutions; altered material bytes, timestamp, stamp, content
contract and basis ID; strict bounds. Repeat interactive codec and receipt contracts and
actual interactive saved dispatch because their common codec changes. Direct Workbench,
Unit and Integration builds, exact discovery, full portability enforcement and before/after
CodeAnalytics are required. The existing Workbench snapshot
`snap-20261020072108-9f0bb7eb` represents the unchanged production predecessor.

This pure boundary does not prove current authority or historical recovery. The following
adapter must select launch/executor Agents from one held catalog set, acquire Project
gates before the enlisted selected Process fence, and retain original ceilings. Historical
receipts need canonical retained source reads independent of current assignment/dispatch.
Workspace approval, actual producer/UOW outcome accounting, manager, Workflow and settings
activation remain open. Do not lift unsupported execution guards before those proofs.
