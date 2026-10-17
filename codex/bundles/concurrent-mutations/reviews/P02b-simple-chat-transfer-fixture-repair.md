# Simple Chat transfer fixture activation repair

Status: source reviewed and prepared on 2026-10-16; no build, discovery or execution yet.

The retained Stable Integration run reports six failures in
`LlmChatsDatabaseTransferIntegrationTests`. Every failure occurs at the explicit
dataset-activation precondition, before the transfer behavior under test. The
shared transfer helper deliberately refuses an unconfigured activation service;
that production-boundary refusal and the helper remain unchanged.

The fixture now assigns one stable resolved profile per database lease. Its six
transfer tests reuse that exact source/target identity instead of creating a new
profile on each call. Target activation uses a separately owned test environment
and the real `DatasetActivationCoordinator`; initialization completes before any
assertion expecting transfer rejection. Disposal releases the database lease and,
in `finally`, its activation environment. Ordinary non-transfer tests do not
allocate an activation environment.

The source-snapshot test retains its original interceptor and explicitly bound
owner session. It supplies the same activated target to that session; no source
read or transfer transaction is replaced. Existing foreign-lineage, invalid graph,
byte/row bounds, exact audit/history and isolation assertions remain intact.

Expected focused selection is the complete class: **7 Facts**, comprising the six
transfer scenarios and its existing usage projection case. No cases, retries,
timeouts or production behavior changed. Build-backed discovery must confirm
seven before execution; the mandatory portability gate follows final source.
