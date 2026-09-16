# Requirements and traceability

The requirement-to-scenario mapping is explicit but planned. Feature-proof candidates are broader module-based discovery aids, not complete semantic coverage.

## REQ-001

**Requirement:** Refactor rather than perform a complete rewrite.

**Primary document:** architecture/01-principles-and-decisions.md

**Planned qa ids:** QA-092; QA-100

**Status:** DESIGN_ADDRESSED_RUNTIME_PROOF_NOT_RUN

**Proof note:** References identify a plan or description, not an executed test.

## REQ-002

**Requirement:** Provide a shared architectural foundation without implementation subbundles or blanket execution.

**Primary document:** START_HERE_FOR_ASTRA.md

**Planned qa ids:** None

**Status:** PACKAGE_REQUIREMENT

**Proof note:** References identify a plan or description, not an executed test.

## REQ-003

**Requirement:** Stabilize the current Agents checkpoint; defer new broad UI extraction until the affected boundaries are correct.

**Primary document:** architecture/11-ui-seams-and-hosting.md

**Planned qa ids:** QA-098; QA-100

**Status:** DESIGN_ADDRESSED_RUNTIME_PROOF_NOT_RUN

**Proof note:** References identify a plan or description, not an executed test.

## REQ-004

**Requirement:** Agents owns technical definitions and Provider/AI pricing; CRM owns its projection and staffing facts.

**Primary document:** architecture/03-context-map-and-ownership.md

**Planned qa ids:** QA-049; QA-050; QA-051; QA-056; QA-057

**Status:** DESIGN_ADDRESSED_RUNTIME_PROOF_NOT_RUN

**Proof note:** References identify a plan or description, not an executed test.

## REQ-005

**Requirement:** Project Structure owns notes, native items, and relationships, not only projections.

**Primary document:** architecture/05-project-structure-and-safe-contributions.md

**Planned qa ids:** QA-003; QA-022; QA-025

**Status:** DESIGN_ADDRESSED_RUNTIME_PROOF_NOT_RUN

**Proof note:** References identify a plan or description, not an executed test.

## REQ-006

**Requirement:** Agents, processes, and workflows may safely request or require node, asset, and task creation.

**Primary document:** architecture/05-project-structure-and-safe-contributions.md

**Planned qa ids:** QA-004; QA-017; QA-018; QA-019; QA-020; QA-021; QA-035

**Status:** DESIGN_ADDRESSED_RUNTIME_PROOF_NOT_RUN

**Proof note:** References identify a plan or description, not an executed test.

## REQ-007

**Requirement:** Preserve floating agents over Structure/Gantt and effective contextual operations.

**Primary document:** architecture/08-execution-and-product-journeys.md

**Planned qa ids:** QA-001; QA-002; QA-005; QA-006; QA-007; QA-008

**Status:** DESIGN_ADDRESSED_RUNTIME_PROOF_NOT_RUN

**Proof note:** References identify a plan or description, not an executed test.

## REQ-008

**Requirement:** Preserve file reading and creation without losing security/storage semantics.

**Primary document:** architecture/09-assets-files-and-lifecycle.md

**Planned qa ids:** QA-014; QA-015; QA-016; QA-075; QA-093; QA-094

**Status:** DESIGN_ADDRESSED_RUNTIME_PROOF_NOT_RUN

**Proof note:** References identify a plan or description, not an executed test.

## REQ-009

**Requirement:** Preserve workflow launch from Structure and result writeback.

**Primary document:** architecture/08-execution-and-product-journeys.md

**Planned qa ids:** QA-027; QA-028; QA-029; QA-030; QA-031; QA-033; QA-035

**Status:** DESIGN_ADDRESSED_RUNTIME_PROOF_NOT_RUN

**Proof note:** References identify a plan or description, not an executed test.

## REQ-010

**Requirement:** Preserve process launch from Structure, writeback, subprocesses, and recovery.

**Primary document:** architecture/08-execution-and-product-journeys.md

**Planned qa ids:** QA-036; QA-037; QA-038; QA-039; QA-044; QA-045

**Status:** DESIGN_ADDRESSED_RUNTIME_PROOF_NOT_RUN

**Proof note:** References identify a plan or description, not an executed test.

## REQ-011

**Requirement:** Define precise terminology, interface ownership, and communication direction.

**Primary document:** architecture/04-contract-rules.md

**Planned qa ids:** QA-040; QA-059; QA-072; QA-091

**Status:** DESIGN_ADDRESSED_RUNTIME_PROOF_NOT_RUN

**Proof note:** References identify a plan or description, not an executed test.

## REQ-012

**Requirement:** Split the shared DbContext without duplicate truth or lost guarantees.

**Primary document:** architecture/06-persistence-transactions-and-migrations.md

