# Module operation coverage matrix

Read and command paths for every module/boundary. These are architectural requirements, not default grants; neutral UI and composition are not domain writers.

## MAT-001

**Module id:** MOD-AGENTS

**Read contract ids:** CON-001; CON-063

**Command contract ids:** CON-002; CON-063; CON-065

**Automation callers:** MOD-AGENTS; BND-WORKFLOWS; MOD-PROCESSES

**Observed surface:** Managed HR tools observed; capability curator documented.

**Guardrails:** Technical definitions and protected capability policy remain Agents-owned; no self-escalation.

**Status:** TARGET_COVERAGE_NOT_PERMISSION_MANIFEST

**New automatic grants:** No

**Workflow process rule:** Register and prove a distinct typed executor/step adapter with current run delegation; do not borrow interactive managed identity.

## MAT-002

**Module id:** MOD-PROVIDERS

**Read contract ids:** CON-005; CON-009

**Command contract ids:** CON-006; CON-008

**Automation callers:** MOD-AGENTS; BND-WORKFLOWS; MOD-PROCESSES

**Observed surface:** Provider administration exists in the baseline; arbitrary agent administration is not proven.

**Guardrails:** Selecting an allowed model is not editing tariffs, publishing providers, or reading secrets. Pricing and sharing writes require dedicated administrative grants.

**Status:** TARGET_COVERAGE_NOT_PERMISSION_MANIFEST

**New automatic grants:** No

**Workflow process rule:** Register and prove a distinct typed executor/step adapter with current run delegation; do not borrow interactive managed identity.

## MAT-003

**Module id:** MOD-CRM

**Read contract ids:** CON-010; CON-057; CON-059

**Command contract ids:** CON-058; CON-059; CON-060; CON-012; CON-013; CON-014

**Automation callers:** MOD-AGENTS; BND-WORKFLOWS; MOD-PROCESSES

**Observed surface:** HR safe queries, party create, and affiliation operations observed; broader staffing mutation is an extension.

**Guardrails:** Separate safe contact, confidential HR, staffing, participation, reservations, and technical-agent projections. No CRM AI-price/technical-definition master.

**Status:** TARGET_COVERAGE_NOT_PERMISSION_MANIFEST

**New automatic grants:** No

**Workflow process rule:** Register and prove a distinct typed executor/step adapter with current run delegation; do not borrow interactive managed identity.

## MAT-004

**Module id:** MOD-PROJECTS

**Read contract ids:** CON-015

**Command contract ids:** CON-016; CON-017

**Automation callers:** MOD-AGENTS; BND-WORKFLOWS; MOD-PROCESSES

**Observed surface:** Project/Structure-related paths retained; every new direct tool requires registration discovery.

**Guardrails:** Project lifecycle, phases, and portfolio hierarchy owned here. Project creation/deletion requires explicit scope and recovery.

**Status:** TARGET_COVERAGE_NOT_PERMISSION_MANIFEST

**New automatic grants:** No

**Workflow process rule:** Register and prove a distinct typed executor/step adapter with current run delegation; do not borrow interactive managed identity.

## MAT-005

**Module id:** MOD-STRUCTURE

**Read contract ids:** CON-018

**Command contract ids:** CON-019; CON-020; CON-021; CON-022

**Automation callers:** MOD-AGENTS; BND-WORKFLOWS; MOD-PROCESSES

**Observed surface:** Actual Structure tools/executor paths retained.

**Guardrails:** Native nodes/links are owner-written; foreign source and runtime state remain projected. Required contribution is not elevated permission.

**Status:** TARGET_COVERAGE_NOT_PERMISSION_MANIFEST

**New automatic grants:** No

**Workflow process rule:** Register and prove a distinct typed executor/step adapter with current run delegation; do not borrow interactive managed identity.

## MAT-006

**Module id:** BND-WORK

**Read contract ids:** CON-023; CON-026

**Command contract ids:** CON-023; CON-024; CON-025; CON-026

**Automation callers:** MOD-AGENTS; BND-WORKFLOWS; MOD-PROCESSES

**Observed surface:** Task/Gantt paths user-confirmed and evidenced in baseline.

