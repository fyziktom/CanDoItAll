# Risk register

| ID | Risk | Severity | Mitigation | Blocking proof |
|---|---|---:|---|---|
| R01 | UI observation is treated as authority | Critical | independent authority resolver and typed snapshot | forged/unauthorized scope test |
| R02 | mixed workspace scopes across file/MCP/tool services | Critical | one scope-bound services bundle | identity consistency tests |
| R03 | an approval is retargeted after navigation | Critical | original turn lease and authority reference | Project X approval while viewing Y |
| R04 | process recovery remains in MAF through a renamed helper | High | generic runtime evidence and Processes-owned policy | source assertion and direct policy tests |
| R05 | broad runtime survives behind multiple wrappers | High | caller inventory, no-new-caller assertion, facade deletion | old-class shrink proof |
| R06 | dependency cycle appears during contract extraction | High | move smallest SDK-free contract first | dependency graph checkpoint |
| R07 | Gantt projection is mistaken for canonical task state | High | label as observation/projection; tools read canonical state | mutation path tests |
| R08 | view changes trigger excessive publications/render loops | Medium | structural equality, debouncing only where safe, bounded contributor data | component responsiveness tests |
| R09 | conversation binding is committed before execution admission | Medium | commit binding only after authority resolution/admission | failed-admission binding test |
| R10 | stale inactive pinned context is reused | High | no pin until canonical hydrator exists | source assertion / mode validation |
| R11 | restart loses opaque transient lease | Medium | explicit continuation policy and clear fail-closed state | restart/unavailable test |
| R12 | metadata v1/v2 migration changes old runs | High | backward reader, dual-read period, immutable old run semantics | old fixture round-trip |
| R13 | context fragments retain operational policy prose | Medium | move guidance to trusted module context contributor | source assertion |
| R14 | workflow payload can elevate scope | Critical | explicit trusted invocation scope; no payload parsing | malicious payload test |
| R15 | MAF-specific state leaks into contracts | High | SDK-free architecture tests | assembly/package reference assertions |
| R16 | global tool collision remains first-wins | High | fail-fast global validation | duplicate tool negative test |
| R17 | full solution refactor becomes one unreviewable PR | High | phase and checkpoint discipline | proof manifest per subbundle |

## Revision 2 additional risks

| ID | Risk | Severity | Mitigation | Blocking proof |
|---|---|---:|---|---|
| R18 | two side-effecting paths run during cutover | Critical | one selector; shadow pure mapping only | single-path guard and telemetry |
| R19 | runtime port split loses usage/finalizer/tool/session evidence | Critical | differential fixtures and fault tests | SB11 parity matrix |
| R20 | DI cleanup changes lifetime/decorator/disposal behavior | High | composition owner and disposal characterization | scope/lifecycle tests |
| R21 | legacy waiting approvals become unresumable | Critical | envelope legacy reader and persisted fixtures | restart/continuation matrix |
| R22 | boolean approval decides a changed pending set | Critical | expected pending-set fingerprint/revision | concurrency negative test |
| R23 | process recovery moves but bypasses ordinary gates | Critical | one completion coordinator path | exact-once gate/receipt integration |
| R24 | lightweight port duplicates provider credential/retry/usage code | High | build above provider runtime/driver | source/dependency and lifecycle tests |
| R25 | ordinary chat becomes a disabled agent | High | separate conversation application contract | no-agent architecture guard |
| R26 | model/session switch loses implementation state | High | durable Claude handoff files | handoff validation at closure |
| R27 | regression patched at symptom layer restores forbidden coupling | Critical | owner-stage bugfix loop and architecture gate | regression test + guard |
| R28 | public APIs expose envelope/authority/private context | Critical | explicit public projections | API serialization tests |
| R29 | provider usage or blank-response retry is counted twice across driver, lightweight adapter, and workflow projection | High | one terminal usage source and additive observations | fake-driver and provider protocol accounting tests |
| R30 | lightweight streaming emits duplicate/missing terminal state or loses cancellation | High | stable sequence contract and one terminal disposition | streaming fault/cancellation tests |
| R31 | mock, scenario, diagnostic, or API test host keeps a divergent manual runtime graph | High | migrate every caller family and run production-composition smoke | caller scan and host parity tests |
| R32 | source guards are updated to new names but no longer prove the architectural invariant | High | behavior/dependency guards plus negative fixtures that make each guard fail | guard self-tests / seeded violation proof |
| R33 | provider runtime pool/handle lifetime changes under lightweight calls | High | reuse owned pool/handle lifecycle and characterize disposal/concurrency | handle reuse, cancellation, and disposal tests |
