# Sensitive-state and outcome contract

## Data inventory

| Data | Permitted lifetime and use | Prohibited destinations |
| --- | --- | --- |
| Safe API status, issuer/audience, admin username | Owner-projected display with availability state | Signing key/options tree or inferred authority |
| Account metadata and expected version | Current authorized page/editor; safe bounded outcome | Password hash, authentication revision binding or new implicit grants |
| Token metadata ID/kind/scopes/timestamps | Authorized page, exact operation and bounded receipt | Bearer reconstruction or treating label/subject as admin |
| Raw create/reset password | Active specific editor and minimal captured call | Receipt/history, logs, URL, component persistence, recordings, screenshots |
| Fresh bearer returned once | Original authorized disclosure surface only; explicit copy/dismiss | Token list/store, generic result ledger, inactive retained DOM, local/session storage |
| Runtime private token record or deployment options | Original backend owners only | Light UI/public state/sandbox imports |

Remove references when a sensitive editor/result retires. Do not claim cryptographic erasure
of immutable .NET strings. Avoid custom `ToString()`/serialization that can emit plaintext;
log action, safe target identity and failure category instead of arbitrary exception text.
A controlled fixture can compare secret values in assertions without capturing them as evidence.
AP02, AP03, AP07, AP08, AP16.

## Status is not the same as outcome

| State | Required treatment |
| --- | --- |
| Known pre-dispatch denial/invalid request/conflict/missing exact record | No write claimed; preserve appropriate input, clear disclosure when authority is gone; correction/reload explicitly available |
| Known stored result | Keep exact safe identity/version before subsequent reads; publish once to the matching origin |
| Stored result with secondary failure | Preserve the known result and safe warning; refreshing metadata never repeats the mutation |
| Unknown durable acknowledgement | Preserve redacted uncertainty, block automatic/conflicting replay and offer appropriate exact read-only inspection |
| Operation completed after UI retirement | Keep only permitted safe original facts; do not publish password/bearer or navigation into a successor |

For issuance, token creation and registry registration are distinct steps. A GUID generated
before registration is a candidate identity, not a confirmed credential. Return no bearer
as usable success if registration failed. If an ambiguous registration needs correlation,
expose only a redacted original candidate identity through a narrow owner seam. Never decode
or recreate bearer values in the renderer to compensate for a missing owner contract.

For accounts, store-returned success precedes later logging. Keep confirmed safe profile,
version and action when logging fails. Password hashing, durable-write acknowledgements,
cleanup failures and access checks are not all interchangeable InvalidOperationExceptions.
Do not use a blanket catch to convert every case to either Refused or Committed.

## Recovery without invented causality

Reading an account with the same username does not prove which create produced it. Reading
an exact ID/version is a current-state observation, not proof that a prior request completed.
Do not clear an unknown create and transparently retry. Explicitly open an observed current
account after checking authority/version, or explicitly revoke a known registered credential,
without rewriting the historical result. Re-enable must not resurrect old sessions; deleting
and recreating a username must keep distinct GUIDs. AP07, AP11, AP15.

Choose bounded in-session metadata receipts only. No new durable command journal, retry daemon,
password archive, generic state dump or global cache. When capacity is exhausted, report it;
never evict pending/unknown operations just to allow a duplicate. Keep separate known committed
facts and optional failed observation state, so a failed read cannot turn a confirmed commit
back into Unknown.

## Authority retirement

Check permission at every real owner action. The renderer's enabled state is only UX.
A caller/access epoch change invalidates active sensitive disclosures and pending presentation
continuations. The composing host owns real authority observation; the sandbox supplies a
controlled safe stand-in at the same boundary, not a production bypass. Check the epoch both
before work and before publishing results. Cancellation is not rollback.

Keep the existing trusted-local and validated-HTTP administrator rules. A PostgreSQL profile
change may retire a Settings view, but API account storage remains the same instance-local
control plane. Do not borrow another database's owner, clear persisted accounts or treat a
same-looking ID as global. Do not add a claim or a client boolean that supplies administration.
AP10–AP12.

## Evidence hygiene

Use task-owned disposable control-plane/account/token roots and generated fixture credentials.
Masked screenshots and safe metadata proofs only. Redact credential-bearing request/response
bodies, browser traces, console logs and exception messages before retaining artifacts. A
private isolated test credential is still a credential and should not be committed. Raw
responses may be asserted in memory but must not be dumped. Preserve test identities/counts
when redacting evidence. Do not pass the real user's credentials to an external service.