**Guardrails:** Task creation, assignments, dependencies, and plan baselines use one writer; staffing reservation and price source are separate.

**Status:** TARGET_COVERAGE_NOT_PERMISSION_MANIFEST

**New automatic grants:** No

**Workflow process rule:** Register and prove a distinct typed executor/step adapter with current run delegation; do not borrow interactive managed identity.

## MAT-007

**Module id:** MOD-PROCESSES

**Read contract ids:** CON-028

**Command contract ids:** CON-027; CON-028; CON-068

**Automation callers:** MOD-AGENTS; BND-WORKFLOWS; MOD-PROCESSES

**Observed surface:** UI/HTTP/governed/Structure bridges; no general first-party direct Process provider documented.

**Guardrails:** Definition editing, execution assignment, admission, cancellation, and recovery are different scoped actions.

**Status:** TARGET_COVERAGE_NOT_PERMISSION_MANIFEST

**New automatic grants:** No

**Workflow process rule:** Register and prove a distinct typed executor/step adapter with current run delegation; do not borrow interactive managed identity.

## MAT-008

**Module id:** BND-WORKFLOWS

**Read contract ids:** CON-029; CON-030

**Command contract ids:** CON-030; CON-064

**Automation callers:** MOD-AGENTS; BND-WORKFLOWS; MOD-PROCESSES

**Observed surface:** Workflow execution and curator providers documented; exact methods/purposes require current discovery.

**Guardrails:** Definition revision edits do not rewrite admitted runs/checkpoints. Executors may call other owners only under their own delegated scope.

**Status:** TARGET_COVERAGE_NOT_PERMISSION_MANIFEST

**New automatic grants:** No

**Workflow process rule:** Register and prove a distinct typed executor/step adapter with current run delegation; do not borrow interactive managed identity.

## MAT-009

**Module id:** BND-SIMPLECHATS

**Read contract ids:** CON-061

**Command contract ids:** CON-062

**Automation callers:** MOD-AGENTS; BND-WORKFLOWS; MOD-PROCESSES

**Observed surface:** Owner definition API observed; HR definition adapter missing/requested.

**Guardrails:** Definition management only; no transcript access, tools, agent runtime, implicit project context, or ordinary-turn injection.

**Status:** TARGET_COVERAGE_NOT_PERMISSION_MANIFEST

**New automatic grants:** No

**Workflow process rule:** Register and prove a distinct typed executor/step adapter with current run delegation; do not borrow interactive managed identity.

## MAT-010

**Module id:** MOD-PROMPTS

**Read contract ids:** CON-033

**Command contract ids:** CON-034

**Automation callers:** MOD-AGENTS; BND-WORKFLOWS; MOD-PROCESSES

**Observed surface:** Prompt retrieval and curator providers documented.

**Guardrails:** Safe lookup, draft/edit, version publication, and deletion require distinct permissions; preserve pinned use.

**Status:** TARGET_COVERAGE_NOT_PERMISSION_MANIFEST

**New automatic grants:** No

**Workflow process rule:** Register and prove a distinct typed executor/step adapter with current run delegation; do not borrow interactive managed identity.

## MAT-011

**Module id:** MOD-RESOURCES

**Read contract ids:** CON-035; CON-069

**Command contract ids:** CON-035; CON-070

**Automation callers:** MOD-AGENTS; BND-WORKFLOWS; MOD-PROCESSES

**Observed surface:** Resource services/catalog paths retained; direct general Resource tool attachment not established.

**Guardrails:** Catalog metadata differs from CRM resources and Storage bytes. Supported schema/connector actions are explicit.

**Status:** TARGET_COVERAGE_NOT_PERMISSION_MANIFEST

**New automatic grants:** No

**Workflow process rule:** Register and prove a distinct typed executor/step adapter with current run delegation; do not borrow interactive managed identity.

## MAT-012

**Module id:** BND-STORAGE

**Read contract ids:** CON-043; CON-071

**Command contract ids:** CON-043

**Automation callers:** MOD-AGENTS; BND-WORKFLOWS; MOD-PROCESSES

