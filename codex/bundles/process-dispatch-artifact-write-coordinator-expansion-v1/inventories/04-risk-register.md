# Risk Register

| Risk | Severity | Mitigation | Owning subbundles |
| --- | --- | --- | --- |
| Coordinator accidentally owns source semantics | High | Static scans and source review: coordinator must not inspect candidate/expectation matching logic | SB03, SB04, SB12 |
| Key format changes | Critical | Key parity tests before and after each migration | SB05-SB10 |
| Hard failures become soft warnings | High | Process mock tests must assert throw behavior | SB05 |
| Response text changes due to newline/path handling | High | Response projection content tests | SB09 |
| Provider-native browser modes collapse | High | Separate expected/discovered mode tests | SB10 |
| Completed decision artifacts get forced into storage path | Medium | Record-only helper tests | SB11 |
| Mobile/small/medium proof waste | Low | Proof path scan | All |
