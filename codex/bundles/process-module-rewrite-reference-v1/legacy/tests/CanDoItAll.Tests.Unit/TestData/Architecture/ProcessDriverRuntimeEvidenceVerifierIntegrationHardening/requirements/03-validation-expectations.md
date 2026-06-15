# Validation Expectations

Required final proof:
- `dotnet build CanDoItAll.slnx --no-restore`
- full unit tests
- focused transcript verifier tests
- focused process adapter tests
- focused runtime evidence verifier tests
- architecture tests for Core/API/driver boundaries
- source scans for forbidden runtime tokens and dependency drift
- no UI/media drift scan
- anti-stub scan
- prepared-stage bundle validator
- completed-stage bundle validator

Critical gates require:
- failing-first transcript or source assertion,
- semantic positive proof,
- adversarial negative proof,
- anti-stub audit,
- proof manifest with changed-file hashes,
- raw-note closure.
