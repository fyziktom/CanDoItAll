# Architecture Checkpoints

| Gate | Required decision and proof | Reopen impact |
|---|---|---|
| Preparation | Current inventory, exact references, dependency direction, pattern choices, testability and findings are reviewed. | Repair bundle before execution if a proposed responsibility has no owner. |
| SB00 | MAF 1.20, A2A preview, MEAI 10.9 and Microsoft.Extensions 10.0.11 resolve coherently; SDK changes remain in adapters; malformed binding still demonstrates later repair need. | Reopen every downstream assumption affected by schema/result/session/workflow changes. |
| SB01 | Actual adapter/contract changes stay SDK-neutral outside Maf; normal authorization still precedes execution; safe diagnostics and Unknown behavior proven. | Reopen SB02–SB06 on contract or validation-policy changes. |
| SB02 | Both completion branches use one assessment; durable/API mapping and legacy compatibility proven. | Reopen SB03/SB05/SB06 on outcome/recovery/receipt changes. |
| SB03 | Canonical projection scoped with current authority; no raw session restoration or credential propagation. | Reopen SB06 and SB04 continuation checks if session inputs change. |
| SB04 | Same application policy above both transports; real source relay and SDK protocol contracts tested. | Reopen affected SB01/SB03 tests if SDK mappings or message shape changes. |
| SB05 | Managed storage retains ownership; telemetry/commit ordering explicit; context effects remain scoped. | Reopen SB02/SB03 and SB06 if new effect semantics are needed. |
| SB06 | Final dependency comparison, production/test caller evidence and UI review match the implemented source checkpoint. | Reopen the earliest weak phase and every invalidated downstream proof. |

No new project, broad interface hierarchy, generic manager or partial-file split is pre-approved by this bundle. New abstractions need an actual boundary and callers. Requery Components MCP before any markup change; unavailable metadata is an execution prerequisite, not a reason to invent component APIs.
