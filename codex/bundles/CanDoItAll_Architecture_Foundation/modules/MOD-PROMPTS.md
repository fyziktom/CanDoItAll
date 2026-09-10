# MOD-PROMPTS — Prompts: versioned library and curation

**Target owner:** Prompts

PromptArtifact, its versions, collections, tags, compatibility and owned usage/reference evidence. A workflow or agent selects a versioned prompt without acquiring authority to edit it.

**Repository discovery location:** `src/Modules/CanDoItAll.Modules.Prompts`

**Primary sources:** SRC-015, MAP-11, SRC-003

## Provided responsibilities

- Versioned prompt lookup/search and typed owner create/edit/publish/import commands.
- Projection contributions, composer use and curator application APIs.
- Automation uses the same canonical owner application boundary as other callers where supported; required read/write surfaces and rollout status are specified in catalogs/module-operation-matrix.json. This is not a default grant.

## Required capabilities

- Providers safe compatibility catalog; Agents for launching a curator.
- Projects for scoped references; Security/Storage for attachments and access as needed.

## Forbidden shortcuts

- MAF Core must not hardcode product prompt-tool names.
- A Structure projection does not change the original prompt.
- Never skip approval merely because a curator agent supplied the proposal.

## Future needs and explicit limits

- REQUIRED FOR THE BOUNDARY: curator mutation APIs belong to Prompts; the agent adapter translates intent.
- ESTIMATE: stronger publishing/review policy and compatibility matrices; not a new framework for each edit.

## Persistence

Prompts_* tables have one writer. A prompt version used by execution does not change when the current final/default changes. Historical references and source deletion have explicit retention rules.

## Recorded capabilities

- **FEAT-053** [DOC; BASELINE_EVIDENCE_NOT_RUNTIME_PROOF] Prompt library artifacts are available to users and agents.
- **FEAT-054** [MAP; BASELINE_EVIDENCE_NOT_RUNTIME_PROOF] Prompt artifacts, versions, collections, tags, provider compatibility, consumer tokens, and warnings.
- **FEAT-055** [MAP; BASELINE_EVIDENCE_NOT_RUNTIME_PROOF] Prompt imports, search, usage, seed data, and search indexing.
- **FEAT-056** [MAP; BASELINE_EVIDENCE_NOT_RUNTIME_PROOF] Prompt picker, editor, composer, and warning surfaces.
- **FEAT-057** [DOC; BASELINE_EVIDENCE_NOT_RUNTIME_PROOF] Runtime prompt retrieval and authorized curation use separate providers.
- **FEAT-058** [MAP; BASELINE_EVIDENCE_NOT_RUNTIME_PROOF] PromptCuratorLauncher is a consumer-owned port rather than embedded runtime knowledge.

## Contracts and automation coverage

See [complete communication contracts](../catalogs/contracts.md) and [module-operation matrix](../catalogs/module-operation-matrix.md). Caller membership is not a permission grant.

- **CON-021 — Ensure structure contribution:** caller.
- **CON-032 — Read-only structure contribution adapter:** implementation/integration boundary, extension adapter.
- **CON-033 — Prompt search/version retrieval:** declaration owner, implementation/integration boundary.
- **CON-034 — Prompt authoring/curation:** declaration owner, implementation/integration boundary.
- **CON-046 — Project lifecycle participant protocol:** implementation/integration boundary, extension adapter.
- **CON-053 — Managed curator launch port:** declaration owner, caller.
- **CON-056 — Database profile transfer participant protocol:** implementation/integration boundary, extension adapter.
- **CON-072 — Workflow executor owner-operation extension port:** destination data owner, not runtime-port implementer.
- **CON-073 — Process step owner-operation extension port:** destination data owner, not runtime-port implementer.

## Proof and unresolved questions

Relevant planned scenarios: QA-066, QA-067, QA-076, QA-115, QA-119, QA-151.

Related gaps: GAP-017.

Every application scenario remains NOT_RUN. Inspect actual current registrations, fields, writers, and persisted contracts before a scoped change.