**Planned qa ids:** QA-084; QA-085; QA-089; QA-090; QA-091; QA-092; QA-106

**Status:** DESIGN_ADDRESSED_RUNTIME_PROOF_NOT_RUN

**Proof note:** References identify a plan or description, not an executed test.

## REQ-013

**Requirement:** Map current capabilities of every module and identify future needs and gaps.

**Primary document:** catalogs/modules.md

**Planned qa ids:** QA-109; QA-110; QA-111; QA-112; QA-113; QA-114; QA-115; QA-116; QA-117; QA-118; QA-119

**Status:** DESIGN_ADDRESSED_RUNTIME_PROOF_NOT_RUN

**Proof note:** References identify a plan or description, not an executed test.

## REQ-014

**Requirement:** Preserve the product, not merely a cleaner reference graph; do not pretend runtime certainty.

**Primary document:** qa/proof-levels-and-fixtures.md

**Planned qa ids:** QA-096; QA-098; QA-099; QA-100; QA-101; QA-119

**Status:** DESIGN_ADDRESSED_RUNTIME_PROOF_NOT_RUN

**Proof note:** References identify a plan or description, not an executed test.

## REQ-015

**Requirement:** Perform architectural and QA reviews, correct findings, and repeat review before producing the ZIP.

**Primary document:** reviews/review-log.md

**Planned qa ids:** None

**Status:** PACKAGE_REQUIREMENT

**Proof note:** References identify a plan or description, not an executed test.

## REQ-016

**Requirement:** Provide safe read and write paths from agents/workflows/processes to appropriate owners beyond Structure.

**Primary document:** architecture/14-cross-module-operations-and-runtime-adapters.md

**Planned qa ids:** QA-122; QA-124; QA-139; QA-140; QA-161

**Status:** DESIGN_ADDRESSED_RUNTIME_PROOF_NOT_RUN

**Proof note:** Requirement mapping is a plan, not proof of execution.

## REQ-017

**Requirement:** Preserve HR agent administration and actual existing CRM contact/affiliation tools; support a possible CRM-specialist without duplicate ownership.

**Primary document:** architecture/15-managed-agents-and-simple-chats-administration.md

**Planned qa ids:** QA-122; QA-123; QA-149; QA-150

**Status:** DESIGN_ADDRESSED_RUNTIME_PROOF_NOT_RUN

**Proof note:** Requirement mapping is a plan, not proof of execution.

## REQ-018

**Requirement:** Add the missing governed HR integration for Simple Chats definition management without making Simple Chats an agent.

**Primary document:** architecture/15-managed-agents-and-simple-chats-administration.md

**Planned qa ids:** QA-129; QA-130; QA-131; QA-132; QA-133; QA-134; QA-135; QA-136

**Status:** DESIGN_ADDRESSED_RUNTIME_PROOF_NOT_RUN

**Proof note:** Requirement mapping is a plan, not proof of execution.

## REQ-019

**Requirement:** Define reverse queries for CRM people and Resources, including identity, privacy, provider disclosure, and task use.

**Primary document:** architecture/16-cross-module-queries-and-context-use.md

**Planned qa ids:** QA-124; QA-126; QA-127; QA-128; QA-152; QA-154; QA-155

**Status:** DESIGN_ADDRESSED_RUNTIME_PROOF_NOT_RUN

**Proof note:** Requirement mapping is a plan, not proof of execution.

## REQ-020

**Requirement:** Distinguish actual Scheduler/Process runtime surfaces from new intended extensions.

**Primary document:** catalogs/runtime-surfaces.md

**Planned qa ids:** QA-143; QA-147; QA-148; QA-159; QA-160

**Status:** DESIGN_ADDRESSED_RUNTIME_PROOF_NOT_RUN

**Proof note:** Requirement mapping is a plan, not proof of execution.

## REQ-021

**Requirement:** Define safe cross-owner retries, receipts, approval continuation, compensation, and workflow/process checkpoint recovery.

**Primary document:** architecture/17-operation-protocol-and-multi-owner-journeys.md

**Planned qa ids:** QA-136; QA-137; QA-138; QA-141; QA-142; QA-144; QA-145; QA-146; QA-162; QA-163

**Status:** DESIGN_ADDRESSED_RUNTIME_PROOF_NOT_RUN

**Proof note:** Requirement mapping is a plan, not proof of execution.

## REQ-022

**Requirement:** Translate the entire replacement package and require downstream engineering documentation/bundles/comments to be English.

**Primary document:** LANGUAGE_POLICY.md

**Planned qa ids:** QA-165

**Status:** DESIGN_ADDRESSED_RUNTIME_PROOF_NOT_RUN

**Proof note:** Requirement mapping is a plan, not proof of execution.
