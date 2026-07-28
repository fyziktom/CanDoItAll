# Machine-Readable and Executable Assets

## Discovery

- `grep-discovery.ps1`
- `grep-discovery.sh`

Run before package edits. Output is written to `.artifacts/maf-1.15-discovery`.

## Validation

- `validate-bundle.py` — validates the extracted bundle structure and machine-readable files
- `validation-commands.ps1`
- `validation-commands.sh`

Validate the bundle itself after extraction:

```bash
python .codex/bundles/maf-1-15-upgrade-architecture/machine/validate-bundle.py   .codex/bundles/maf-1-15-upgrade-architecture
```

Run the repository validation scripts after restore/package edits and again at final closure. Output is written to `.artifacts/maf-1.15-validation`.

## Package Alignment

After restore:

```bash
python .codex/bundles/maf-1-15-upgrade-architecture/machine/check-package-alignment.py .
```

The script fails on unexpected versions and requires manual classification for unknown MAF package IDs.

## Structured Data

- `migration-tasks.json` — dependency-aware task graph
- `architecture-gates.json` — machine-readable A1-A4 progression gates
- `impact-matrix.csv` — migration impacts
- `workaround-register.csv` — keep/rewrite/remove decisions
- `package-baseline.json` — current and target direct packages
- `state-fixture-manifest.schema.json` — required fixture metadata
- `approval-decision.schema.json` — conceptual request-specific approval contract
- `optional-feature-register.template.json` — SB07 decision template
- `expected-package-versions.props.example` — minimal shared MSBuild properties

These files support execution and review. Production types should follow existing repository conventions rather than copying schemas blindly.
