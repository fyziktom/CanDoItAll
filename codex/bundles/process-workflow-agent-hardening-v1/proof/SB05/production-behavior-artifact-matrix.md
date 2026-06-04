# SB05 Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Negative-test citation |
| --- | --- | --- | --- | --- |
| Executor side-effect descriptor | Runtime executor descriptors, plugin manifest descriptors, and bundled plugin descriptors | Workflow definition validator, runtime invoker, plugin manifest validator, catalog consumers, and future UI | Declared with each executor; defaults to `None`; email download executors use external read; mark-processed executors use idempotent processed marker | `failing-first-unsafe-retry-policy-mutation.txt`; `unsafe-retry-policy-restored-tests.txt` |
| Processed-marker record | Gmail and Office365 mark-processed payload builders | Workflow output consumers, scheduler replay diagnostics, and process evidence review | Produced for preview and commit modes; contains provider, message id, source marker, processed marker, and idempotency key | `failing-first-gmail-duplicate-mutation.txt`; `gmail-duplicate-restored-test.txt` |
| Idempotency record | Gmail and Office365 download and mark-processed payload builders | Workflow retry policy, scheduler guidance, and downstream duplicate prevention | Provider-scoped keys are included in root payload and run context for selected messages; mark-processed records tie commit receipts to the message id | `failing-first-unsafe-retry-policy-mutation.txt`; `failing-first-gmail-duplicate-mutation.txt` |
| Executor availability state | Gmail and Office365 executor descriptor factories | Runtime invoker, workflow catalog, and future canvas availability display | Availability is calculated from workflow grants and OAuth grants; unavailable execution throws with the descriptor attached | Unit policy/descriptor tests in `unit-side-effect-policy-and-manifest-tests.txt`; source assertions |
| External side-effect receipt | Gmail and Office365 mark-processed payload builders | Process evidence review, workflow output consumers, scheduler replay diagnostics, and future red-team tests | Preview receipts report dry-run/no mutation; commit receipts report actual controlled fake-client mutation state and idempotency key | `email-plugin-client-tests.txt`; `plugin-preview-simulation-tests.txt`; `failing-first-gmail-duplicate-mutation.txt` |

## Dependency Smoke Proof

- SB07 can display executor availability and side-effect level from descriptor metadata without deriving it from executor ids.
- SB08 can run email workflow scenarios with deterministic preview outputs and controlled fake-client commit proof.
- SB09 can red-team duplicate processing, unsafe retry policies, dry-run mutation attempts, and receipt schema drift using deterministic validator and client tests.
