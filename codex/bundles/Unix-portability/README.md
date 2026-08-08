# CanDoItAll Unix Portability Program

This package updates and supersedes the supplied `CanDoItAll-linux-portability-codex-bundle-2026-07-31` for the current `development` architecture.

## Prepared source anchor

- Repository: `fyziktom/CanDoItAll`
- Branch: `development`
- Commit: `62ea8ee0cc42c1c06da934d126a5c18f8237a89f`
- Commit message: `Merge branch 'maf-refactor' into development`
- SDK: `.NET 10.0.302`
- Prepared: `2026-08-08`

The previous bundle was anchored at `d44faef347be128eb85856a18c6fe253ce6fc1ee` with `.NET 10.0.200`. The current branch was 64 commits ahead and included the MAF refactor merge, the dedicated Processes stack, process-driver projects, and `Security.Abstractions`.

## Program decision

The deliverable is one ZIP with **two sequential, independently executable Codex bundles**:

1. [`01-core-portability-foundation`](bundles/01-core-portability-foundation/README.md) — paths, slash/config cleanup, filesystem semantics, storage/control-plane migration, secrets/key protection, composition, headless hosting, and the first Windows/Linux/macOS CI gate.
2. [`02-runtime-tools-process-drivers`](bundles/02-runtime-tools-process-drivers/README.md) — process execution primitives, Workbench runtime nodes, Manager, MCP, external tools, plugins/FileTools, and Processes-domain capability adaptation.

The runtime bundle is deliberately prepared now but blocked until **Core Gate C4** succeeds on an exact commit. Its first subbundle, `B00`, must rebase every source reference after the core work lands.

## Why the split is mandatory

The current source no longer supports treating portability as one uniform OS-switch task:

- Core changes migrate persisted path and protected state. A defect can make existing data unreadable.
- Runtime changes span MAF Core, Workbench presentation, Manager supervision, MCP, plugins, and Processes semantics.
- The latest architecture explicitly assigns process semantics and recovery to `Processes`, not to MAF or a generic Infrastructure service.
- Core support should be able to stabilize headlessly before optional desktop/runtime dependencies are enabled.
- Codex 5.6 Sol xhigh can execute large plans, but model capability does not remove migration, ownership, or rollback risk.

## First use

From the extracted program directory:

```text
python ./scripts/validate_bundle.py --bundle-root . --stage portable
python ./scripts/materialize_bundle.py --bundle-root . --repo-root <absolute-path-to-CanDoItAll> --output-root <materialized-bundle>
python <materialized-bundle>/scripts/validate_bundle.py --bundle-root <materialized-bundle> --repo-root <absolute-path-to-CanDoItAll> --stage prepared
```

Then execute only `bundles/01-core-portability-foundation/subbundles/00-anchor-baseline-and-current-inventory`.

Do not start the runtime bundle until `bundles/01-core-portability-foundation/reviews/CORE-C4-HANDOFF.md` is completed and Gate C4 is GO.

## Non-negotiable execution rules

- One subbundle at a time; one explicit gate result before downstream work.
- Re-anchor and repair this bundle whenever `development` differs from the prepared commit.
- Preserve unrelated working-tree changes. Never reset, clean, or overwrite them.
- Add failing-first tests or named characterization evidence before implementation.
- Use typed arguments for direct process execution. Shell text is only for an explicitly modeled script language.
- Do not introduce a broad `IPlatformService`, a second process stack, an insecure secret fallback, automatic `sudo`, or process-name-only termination.
- Persist logical paths with `/`; never normalize arbitrary physical paths, URLs, or opaque scripts as logical paths.
- Keep process-domain semantics in `Processes`; MAF remains a generic execution adapter.
- Keep all source-code comments in English.
- Do not commit, push, or open a pull request unless the operator explicitly requests it.
- Redact every generated artifact.

## Package navigation

- [`EXECUTIVE-SUMMARY-CS.md`](EXECUTIVE-SUMMARY-CS.md) — Czech human summary
- [`PROGRAM-SEQUENCING.md`](PROGRAM-SEQUENCING.md) — phases and gates
- [`CURRENT-DEVELOPMENT-DELTA.md`](CURRENT-DEVELOPMENT-DELTA.md) — changes from the old bundle
- [`ARCHITECTURE-BOUNDARIES.md`](ARCHITECTURE-BOUNDARIES.md) — target ownership model
- [`CODEX-EXECUTION-CONTRACT.md`](CODEX-EXECUTION-CONTRACT.md) — executor rules
- [`shared/findings-register.csv`](shared/findings-register.csv) — prepared findings
- [`shared/source-reference-manifest.json`](shared/source-reference-manifest.json) — exact source evidence
- [`shared/rebase-protocol.md`](shared/rebase-protocol.md) — how to update the plan on a newer checkout
- [`scripts/`](scripts/) — validation, materialization, scanning, baseline, and artifact redaction helpers

## Preparation limits

This package was prepared through current GitHub source inspection and comparison with the supplied ZIP. A local checkout was not mounted in the preparation environment, so repository build/test commands were not reproduced here. `A00` and `B00` make local and actual-host reproduction mandatory before implementation.
