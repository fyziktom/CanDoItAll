# Semantic invariants SB01

## SB01-I1: branch and evidence scope are explicit

- Expected behavior: downstream hardening starts from known branch status and changed-file categories.
- Disallowed shallow implementation: proceed directly to product changes without branch or residue evidence.
- Positive proof: `bundle://proof/SB01/changed-file-scope.md`.
- Negative proof: `bundle://proof/SB01/residue-audit.log` searches active runtime/test source for SQLite provider residue.

## SB01-I2: proof artifacts are intentionally retained

- Expected behavior: tracked proof artifacts are kept because this branch uses bundle proof as its audit trail.
- Disallowed shallow implementation: delete proof artifacts to simplify diffs.
- Positive proof: retention decision in `bundle://proof/SB01/changed-file-scope.md`.

## Residual risk

Remote branch currency is only as fresh as the local `origin/development` ref because `git fetch origin` failed under the current SSH configuration.
