# Bundle utilities

These utilities use only the Python standard library, Git, .NET CLI, Bash, or PowerShell. They do not modify the CanDoItAll checkout unless an implementation subbundle later directs Codex to do so.

## Portable integrity validation

```text
python ./scripts/calculate_checksums.py --bundle-root . --verify
python ./scripts/validate_bundle.py --bundle-root . --stage portable
```

## Materialize repository references

Create a disposable materialized copy against the exact checkout:

```text
python ./scripts/materialize_bundle.py \
  --bundle-root <portable-bundle> \
  --repo-root <CanDoItAll-checkout> \
  --output-root <materialized-bundle>
```

A commit mismatch blocks materialization by default. `--allow-different-commit` exists only to run the mandatory rebase/re-analysis protocol; it does not authorize implementation.

## Portability inventory

```text
python ./scripts/scan_portability.py \
  --repo-root <CanDoItAll-checkout> \
  --output <artifact-root>/portability-scan.json
```

The scanner writes JSON, CSV, and Markdown summaries. Pattern matches are review leads, not automatic defects.

## Baseline evidence

Linux/macOS:

```text
./scripts/run-baseline.sh --repo-root <CanDoItAll-checkout> --output-root <artifact-root>
```

Windows:

```text
pwsh ./scripts/run-baseline.ps1 -RepoRoot <CanDoItAll-checkout> -OutputRoot <artifact-root>
```

## Secret scan

```text
python ./scripts/scan_artifacts_for_secrets.py \
  --root <artifact-root> \
  --output <artifact-root>/secret-scan.json
```

The scanner reports only redacted excerpts and truncated SHA-256 fingerprints. A finding returns a non-zero exit code unless `--report-only` is used for triage.
