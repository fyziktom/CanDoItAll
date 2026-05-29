# Assumptions And Risks

## Working Assumptions

- The implementation target remains branch `processes-hardening`.
- Automated proof must use fake Graph clients or fake `HttpMessageHandler` paths and must not require live Office365 credentials.
- Scheduler UX should use the existing Blazor/Radzen/component patterns already present in the module rather than introducing a new UI stack.
- Raw JSON editing must remain available as an advanced or fallback path for workflows without typed parameter descriptors.

## Critical Path Risks

- Office365 filter syntax may be rejected by Graph when address, category exclusion, and lookback filters are combined; the executor needs a bounded fallback query.
- Idempotency must land before final scheduled retry proof, or retry after mark-processed failure can duplicate project outputs.
- Approval/preapproval work can become unsafe if it silently treats Scheduler launches as trusted external writes.
- Scheduler typed input schema must be durable enough to survive template loading and saved workflow definitions, not only render one form instance.

## Validation Risks

- Unit tests that only assert filled JSON fields or non-empty output are not enough for the email polling behavior.
- No-message proof must prove the scheduler treats the run as non-failure and does not retry.
- UI proof must show the Scheduler form can configure email/contact, project, parent node, category, and a two-hour interval without hand-writing JSON.
- Browser proof must use the real `/scheduler` and `/agents/workflows` routes with desktop and narrow viewport checks.

## Reopen Triggers

- Reopen SB02 if later template or scheduler tests show the executor does not preserve project/node/run context or idempotency context.
- Reopen SB03 if mark-processed can run before the project write succeeds.
- Reopen SB04/SB05 if Scheduler typed input does not round-trip into the saved `InputJson`.
- Reopen SB06 if duplicate project outputs can be produced for the same Office365 message id.
- Reopen SB07 if no-message runs update `LastError`, trigger retry, or become indistinguishable from failures.

