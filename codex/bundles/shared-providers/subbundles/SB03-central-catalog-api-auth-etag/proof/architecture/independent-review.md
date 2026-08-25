# SB03 independent C# and Web boundary review

Result: `PASS`.

The independent review was performed against the stabilized source after the repair cycle. No
remaining source-level correctness or boundary blocker was found. The focused Release builds,
exact 18/14/10 selections, after CodeAnalytics snapshot, anti-stub audit, and secret/content scan
all pass; file hashes are centralized in `proof/hashes.sha256`.

## Findings repaired before freeze

- Strict enum parsing originally allowed numeric strings through `Enum.TryParse`; the reader now
  requires an exact defined enum name.
- OpenAI image publication was initially unreachable; OpenAI image plus Responses and ComfyUI
  image plus ChatCompletions are now the exact allowed transports. Azure remains rejected.
- Required secret checking initially treated a non-null GUID as sufficient. Publish and query now
  require a non-empty existing `SecretRecord`; actual save paths reject missing targets.
- Secret deletion had no `ProviderProfile` FK/reference policy. Security now owns a typed deletion
  extension contract and stable mutation key; Workspace/AgentFramework saves and Delete share the
  same serializable/advisory scope. The deterministic interleaving proves no dangling reference.
- Real Workspace/AgentFramework/bootstrap save paths originally omitted strict publication
  metadata. They now call one canonical writer. A follow-up review caught default pricing rows
  being misclassified as suggested public models; only explicit preserved models are now written.
- Raw health text is now mapped to `Degraded`, `Available`, or `Unavailable`; exact `Healthy` is the
  only checked Available state, and the raw text is never serialized.
- Cache correctness originally risked local-observer-only trust. The query derives a stamp from
  persisted source/publication/profile state and current secret existence, then rechecks current
  eligibility for each host.
- `If-None-Match` review caught the framework parser accepting an invalid mixed wildcard/list; Web
  now applies the RFC 9110 wildcard grammar in addition to strict typed parsing.
- OpenAPI review caught missing header parameters/response headers. The endpoint transformer now
  describes `If-None-Match`, opaque access context, response ETag/cache/request-id headers, and the
  owned status set. Exported auth-scheme ownership remains SB11.
- A nullable warning in canonical suggested-model construction was repaired before the final
  source freeze.
- The actual host composition test resolves the concrete Http catalog and requires exactly five
  production connector/purpose rows, excluding scenario/process/import/fallback/audio/Azure.

## Confirmed design properties

- Workspace depends on Abstractions only; Http depends on Abstractions only; Composition selects
  Http; Web has no provider/upstream dispatch.
- Publication application checks the publication token and current eligibility, commits before
  observer/activity effects, and returns a stable public ID.
- The catalog is rebuilt only from published/currently eligible rows and serializes normalized
  public DTOs. The private routing index contains the internal target but is not exposed by the
  query snapshot.
- Public revisions and ETags depend only on the sanitized representation. Private URL, secret,
  configuration, note, and raw health changes do not enter the hash.
- Missing/wrong/expired auth, malformed conditional headers, controlled exceptions, and native
  versus OpenAI error paths are explicitly separated.
- The exact unit class remains 18 Facts after folding actual save, deletion race, OpenAI image,
  numeric-enum, dangling-secret, bounded-health, and cross-host cache coverage.

## Recorded constraints and downstream checks

- The warmed cache intentionally reloads persisted eligibility inputs to preserve revocation and
  multi-host correctness. A future optimization may add a transactional database change stamp,
  but must not regress to observer-only validity.
- PostgreSQL/multi-instance end-to-end behavior remains owned by SB07/SB12; SB03 proves the shared
  lock contract and deterministic unit interleaving, not the final three-instance deployment.
- Auth-scheme export remains SB11; SB03 owns endpoint operation/header/response metadata only.
- Any new connector descriptor, metadata classification branch, or catalog public field reopens
  policy/revision/ETag tests and the after architecture review.

## Final evidence assessment

- Unit, Web, and Integration Release builds completed with zero warnings and zero errors.
- Exact discovery/run results are 18/18 unit, 14/14 catalog/API, and 10/10 authorization.
- After snapshot `snap-20260825012213-a17e36ed` confirms 14 projects, 33 direct references, no
  project cycle, unchanged two module plus one type cycles, and no error finding.
- The anti-stub audit passed for 56 selected files and the secret/content scan found neither
  credential-shaped content nor forbidden private provider fields on the public surface.

The review authorizes SB04 progression while preserving the downstream ownership limits recorded
above.
