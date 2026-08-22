# Invalidation Keys

| Key | Trigger | Reopen |
|---|---|---|
| IK-01 | `development` HEAD materially changes MAF/workflow/API/persistence surfaces | SB00 and affected downstream |
| IK-02 | Official 1.18 stable/preview coordinates differ at restore time | SB01 onward |
| IK-03 | Direct `ToolApprovalAgent` usage is discovered | SB02 |
| IK-04 | `UseProvidedChatClientAsIs = true` or a custom `FunctionInvokingChatClient` bypass is discovered | SB02 |
| IK-05 | Any application path enables `AllowConcurrentInvocation = true` | SB02 and SB06 |
| IK-06 | Existing mixed declaration-only/executable tool behavior depends on the new experiment | SB02 scope decision |
| IK-07 | MAF checkpoint/HITL 1.18 signatures differ from source evidence | SB03 onward |
| IK-08 | Compiler cannot preserve deterministic executor/port identities | SB03 onward |
| IK-09 | Checkpoint JSON cannot serialize current custom workflow message/state types | SB03/SB04 |
| IK-10 | Existing persistence convention prohibits the proposed payload/operation records | SB04 onward |
| IK-11 | Multi-host execution permits two hosts to resume the same run | SB04 onward |
| IK-12 | Side-effecting executor cannot accept a stable idempotency/dedup key | SB04 closure |
| IK-13 | API authorization is only endpoint-level or actor scope cannot be resolved | SB05 closure |
| IK-14 | Existing clients require the legacy double-encoded response DTO | SB05 compatibility decision |
| IK-15 | Broad build baseline is red before edits | SB00, broad-gate exception |
| IK-16 | Focused test filter discovers zero tests | Active subbundle |
| IK-17 | A completed subbundle's proof is contradicted by later behavior | Owning prerequisite and dependents |
| IK-18 | UI changes become necessary to expose operation status | Create explicit UI follow-up unless user expands scope |
| IK-19 | Live pending-request projection cannot safely render HumanInput intent/schema | SB07 and bounded SB05 projection claim |
| IK-20 | Sample requires browser-visible bearer credentials or direct cross-origin EventSource | SB07 architecture |
| IK-21 | SimWiki executor bypasses approval or private-network policy | SB07 workflow definition |
| IK-22 | SSE handling uses polling fallback or treats every attention signal as HumanInput | SB07 client/runtime |
| IK-23 | Playwright cannot distinguish direct-hit, retry-hit, and terminal miss | SB07 workflow/sample proof |