**Observed surface:** Authorized file/content paths retained; exact driver capabilities must be discovered.

**Guardrails:** Only owner-issued scoped content handles; bounds, use-time access, safe preview, provenance, cleanup, and egress.

**Status:** TARGET_COVERAGE_NOT_PERMISSION_MANIFEST

**New automatic grants:** No

**Workflow process rule:** Register and prove a distinct typed executor/step adapter with current run delegation; do not borrow interactive managed identity.

## MAT-013

**Module id:** MOD-SCHEDULER

**Read contract ids:** CON-066

**Command contract ids:** CON-067; CON-037

**Automation callers:** MOD-AGENTS; BND-WORKFLOWS; MOD-PROCESSES

**Observed surface:** Managed interactive workflow target/schedule search and schedule creation observed.

**Guardrails:** Plan write does not automatically authorize every future target operation. Recheck dispatch authority; process tool support is not implied.

**Status:** TARGET_COVERAGE_NOT_PERMISSION_MANIFEST

**New automatic grants:** No

**Workflow process rule:** Register and prove a distinct typed executor/step adapter with current run delegation; do not borrow interactive managed identity.

## MAT-014

**Module id:** MOD-TESTLAB

**Read contract ids:** CON-036

**Command contract ids:** CON-036; CON-076

**Automation callers:** MOD-AGENTS; BND-WORKFLOWS; MOD-PROCESSES

**Observed surface:** TestLab baseline paths retained; no universal runner/tool inferred.

**Guardrails:** Evidence/target revision and execution proof required; model text cannot manufacture a PASS or business acceptance.

**Status:** TARGET_COVERAGE_NOT_PERMISSION_MANIFEST

**New automatic grants:** No

**Workflow process rule:** Register and prove a distinct typed executor/step adapter with current run delegation; do not borrow interactive managed identity.

## MAT-015

**Module id:** MOD-MEMORY

**Read contract ids:** CON-038

**Command contract ids:** CON-038; CON-075

**Automation callers:** MOD-AGENTS; BND-WORKFLOWS; MOD-PROCESSES

**Observed surface:** Provider query/status tools documented; complete ingestion-tool surface not audited.

**Guardrails:** Derived nonauthoritative data with provenance, current source access, and retention; no source-master back-write.

**Status:** TARGET_COVERAGE_NOT_PERMISSION_MANIFEST

**New automatic grants:** No

**Workflow process rule:** Register and prove a distinct typed executor/step adapter with current run delegation; do not borrow interactive managed identity.

## MAT-016

**Module id:** MOD-COLLAB

**Read contract ids:** CON-050

**Command contract ids:** CON-050; CON-074

**Automation callers:** MOD-AGENTS; BND-WORKFLOWS; MOD-PROCESSES

**Observed surface:** Thread/member/message paths are inherited discovery, not a new runtime claim.

**Guardrails:** Membership and publication audience apply; Collaboration is not agent or ordinary-chat transcript storage.

**Status:** TARGET_COVERAGE_NOT_PERMISSION_MANIFEST

**New automatic grants:** No

**Workflow process rule:** Register and prove a distinct typed executor/step adapter with current run delegation; do not borrow interactive managed identity.

## MAT-017

**Module id:** MOD-PLUGINS

**Read contract ids:** CON-040

**Command contract ids:** CON-040

**Automation callers:** MOD-AGENTS; BND-WORKFLOWS; MOD-PROCESSES

**Observed surface:** Plugin installation/grant/OAuth/activation exist in baseline; no generic agent administration grant.

**Guardrails:** Install, execute, activate, and grant administration are distinct. Privileged changes require explicit human/admin policy, not default automation.

**Status:** TARGET_COVERAGE_NOT_PERMISSION_MANIFEST

**New automatic grants:** No

**Workflow process rule:** Register and prove a distinct typed executor/step adapter with current run delegation; do not borrow interactive managed identity.

## MAT-018

**Module id:** MOD-SECURITY

**Read contract ids:** CON-042

**Command contract ids:** CON-042

**Automation callers:** MOD-AGENTS; BND-WORKFLOWS; MOD-PROCESSES

