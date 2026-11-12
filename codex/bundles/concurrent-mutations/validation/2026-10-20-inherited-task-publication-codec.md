# Original inherited task-plan publication codec

Status: implemented and validated; signing of the preceding parent-continuation checkpoint
is still pending in local Pinentry. This slice is not staged while that Git command runs.
Backend remains on `concurrent-mutations`; SPA remains clean on `codex/concurrent-mutations`
without merge or push. This is a pure publication-data boundary, not producer activation.

The [design](../plan/P07-inherited-task-publication-codec.md) preserves the canonical parent
material and stores the child caller's finite write context separately. Workbench owns
explicit execution/content contract identities. The inherited target requires its original
governed session, exact Project lifetime, Process run/root/step/claim, selected child and
material, original source authority, executor ceiling, policy and grant. It never fabricates
interactive governance or adopts a new context on resume. Shared task material contains no
child-specific caller or epoch.

Interactive and inherited target decoders remain distinct. Common pure methods retain
exact preparation/restoration, original basis and approved proposal selection, semantic
fingerprints and receipt/result validation. Inherited commands use Process-Agent source
kind 2 and active-work version 2. Typed source data grants no current dispatch authority.
No provider, producer, manager or historical route is enabled by this change.

The background session wire contains only its required execution, executor, profile and
source. The existing Workbench Process-step converter is extracted unchanged for both
definition and task codecs. There is no change to shared Process identifiers or existing
definition JSON. Material byte limits are checked before allocating the UTF-8 payload.
No SQL query, new lock, registration, module dependency or runtime partial is introduced.

## Validation

All **124 selected cases pass on one final frozen candidate**: 64 new cases and 60 existing
cases. Direct standard Release builds pass: Workbench 21.1760323 seconds, Unit 56.3004855
and Integration 46.6055595. Built discovery matches execution exactly.

| Lane | Discovered | Passed | Failed | Seconds |
| --- | ---: | ---: | ---: | ---: |
| Inherited codec, task operation/interactive codecs and definition proposal codec | 110 | 110 | 0 | 3.8639275 |
| Actual interactive saved publication plus registered Process definition receipt replay | 14 | 14 | 0 | 133.0698621 |

The Unit filter is `FullyQualifiedName~ProjectTaskPlanProcessCodecTests|FullyQualifiedName~ProjectTaskPlanOperationContractTests|FullyQualifiedName~ProjectTaskPlanAgentCodecTests|FullyQualifiedName~ProjectDefinitionToolProposalCodecTests`.
The Integration filter is `FullyQualifiedName~ProjectTaskPlanAgentIntegrationTests|FullyQualifiedName~Registered_process_definition_tool_replays_original_receipt_after_dispatch_expiry_but_cannot_dispatch_a_fresh_effect`.

Proof covers run/step material, exact approved restoration, separate children using the
same original bytes, caller and operation partitioning, source/session/Project/claim/
executor/policy/grant/context substitutions, replaced or oversized material, original
receipt semantics and definite result classification. Fixed Process step JSON bytes remain
identical under both codecs; missing/empty/unknown identity data is refused. Existing
actual interactive publication and Process definition replay after dispatch expiry pass.
Pure inherited tests do not claim actual inherited Process publication or permission proof.

Two earlier candidates each passed 89 of 96 Unit cases and failed seven positive round
trips; the Integration lane did not run in either attempt. First, the background session
omitted chat/authority fields required by strict constructor decoding. The explicit
inherited-session wire fixed that. Second, default decoding of `ProcessStepInstanceId`
changed its value; canonical comparison correctly refused it. Reusing the existing
Workbench converter fixed the second issue. Both failed proofs are retained, and the
entire expanded 124-case selection was repeated after the production corrections.

Full portability enforcement passes with **15,647 reviewed findings**. One added case-policy
finding is intentional: allowed Process operation names follow their existing ordinal
case-insensitive protocol. The initial failure, reviewed refresh and final enforcement
without `--write-baseline` are retained. Baseline/secret-tool tests, rendering, documentation
and diff checks pass. Historical broad Stable failures remain open.

## Architecture and resource evidence

Architecture gate: Pass with follow-up. Scoped snapshots `snap-20261020072108-9f0bb7eb`
and `snap-20261020080757-9f0bb7eb` cover Workbench and Migrations. Types change from 1196
to 1204, members from 10137 to 10186, registrations remain 106 and findings change from
459 to 460. The new informational finding is the cohesive inherited codec's 11 source
members. Existing codec/result size findings change; the definition codec becomes smaller
after converter extraction. No project cycle is added. One existing module cycle, five
existing type cycles and 204 existing diagnostics remain unchanged. CodeAnalytics still
reports blocking diagnostics; direct Release builds provide scoped compilation evidence.

All three roots `c4-inherited-task-publication-codec`, `...-final` and `...-verified` under
`.artifacts/concurrent-mutations/validation` are Released. Ownership was recorded before
creation. They reused standard outputs, the isolated PostgreSQL 18 fixture on 53619 and
the existing resource-family inventory. All three freezes verified unchanged after their
tests; observers cover the entire executed commands. Final observer duration is
155.0741557 seconds. Every root has zero remaining observed processes and zero database-name
delta. Two, zero and six respective unowned TEMP deltas are preserved. The shared fixture
remains required; ordinary 5032, installed tooling and historical cleanup denials remain
unchanged. This does not close historical cleanup or sustained-capacity qualification.

Final frozen identities: 9615 source files, manifest SHA-256
`7082DD1373AC5EADC21BBD2DB9F9609A5266403906AA95FD1B67F2674CA6BCD7`;
2850 runtime files, manifest SHA-256
`694982C1A2B687D49A148874835EDA030CDBA3A258A36B9CB8C68DF373471C45`.

Retained UTF-8/LF proofs under `proof/2026-10-19-planning-authority`:

| File | SHA-256 |
| --- | --- |
| `c4-inherited-task-publication-codec.json` | `FAF73F60B16FDBFE0AF06BCFCF2FBCB37D11C6608485124DD0014BAC5ECAEE82` |
| `c4-inherited-task-publication-codec-final.json` | `7032A64B3583C1B43ADA95EB7ADBC5997F277D641469E54979B8CD9B968D8C10` |
| `c4-inherited-task-publication-codec-verified.json` | `DDCED8C18540201BE1AD09A867D346E7412063682A2F2A701A612334B8DA61D9` |

They retain commands, results and failures, architecture review, frozen identities,
portability review, observer coverage and resource reconciliation. Exact proof secret
review passes for all three files plus the private PostgreSQL sentinel without disclosure.
Next: current original-source/executor checks from one selected catalog snapshot, enlisted
inherited publication, original-source history, actual producer/UOW outcome accounting,
workspace approval, manager, Workflow, settings and capacity.
