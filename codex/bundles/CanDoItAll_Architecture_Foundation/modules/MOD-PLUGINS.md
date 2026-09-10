# MOD-PLUGINS — Plugins: installation and authorized activation

**Target owner:** Plugins

Plugin installation/activation, manifests, grants, external connections/OAuth and package/runtime lifecycle. Domains invoked by plugins still enforce their own rules.

**Repository discovery location:** `src/Modules/CanDoItAll.Modules.Plugins`

**Primary sources:** SRC-019, MAP-11

## Provided responsibilities

- Manifest/capability catalogs and install/activate/disable/connection commands.
- Tool/executor registration with demonstrated availability and grant policy.
- Automation uses the same canonical owner application boundary as other callers where supported; required read/write surfaces and rollout status are specified in catalogs/module-operation-matrix.json. This is not a default grant.

## Required capabilities

- Security for purpose-limited secrets/OAuth; transport/network adapters.
- Host composition for activation and domain owners for actual commands.

## Forbidden shortcuts

- A plugin grant is not blanket permission for any project write.
- Do not bypass the Security broker by reading the vault without purpose.
- An installed package is not necessarily a loaded, usable capability.

## Future needs and explicit limits

- REQUIRED FOR THE BOUNDARY: invalidate active tool/capability caches after grant changes.
- ESTIMATE: finer version compatibility and package isolation; not an automatic platform redesign.

## Persistence

Plugins owns lifecycle records; Security owns secrets. Installation recovery and disabling during execution have observable states. Keep schema changes on one migration path.

## Recorded capabilities

- **FEAT-075** [DOC; BASELINE_EVIDENCE_NOT_RUNTIME_PROOF] Plugin catalog, installation, grants, OAuth, logs, activation, runtime-host tools, and settings.
- **FEAT-076** [DOC; BASELINE_EVIDENCE_NOT_RUNTIME_PROOF] Grant evaluation distinguishes disabled, missing, undeclared, and ungranted capabilities.
- **FEAT-077** [DOC; BASELINE_EVIDENCE_NOT_RUNTIME_PROOF] Bundled src/plugins are activated by manifest/registrar, not by page-level assembly loading.
- **FEAT-078** [MAP; BASELINE_EVIDENCE_NOT_RUNTIME_PROOF] Persistence for plugin installations, logs, and OAuth sessions.
- **FEAT-079** [MAP; BASELINE_EVIDENCE_NOT_RUNTIME_PROOF] Plugin secret broker versus raw-vault access requires boundary verification.

## Contracts and automation coverage

See [complete communication contracts](../catalogs/contracts.md) and [module-operation matrix](../catalogs/module-operation-matrix.md). Caller membership is not a permission grant.

- **CON-040 — Plugin lifecycle/capability API:** declaration owner, implementation/integration boundary.
- **CON-042 — Secret reference and purpose-scoped use:** caller.
- **CON-056 — Database profile transfer participant protocol:** implementation/integration boundary, extension adapter.

## Proof and unresolved questions

Relevant planned scenarios: QA-068, QA-069, QA-102, QA-105, QA-114.

Related gaps: GAP-016, GAP-021.

Every application scenario remains NOT_RUN. Inspect actual current registrations, fields, writers, and persisted contracts before a scoped change.
