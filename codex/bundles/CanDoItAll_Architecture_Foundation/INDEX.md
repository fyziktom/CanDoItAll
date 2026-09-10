# Package index

Start with [START_HERE_FOR_ASTRA.md](START_HERE_FOR_ASTRA.md) and [LANGUAGE_POLICY.md](LANGUAGE_POLICY.md).

## Architecture

- [1. Binding principles and decisions](architecture/01-principles-and-decisions.md)
- [2. Domain glossary](architecture/02-domain-glossary.md)
- [3. Context map and ownership](architecture/03-context-map-and-ownership.md)
- [4. Contract semantics and dependency direction](architecture/04-contract-rules.md)
- [5. Project Structure and safe module contributions](architecture/05-project-structure-and-safe-contributions.md)
- [6. Persistence, transactions, and migrations without duplicate truth](architecture/06-persistence-transactions-and-migrations.md)
- [7. Projections, consistency, and scope](architecture/07-projections-consistency-and-scope.md)
- [8. Execution and preservation of product journeys](architecture/08-execution-and-product-journeys.md)
- [9. Files, assets, deletion, and transfer](architecture/09-assets-files-and-lifecycle.md)
- [10. Authority, security, and operational traceability](architecture/10-security-authority-and-observability.md)
- [11. UI seams, composition, and sequencing](architecture/11-ui-seams-and-hosting.md)
- [12. Governance of subsequent changes](architecture/12-change-governance-and-cutover-gates.md)
- [13. Identities, relationships, and reference shapes](architecture/13-identity-relations-and-reference-shapes.md)
- [14. Cross-module operations and runtime adapters](architecture/14-cross-module-operations-and-runtime-adapters.md)
- [15. Managed agents and Simple Chats administration](architecture/15-managed-agents-and-simple-chats-administration.md)
- [16. Cross-module queries, identity resolution, and data use](architecture/16-cross-module-queries-and-context-use.md)
- [17. Owner-operation protocol and multi-owner journeys](architecture/17-operation-protocol-and-multi-owner-journeys.md)

## Modules and logical boundaries

- [BND-API — HTTP / transport adapters](modules/BND-API.md)
- [BND-COMPOSITION — Host, composition and control plane](modules/BND-COMPOSITION.md)
- [BND-CONNECTORS — Connectors / external integrations](modules/BND-CONNECTORS.md)
- [BND-CONVERSATIONS — Shared conversation presentation](modules/BND-CONVERSATIONS.md)
- [BND-MAF — Neutral runtime / SDK boundary](modules/BND-MAF.md)
- [BND-SIMPLECHATS — Simple Chats: independent ordinary LLM conversations](modules/BND-SIMPLECHATS.md)
- [BND-STORAGE — Storage and FileTools](modules/BND-STORAGE.md)
- [BND-WORK — Work Management within Project Structure](modules/BND-WORK.md)
- [BND-WORKFLOWS — Agents / Workflows](modules/BND-WORKFLOWS.md)
- [MOD-AGENTS — Agents: definitions, capabilities and governed use](modules/MOD-AGENTS.md)
- [MOD-COLLAB — Collaboration: discussion and human cooperation](modules/MOD-COLLAB.md)
- [MOD-CRM — CRM / HR: parties and human/AI resource planning](modules/MOD-CRM.md)
- [MOD-MEMORY — Memory: providers, derived knowledge and provenance](modules/MOD-MEMORY.md)
- [MOD-PLUGINS — Plugins: installation and authorized activation](modules/MOD-PLUGINS.md)
- [MOD-PROCESSES — Processes: definitions and authoritative execution](modules/MOD-PROCESSES.md)
- [MOD-PROJECTS — Projects: portfolio and lifecycle](modules/MOD-PROJECTS.md)
- [MOD-PROMPTS — Prompts: versioned library and curation](modules/MOD-PROMPTS.md)
- [MOD-PROVIDERS — Agents / Providers: administration, pricing, sharing and usage](modules/MOD-PROVIDERS.md)
- [MOD-RESOURCES — Resources: reusable material catalog](modules/MOD-RESOURCES.md)
- [MOD-SCHEDULER — SchedulerPlanner: execution calendar, not a task plan](modules/MOD-SCHEDULER.md)
- [MOD-SECURITY — Security: secrets, identities and access enforcement](modules/MOD-SECURITY.md)
- [MOD-STRUCTURE — Project Structure and Workbench: native graph plus projections](modules/MOD-STRUCTURE.md)
- [MOD-TESTLAB — TestLab: product test plans, tests and evidence](modules/MOD-TESTLAB.md)
- [MOD-WORKSPACE — Workspace: environment settings and presentation](modules/MOD-WORKSPACE.md)

## Catalogs

- [Semantic communication contracts](catalogs/contracts.md)
- [Capability inventory](catalogs/features.md)
- [Managed agent policy profiles](catalogs/managed-agent-profiles.md)
- [Module operation coverage matrix](catalogs/module-operation-matrix.md)
- [Module and boundary catalog](catalogs/modules.md)
- [Observed and planned runtime surfaces](catalogs/runtime-surfaces.md)

## Evidence

- [Evidence conventions](evidence/README.md)
- [Baseline and targeted revision audit](evidence/baseline.md)
- [Gaps, limitations, and future needs](evidence/gaps-and-future-needs.md)
- [Persistence discovery](evidence/persistence-discovery.md)
- [Source register](evidence/sources.md)
- [Original-to-English replacement coverage](evidence/translation-coverage.md)

## QA

- [Contract-to-scenario proof plan](qa/contract-proof-plan.md)
- [Architectural and product invariants](qa/invariants.md)
- [Proof levels, fixtures, and acceptance](qa/proof-levels-and-fixtures.md)
- [Regression and hardening scenarios](qa/scenarios.md)
- [Requirements and traceability](qa/traceability.md)

## Inputs and reviews

- [Consolidated original mandate — English translation](inputs/original-requirements.md)
- [Revision request — English translation](inputs/revision-request.md)
- [Preparation plan and scope](reviews/preparation-plan-and-scope.md)
- [Architecture and QA review log](reviews/review-log.md)

## Machine-readable data

All JSON catalogs are under `catalogs/`. `manifest.json` records counts and source pins. `checksums.sha256` covers every delivered file except itself. `tools/validate_package.py` performs package-only checks; it does not run application tests.
