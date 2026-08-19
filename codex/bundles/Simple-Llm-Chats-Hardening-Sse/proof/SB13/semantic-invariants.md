# SB13 semantic invariants

## SBI-13-01 — a final gate must validate the selected dependency graph

- Expected behavior: one package-mode restore feeds the one Release solution build, one stable filtered
  solution test, and one hosted Windows/Linux/macOS matrix at the same commit.
- Disallowed shallow implementation: using sibling source locally while claiming package-mode CI
  readiness, or supplying an unreviewed private package only to the local runner.
- Result: blocked before restore because the exact Spreadsheet 0.1.18 package returns HTTP 404 from the
  only configured feed.

## SBI-13-02 — known prerequisite failure does not consume a single-shot gate

- Expected behavior: preflight detects externally missing dependencies before the single expensive run.
- Disallowed shallow implementation: knowingly dispatching a red restore/CI matrix and rerunning after
  publication, which would exceed the bundle's one-run budget.
- Result: no restore, solution build/test, or hosted matrix was run in SB13. Their budgets remain unused
  for a resumed gate after the external prerequisite is satisfied.

## SBI-13-03 — static closure remains exact-head evidence

- Expected behavior: documentation, source/SSE architecture, traceability, test policy, and bundle
  checksums remain valid at the final candidate.
- Result: all static validators pass at `dea90cfd4cc77e60f1a7d07a2dc16d44165840f9`;
  all bundle Critical/High implementation findings are closed by their owning subbundle proof.

## SBI-13-04 — dependent work remains locked on a red FINAL

- Expected behavior: UI, shared-component isolation, Project Structure context, and enterprise
  deployment work unlock only after the stable gate and same-commit hosted matrix pass.
- Result: FINAL is Not Ready and unlocks nothing. Publishing the exact package, or committing a reviewed
  dependency-source correction, is required before resuming SB13.
