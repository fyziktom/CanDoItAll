# Architecture and QA review log

These reviews are distinct perspectives used during this preparation, not an independent reviewer certification. Application scenarios remain NOT_RUN. The package-only machine validation report is separate.

## Retained v1.0 review findings — English record

| Finding | Original correction retained in v1.1 |
|---|---|
| R1-A01: declaration, caller, implementation, and data authority were conflated | Separate contract fields; consumer ports do not own their sources. |
| R1-A02: process authority incorrectly covered every run's results | Separate CON-031, CON-054, and CON-055. |
| R1-A03: domain context capture leaked into neutral conversation UI | CON-044 belongs to the Agents application adapter. |
| R1-A04: project lifecycle and database transfer were combined | Separate CON-046 and CON-056 with different coordinators. |
| R1-A05: note body risked being interpreted as a mandatory new column | Native note content versus file-node description is semantic; no forced schema change. |
| R1-A06: assignment meanings were mixed | Classify roles, rates, intervals, and audit; explicit reconciliation rather than heuristic ownership. |
| R1-Q01: evidence was confused with runtime proof | All application scenarios remain NOT_RUN. |
| R1-Q02: module-based feature candidates could imply complete coverage | Explicitly discovery candidates, requiring exact scoped tests. |
| R1-Q03: Scheduler and Simple Chats hooks had uncertain current support | Preserve evidence limits; neither autoimplement nor delete based on old labels. |
| R1-Q04: inherited entity labels could be mistaken for EF table inventory | Actual mappings/writers need discovery. |
| R1-Q05: generic READMEs were insufficient for exhaustive audit claims | Retain bounded read scope and unknowns. |
| R2-A01: neutral UI still appeared as direct domain caller | Removed such caller entries; package validator enforces the constraint. |
| R2-A02: logical/infrastructure areas needed complete cards | Retain all nine alongside 15 product modules. |
| R2-A03: reentrant command and lock cycles were underspecified | Explicit coordination/lock-order and QA-120. |
| R2-Q01: receipt retention/restore could repeat external effects | Preserve provenance and explicit expired/unknown reconciliation; QA-121. |
| R2-Q02: QA-120 contract references were inconsistent | Correct assignment/reservation mapping retained. |
| R2-Q03: inventory source was described more broadly than read | SRC-001 is directory inventory, not a complete README/source audit. |

## v1.1 review pass A — domain and security architecture

| Finding | Correction |
|---|---|
| A1: contribution protocol was detailed only for Structure | Chapters 14-17, contracts CON-057..076, and complete owner-operation matrix; direct destination owners, no surrogate Structure gateway. |
| A2: HR CRM creation could be wrongly treated as missing | SRC-029 and FEAT-111/112 distinguish existing party/affiliation tools from broader future CRM administration. |
| A3: Simple Chats administration could accidentally add agent execution | External definition-only adapter, existing owner API reuse, no product/persistence tooling references, no transcript grants; QA-129..136. |
| A4: interactive managed authority could leak into background runs | Preserve HR/Scheduler InteractiveChat gates; distinct delegated executors/steps and supported-purpose evidence. |
| A5: “safe read” missed downstream disclosure | Separate source access, provider disclosure, and destination publication; current validation and minimal checkpoints. |
| A6: receipt after owner call left a crash window | Explicit owner-atomic effect/receipt or proven shared transaction; current Simple Chats request limitation recorded. |
| A7: workflow/process restart could regenerate effect identities | Persist step/iteration/action identity before dispatch; recover from owner receipt before checkpoint replay. |
| A8: target module names in extension_implementers could require forbidden reverse references | CON-072/073 now identify consumer-associated integration adapters; destination owners are separately listed, and Simple Chats never implements runtime ports inside product/persistence. |

## v1.1 review pass B — QA, evidence, and translation

| Finding | Correction |
|---|---|
| B1: new branch observation could silently refresh old evidence | Preserve original pins; targeted SRC-028..037 records state exactly what was read and what was truncated. |
| B2: API/tool/template presence was conflated | Runtime surface catalog distinguishes observed, documented, required, optional, and unsupported. Scheduler workflow-only and absent general Process provider remain explicit. |
| B3: broad CON-011 could imply every HR field is editable | Narrow query/create/affiliation/staffing contracts plus allowlists and negative tests. |
| B4: broad matrix could imply automatic grants | All matrix rows deny automatic grants and distinguish supported surfaces from planned capabilities. |
| B5: generic language QA was incorrectly linked to database-transfer CON-056 | Removed unrelated contract reference; language inspection is package/downstream scope only. |
| B6: untranslated explanatory strings remained in contract direction notes | Translated all such notes; regenerated readable mirrors from English JSON; no old archive embedded. |
| B7: old START_HERE explicitly requested Czech documentation | Replaced by English policy covering bundles, instructions, code comments, and reports; translated both original and revision inputs. |
| B8: new scenarios needed exact contract linkage rather than module-only coverage | Added explicit contract-to-scenario plan alongside clearly labeled broad feature candidates. |
| B9: compensation might delete existing or human-edited records | Explicit per-owner revision-checked compensation and QA-146. |
| B10: validator treated an optional null parent as an unknown module | Allow null only for parent_module_id; keep all required reference checks strict and rerun the full package validation. |

## Recheck scope

Rechecked contract ownership/adapter placement, all module operation coverage, Simple Chats forbidden dependencies and management limits, actor/purpose distinctions, current-versus-target evidence, read/write/disclosure separation, atomic receipts, checkpoint recovery, required effects, and preservation of original Structure/product journeys.

Mechanical validation checks JSON identities and references, original-record retention, every original file's replacement, module-card/matrix coverage, scenario/proof mappings, English-language remnants, Markdown links, neutral-UI caller constraints, manifest counts, and complete SHA-256 coverage. Final extracted ZIP is checked with the same validator and ZIP CRC validation. The structural report is recorded in package-validation.json. Checksum verification and archive round-trip checks are additionally performed at delivery; the report is not rewritten after its own checksum is generated. None of these checks constitutes application behavior proof.

No known unaddressed internal contradiction was intentionally accepted at packaging. Real implementation gaps remain explicitly listed in gaps.json, including missing HR Simple Chats adapter, receipt guarantees, broader tool coverage, and unexecuted runtime proof. These are not hidden claims of completion.
