# Proof Template

Every critical subbundle must record:

- Changed files and before/after hashes.
- Exact wrappers moved or preserved.
- Side-effect classification.
- Passing focused tests.
- Failing-first or adversarial negative proof, unless explicitly N/A with justification.
- Source scans:
  - no Process Core
  - no production driver API
  - no MAF back-dependency
  - no UI/Razor/CSS/JS/TS files
  - no small/medium/mobile proof paths
  - no stubs/TODO/NotImplemented
- Downstream dependency check.
- Reopen triggers.