**Observed surface:** Secret infrastructure exists; raw secret management is not an agent operation by default.

**Guardrails:** Return only permitted metadata/references. No generic secret reveal/export/grant/revocation capability; runtime resolves transport-only values.

**Status:** TARGET_COVERAGE_NOT_PERMISSION_MANIFEST

**New automatic grants:** No

**Workflow process rule:** Register and prove a distinct typed executor/step adapter with current run delegation; do not borrow interactive managed identity.

## MAT-019

**Module id:** MOD-WORKSPACE

**Read contract ids:** CON-047; CON-048

**Command contract ids:** CON-047

**Automation callers:** MOD-AGENTS; BND-WORKFLOWS; MOD-PROCESSES

**Observed surface:** Preferences and control-plane-related UI exist; control-plane writes are not Workspace-owned.

**Guardrails:** Do not route provider/security/database administration through a universal settings writer. No ambient current profile for durable work.

**Status:** TARGET_COVERAGE_NOT_PERMISSION_MANIFEST

**New automatic grants:** No

**Workflow process rule:** Register and prove a distinct typed executor/step adapter with current run delegation; do not borrow interactive managed identity.

## MAT-020

**Module id:** BND-CONNECTORS

**Read contract ids:** CON-041

**Command contract ids:** CON-041

**Automation callers:** MOD-AGENTS; BND-WORKFLOWS; MOD-PROCESSES

**Observed surface:** Manifests/schema integration exists; executable handler coverage remains discovery.

**Guardrails:** Descriptor does not establish executable command. Network/secret/egress policy and supported transport must be explicit.

**Status:** TARGET_COVERAGE_NOT_PERMISSION_MANIFEST

**New automatic grants:** No

**Workflow process rule:** Register and prove a distinct typed executor/step adapter with current run delegation; do not borrow interactive managed identity.

## MAT-021

**Module id:** BND-CONVERSATIONS

**Read contract ids:** None

**Command contract ids:** None

**Automation callers:** None

**Observed surface:** Neutral presentation only; no direct domain operation grants.

**Guardrails:** Emit intent and render state. Product-specific adapters call owners; no privileged tool/DbContext/runtime dependency.

**Status:** TARGET_COVERAGE_NOT_PERMISSION_MANIFEST

**New automatic grants:** No

**Workflow process rule:** Register and prove a distinct typed executor/step adapter with current run delegation; do not borrow interactive managed identity.

## MAT-022

**Module id:** BND-COMPOSITION

**Read contract ids:** CON-048

**Command contract ids:** CON-048; CON-056

**Automation callers:** None

**Observed surface:** Host/database control plane exists; not a general agent surface.

**Guardrails:** Wire adapters, scope/incarnation, host policy, and transfer. No blanket automated DB migration/profile switching or business-master authority.

**Status:** TARGET_COVERAGE_NOT_PERMISSION_MANIFEST

**New automatic grants:** No

**Workflow process rule:** Register and prove a distinct typed executor/step adapter with current run delegation; do not borrow interactive managed identity.

## MAT-023

**Module id:** BND-MAF

**Read contract ids:** CON-051

**Command contract ids:** None

**Automation callers:** None

**Observed surface:** Neutral runtime composition mechanisms documented.

**Guardrails:** Resolve invocation and provider composition; no CRM/SimpleChats all-domain business gateway or owner schemas.

**Status:** TARGET_COVERAGE_NOT_PERMISSION_MANIFEST

**New automatic grants:** No

**Workflow process rule:** Register and prove a distinct typed executor/step adapter with current run delegation; do not borrow interactive managed identity.

## MAT-024

**Module id:** BND-API

**Read contract ids:** None

**Command contract ids:** None

**Automation callers:** None

**Observed surface:** External transport adapter only.

**Guardrails:** HTTP authorization and typed transport delegate to owner operations; route reachability is not an agent grant.

**Status:** TARGET_COVERAGE_NOT_PERMISSION_MANIFEST

**New automatic grants:** No

**Workflow process rule:** Register and prove a distinct typed executor/step adapter with current run delegation; do not borrow interactive managed identity.
