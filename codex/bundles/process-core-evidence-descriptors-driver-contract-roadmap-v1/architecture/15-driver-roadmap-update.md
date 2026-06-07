# Driver Roadmap Update

## Current Decision
- Production driver implementation remains deferred.
- The next driver-related bundle should implement prerequisites as tests and policy documents before any runtime surface exists.

## Required Lane Order
1. Convert permission modes and denial reasons into executable tests.
2. Define audit persistence shape and redaction policy.
3. Define sandbox and command policy for any future execution-capable lane.
4. Select one verification-only lane with no mutation capability.
5. Only after those prerequisites pass, consider a production alpha.

## Candidate Ordering
| Candidate | Next action | Reason |
| --- | --- | --- |
| .NET/Rust transcript verifier | Best first candidate after prerequisites. | High value and can remain readonly over existing build/test/proof artifacts. |
| Business-analysis gap reviewer | Second candidate. | Useful but needs stronger evidence ownership around business records. |
| Office evidence reviewer | Later candidate. | Must prove no Graph calls, mail mutation, task creation, or document mutation. |

## Denied Until A Later Bundle
- Production process-driver registry, pack, selector, provider, runtime, dependency-injection registration, manager command, shell execution, Graph execution, workspace writes, storage writes, process mutation, claim mutation, transition mutation, finalizer application, and retry scheduling.
