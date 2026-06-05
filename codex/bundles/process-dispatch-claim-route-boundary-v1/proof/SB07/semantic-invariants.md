# SB07 Semantic Invariants

- Invariant ID: `SB07_INV_001`
- Source raw note: RN-001 and RN-003.
- Expected behavior: Service-facing execution-run selection wrappers preserve their existing method names and return the same blocking, current-attempt blocking, and recoverable execution-run results as the helper.
- Disallowed shallow implementation: Adding a helper while callers still depend on duplicated dispatcher LINQ, or deleting wrappers before all callers are proven.
- Failing-first test: N/A - non-critical wrapper parity proof; behavior is asserted directly against the helper and existing wrapper tests.
- Passing test: `bundle://proof/SB07/transcripts/sb07-wrapper-parity-tests.txt`.
- Changed source files: `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs`.
- Production assertions: `bundle://proof/SB07/source-assertions/wrapper-parity.md`.
- Red-team negative case: `bundle://proof/SB07/transcripts/sb07-anti-stub-and-scope-scan.txt`.
- Downstream dependency check: SB08 Gate B can assert parity without needing to inspect execution-client side effects.

- Invariant ID: `SB07_INV_002`
- Source raw note: RN-001, RN-003, and RN-004.
- Expected behavior: Transition, fresh recovery skip, and concurrent-session-busy wrappers preserve helper parity while keeping runtime proof service-only.
- Disallowed shallow implementation: Moving transition state changes or execution-client calls into the helper, or proving parity with UI/browser artifacts.
- Failing-first test: N/A - non-critical wrapper parity proof; direct parity assertions cover the wrapper contract.
- Passing test: `bundle://proof/SB07/transcripts/sb07-wrapper-parity-tests.txt`.
- Changed source files: `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs`.
- Production assertions: `bundle://proof/SB07/source-assertions/wrapper-parity.md`.
- Red-team negative case: `bundle://proof/SB07/transcripts/sb07-anti-stub-and-scope-scan.txt`.
- Downstream dependency check: SB10 and SB13 must keep transition/finalizer side effects out of selection helpers.
