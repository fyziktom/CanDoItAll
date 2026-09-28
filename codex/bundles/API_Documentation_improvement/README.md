# CanDoItAll API documentation completion pack

**Implementation model:** Opus 5 Max  
**Review date:** 2026-09-19  
**Purpose:** Make the HTTP API understandable from its generated OpenAPI document and Swagger UI, with the same accurate English documentation in C# source. Synchronize SharedInfo, including API skills.

## Start here

Give Opus this entire directory and ask it to execute **[01_OPUS_5_MAX_PROMPT.md](01_OPUS_5_MAX_PROMPT.md)**. The other files are working references, not independent competing instructions.

1. Read the prompt and [source review](02_SOURCE_REVIEW.md).
2. Adopt the [reviewed terminology](glossary/API_DOMAIN_GLOSSARY.md) and [editorial rules](03_DOCUMENTATION_STANDARD.md).
3. Prove the [XML-to-OpenAPI pipeline](04_XML_OPENAPI_PIPELINE.md) using real task-update and CRM cases before bulk annotation.
4. Follow the [task-update worksheet](05_TASK_UPDATE_CONTRACT.md), [inventory plan](inventory/COVERAGE_PLAN.md), [SharedInfo synchronization plan](06_SHAREDINFO_SYNC.md), and [acceptance tests](07_VALIDATION_AND_ACCEPTANCE.md).
5. Use the optional offline helpers under `tools/`; their limitations and tested scope are in [tools/README.md](tools/README.md).

## What was actually reviewed

Product source was inspected at **development / d0f3c41a458fd8543f447a4525e6123e6820de59**. SharedInfo was inspected at **main / 409d7c291e9378b2a17fdfce91ed64e9ef851f0e**. The task-update input and HTTP binding repair are present in that product source. The attached execution report predates that observation and ended by saying nothing was committed; it must not override the later source evidence.

The observed product `main` was `10a72521aae7cbcd5d5bc2b7c16366d496ef8285`; `components-decoupling` was `0bdad02f33ec5e3351e8b61a431511c581400b26` (bundle removal). **Do not assume those branches contain identical code or reset a local worktree to one of these hashes.** Reconfirm the actual starting branch, HEAD and dirty state.

This is a source-review and implementation preparation pack, not a certified current API export. No product build, live HTTP, database, Swagger or agent test was executed by the reviewer. The complete current API/schema closure must be inventoried by Opus. Source IDs and exact review limits are in `evidence/sources.json` and `evidence/review-baseline.json`.

## Deliverable map

| Item | Use |
|---|---|
| `01_OPUS_5_MAX_PROMPT.md` | Autonomous implementation assignment, scope and exit conditions |
| `02_SOURCE_REVIEW.md` | Verified current gaps and source/report distinctions |
| `03_DOCUMENTATION_STANDARD.md` | Operation, DTO, property, enum and example writing rules |
| `04_XML_OPENAPI_PIPELINE.md` | Native ASP.NET Core OpenAPI integration and proof strategy |
| `05_TASK_UPDATE_CONTRACT.md` | Verified field meanings, constraints and task migration work |
| `06_SHAREDINFO_SYNC.md` | Canonical glossary/standard placement, snapshot and skill synchronization |
| `07_VALIDATION_AND_ACCEPTANCE.md` | Functional, structural, rendered and semantic documentation gates |
| `glossary/*` | 107 source-grounded preferred terms in Markdown and JSON |
| `inventory/*` | Historical family seed and current endpoint/schema inventory procedure |
| `examples/*` | Editorial examples and source-grounded task payload construction recipe |
| `tools/*`, `tests/*` | Offline JSON audit and structural-comparison helper with self-tests |
| `evidence/*` | Provenance, inspected sources, reviewer checks and limitations |
| `MANIFEST.sha256` | Integrity hashes for this preparation pack |

## Important boundaries

Do not copy this whole working pack into product documentation, SharedInfo or `codex/bundles`. Move/adopt only durable standards, glossary definitions, implementation and maintained tests in their proper owners. The user authorized changes in both repositories, not a new round of unrelated UI refactoring, merges, pushes, deployments, permission expansion or framework upgrades.

A glossary is an authoring control, not a substitute for self-contained endpoint and property descriptions. A reader should not have to leave Swagger to understand an ordinary request.
