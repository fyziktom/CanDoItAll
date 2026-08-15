# Acceptance evidence — SB10

- [x] An API client cannot choose or spoof stored conversation origin.
- [x] Authorization-enabled hosts enforce distinct read, manage, and execute policies.
- [x] Authorization-disabled trusted-local hosts preserve documented local behavior.
- [x] No API/SSE error exposes prompts, system instructions, credentials, or raw provider failures.
- [x] OpenAPI exposes versioned transport DTOs and stable links, not domain or EF entities.
- [x] Future chatbot concerns remain a separate documented deployment boundary rather than dormant definition fields.

## Required semantic proof

- Intended case: exact read/manage/execute JWTs can perform only their assigned route families; a normal
  no-origin conversation create persists `Api`; an Authorization-header read token reaches SSE lookup;
  operation responses carry v1 schema and canonical links.
- Negative/race/crash/failure case: broad/wrong scopes return 403; `origin: application` returns 400 and
  creates no row; query JWT returns 401; idempotency conflict excludes both messages/fingerprint; prompt
  and raw exception content do not cross response/log boundaries.
- Why the old implementation would fail this proof: it bound caller origin and authenticated only the
  parent API group, so broad `api` returned 200 and every authenticated token reached all LLM Chat routes;
  operation snapshots were unversioned and raw exception log overloads/system-prompt response remained.
- Exact source owner: Workspace owns scope names; Web policy/route metadata owns bearer authorization and
  transport DTOs; Web supplies API provenance; product commands preserve trusted provenance and stable
  failures; EF remains authoritative persistence.
- Exact command(s): expected-red exact auth test; two affected Web builds; 12-case focused API union;
  exact product origin test; exact PostgreSQL persisted-origin test; source guards; before/after
  CodeAnalytics; bundle validator set.
- Actual result: 12/12 API plus 1/1 product plus 1/1 PostgreSQL pass; final build 0 warnings/errors;
  zero cycles/blocking diagnostics; all source/bundle validators pass within budget.
- Evidence artifact: `bundle://proof/SB10/manifest.md` and its transcripts/invariant/hash artifacts.
- Commit SHA: `ebb8deae5f2deb0a379875fecf853ea8fc423be7`.
