# Collaboration UI decoupling — Codex handoff

**Selected next module: Collaboration.** This package asks Codex GPT-6 Astra Max to extract the complete existing `/collaboration` workspace into a lightweight rendering library and a real backend-free UI sandbox, with production integration, relevant lifecycle repairs, tests and maintained documentation.

## Start

Extract this archive outside the application source tree, make the folder available to Codex, and open the intended working checkout of the main CanDoItAll repository. The existing checkout may be a new branch based on newer `development`; no revision checkout is required by this package.

Give Codex this instruction (adjust only the handoff folder location if needed):

```text
Read and execute CanDoItAll_Collaboration_UI_Decoupling/prompt.md.
Use its bundled shared foundation and module-specific review/validation documents.
Implement only the Collaboration UI decoupling on the current CanDoItAll checkout.
Complete the production wiring, real sandbox, affected tests and documentation.
Do not stop at a plan, and do not move on to another module.
```

The full execution prompt is [prompt.md](prompt.md). The ZIP is self-contained: it includes the previous shared v3 foundation **unchanged**, so no manual merging of old shared packages is needed. The separately downloadable prompt is a reading convenience; its relative companion references are resolved by this extracted package.

## Contents

| File | Role |
|---|---|
| [prompt.md](prompt.md) | Detailed English implementation assignment, scope, ownership and latitude. |
| [MODULE_SELECTION.md](MODULE_SELECTION.md) | Established versus remaining seams, selection rationale and flexible later sequence. |
| [REVIEW_NOTES.md](REVIEW_NOTES.md) | Actual Collaboration code observations, compatibility anchors and non-goals. |
| [VALIDATION_MATRIX.md](VALIDATION_MATRIX.md) | Source-derived test anchors, new behavior obligations, browser/watch proof and closure. |
| [SOURCES.md](SOURCES.md), [sources.json](sources.json) | Current source references and explicit read coverage. |
| [shared/README.md](shared/README.md) | Unmodified `CDA-UI-DECOUPLING-SHARED-v3` foundation. |
| [PACKAGE_VALIDATION.md](PACKAGE_VALIDATION.md) | Package-only checks; not application verification. |
| [bundle.json](bundle.json), `MANIFEST.sha256` | Handoff metadata and file integrity. |

## Scope in one sentence

Production keeps its route and in-process application owner; the same lightweight feature renderers are used by production and a deterministic sandbox. No API-only conversion, database redesign, large-module extraction or automatic next-module work is authorized by this assignment.

Review basis: `development` at `7db3543ab437376baeca55089cb331fbe1b30483`, 2026-09-28. This is source-review provenance, not an execution pin. The preparation did not build or test the application; Codex must perform and accurately report the required validation on its actual checkout.
