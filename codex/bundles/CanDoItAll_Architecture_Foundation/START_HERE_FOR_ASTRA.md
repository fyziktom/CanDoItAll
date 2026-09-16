# Entry instructions for GPT-6 Astra xhigh / Codex

Work as a senior C# architect and implementer **only within the next concrete approved scope**. This package is a shared foundation, not authority to implement the entire refactoring. Without a separate narrow implementation request, perform rebaseline/analysis and propose the affected boundary; do not create subbundles or rewrite modules automatically.

**Write all engineering artifacts in English:** instructions, bundles, plans, design decisions, code comments, tests' explanatory prose, reviews, and completion reports. Preserve technical identifiers and required product localization. See [LANGUAGE_POLICY.md](LANGUAGE_POLICY.md).

## Reading order

Read [README](README.md), [baseline](evidence/baseline.md), [decisions](architecture/01-principles-and-decisions.md), [glossary](architecture/02-domain-glossary.md), [ownership](architecture/03-context-map-and-ownership.md), and [contracts](architecture/04-contract-rules.md).

Then read [cross-module adapters](architecture/14-cross-module-operations-and-runtime-adapters.md), [managed agents and Simple Chats](architecture/15-managed-agents-and-simple-chats-administration.md), [reverse queries](architecture/16-cross-module-queries-and-context-use.md), and [operation/recovery protocol](architecture/17-operation-protocol-and-multi-owner-journeys.md). For the affected slice also read Structure contributions, persistence, projection, file lifecycle, security, product journeys, and the relevant module cards, CON/FEAT/QA/gap records and [runtime surfaces](catalogs/runtime-surfaces.md).

## Use evidence correctly

CODE/DOC/MAP/USER is not current PASS. Verify the actual SHA and read relevant current registrations, callers, owner methods, authorization helpers, and persistence. Reuse completed Agents UI seams; do not overwrite them from older maps. Original Fable implementation subbundles are not adopted.

No mechanical Contracts project per module, interface per class, or quota-driven class splitting. Define semantics and authority, remove the actual foreign write/cycle/backdoor, then choose physical placement. Interface naming in this guide is illustrative; prefer compatible existing APIs.

## Preserve useful cross-module behavior

Agents/workflows/processes can query and request changes from CRM, Resources, Simple Chats definitions, Scheduler, Prompts, Projects, Structure, and other permitted owners. They do not write foreign EF entities. Structure is not a universal intermediary. Safe cross-module collaboration is required; disabling it is not decoupling.

Preserve actual HR CRM contact/affiliation operations and managed-identity guards. Add the requested Simple Chats administration only through its existing owner boundary and a separately scoped adapter. Never add agent runtime/tools/implicit context to Simple Chats or infer transcript access from definition administration. Do not impersonate interactive HR/Scheduler in background execution.

Agents/Providers retains technical and AI-price authority; CRM retains its local parties/staffing; Work Management owns task assignments; Structure owns native notes/links and projections. Keep floating agents, FileTools/task operations, workflow/process writeback, source scope, pricing history, and truthful post-commit warnings.

## Before changing code

Record the operation scope and unchanged capabilities; master/projection/historical/local ownership; exact entry points and public/persisted shapes; real tool/executor/HTTP availability and purposes; authority/disclosure policy; identity/revision/pricing/transaction/receipt plan; selected QA baseline; single-writer switch/rollback; unknowns. Do not paper over a security/data question with a convenient default.

Do not upgrade MAF/SDK/providers, parallelize tools, redesign mobile UI, add unrelated forecast features, introduce microservices, or run a blanket rewrite without explicit scope. Future/gap records are not automatic TODOs.

## Evidence after the change

Show real production routing to the owner, removed problematic references/writers, actual schema/migration path, discovered tests and execution results, relevant UI/HTTP/tool/executor parity, and failure injection. Distinguish PASS/FAIL/BLOCKED/NOT_RUN. A fake renderer or build is not runtime validation. Missing environment is an unperformed gate, not success.

Before closure review again as architect and QA for competing authority, hidden dependencies, scope/disclosure leaks, lost effects, approval bypass, post-commit false failure, replay duplicates, and ordinary-chat regression. Correct findings and repeat affected checks. Run only relevant small-slice proof plus broader gates triggered by shared authority/serialization/storage/schema changes. Do not omit an affected real cross-module journey merely to reduce test time.
