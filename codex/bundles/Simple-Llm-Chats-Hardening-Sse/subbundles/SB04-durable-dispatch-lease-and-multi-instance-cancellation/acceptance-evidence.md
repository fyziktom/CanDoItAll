# Acceptance evidence — SB04

For each criterion, provide behavioral/source evidence rather than only a test count.

- [x] Two independent service providers sharing PostgreSQL race the same operation; exactly one claim succeeds.
- [x] HTTP returns `202 Accepted` before the controlled provider is released; cancelling the request token does not stop completion.
- [x] A second database context commits cancellation and the current owner's next bounded heartbeat observes it.
- [x] Unit proof removes any local registration while a remote lease is live; reconcile/abandon do not infer orphaning.
- [x] Fake-time tests reclaim expired pre-dispatch work with a new epoch and reduce expired post-dispatch work to `RecoveryRequired`.
- [x] Admission with no registered executor returns typed retryable `503 DispatcherUnavailable` without a durable write.

## Required semantic proof

- Intended case: admission durably queues one exact turn, a registered dispatcher claims it, maintains
  the lease, invokes the provider outside the request lifetime, and finalizes only while the same
  runtime profile and execution lease remain current.
- Negative/race/crash/failure case: competing claims, remote cancellation, missing local registry,
  expired pre/post-dispatch leases, unavailable dispatcher, request disconnect, and host/profile loss.
- Why the old implementation would fail this proof: the pre-SB04 API call remained blocked inside
  provider execution and timed out instead of returning admission; ownership and cancellation were
  process-local and could not fence a second host.
- Exact source owner: `LlmChatExecutionLeaseService`, `LlmChatOperationDispatcher`,
  `LlmChatOperationExecutor`, `LlmChatOperationTransitions`, and
  `DatabaseProfileLlmChatExecutionLeaseHeartbeatStore`; composition only hosts the dispatcher loop.
- Exact command(s): recorded in `proof/SB04/transcripts/01-red-request-lifetime.md`,
  `02-unit-and-build.md`, and `03-postgresql-api.md`.
- Actual result: historical 0/1 expected red; final Unit 15/15; PostgreSQL lease/cancellation 2/2;
  request-disconnect API 1/1; final build and model checks pass.
- Evidence artifact: `proof/SB04/manifest.md`.
- Commit SHA: `7389daff6c21a4568895e514debe110434908d67`.
