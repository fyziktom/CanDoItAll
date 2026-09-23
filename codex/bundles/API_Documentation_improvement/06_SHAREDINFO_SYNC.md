# SharedInfo synchronization and canonical terminology

## Ownership decision

The user explicitly authorized work in both repositories. SharedInfo normally requires explicit adoption authorization for sibling edits; this request provides it. Product behavior/contracts remain product-owned. SharedInfo owns reusable standards, shared vocabulary orientation, API skills and the generated reference package. [S01, S02, S06, S07]

Suggested maintained locations, subject to existing conventions:

- `docs/standards/api-documentation.md`: cross-repository XML/OpenAPI documentation rules.
- `docs/architecture/candoitall-api-domain-glossary.md`: adopted product API/domain vocabulary and owner references.
- A companion machine-readable glossary in a maintained schema/data location if the repository has one.
- Product `docs/architecture/api-documentation.md` or a suitable existing document: how this product emits and validates XML/OpenAPI, with a link to the canonical glossary and a versioned adoption reference.

These are proposed paths, not pre-existing files verified by the review. Do not copy the entire preparation pack, test output, raw report or product code into SharedInfo. Do not maintain two independent glossary masters.

## Refresh the actual shared contract

The support package is `codex/skills/_candoitall-api-shared`:

- `references/candoitall-web.openapi.json`
- `manifest.json`
- `README.md`
- `references/partner-api-migration.md`

It currently records a September 15 pre-merge module-decoupling source, not the inspected development source containing task-update repair. Update it from an identified current final product build, not from hand-edited historical JSON. [S03]

1. Record final product commit, clean/dirty state, exact sibling revisions, relevant SDK/package versions and build/publish identity.
2. Start an isolated host from that exact build with synthetic/private data. Do not reconfigure or stop a protected user instance merely to acquire a customary port.
3. Capture both document routes on the same host. Verify version and equivalence, recording any deliberate normalization rather than disguising it as byte identity.
4. Calculate the real file hash, operation/path/schema counts and route-family totals from those bytes. Update the manifest and its README atomically within the SharedInfo change.
5. The inspected validator pins `http://localhost:5032/`. Use that port only when available and legitimate, or revise the server-provenance rule in a small reviewed change with tests and the actual capture URL. Do not edit servers metadata to misrepresent the capture.
6. If a dirty-source publication is unavoidable and explicitly accepted, record the baseline commit and a content/diff fingerprint. A plain status-file hash alone is weak evidence of exact modified content; prefer a clean committed source for the final public snapshot.
7. Classify structural drift versus the old snapshot. Separate earlier task-binding repair already in the baseline from new documentation metadata fixes in this run.

## Synchronize skills and guidance

Enumerate all maintained API skills and referenced files. Update task-update guidance, examples and source/operation sets in the Project Structure skill. Review the CRM/HR, Agents, Process, Workflow, Simple Chat, Shared Provider, Memory and any other discovered API skills for vocabulary and contract consistency. Do not invent package names that are not present.

Refresh task workflow guidance with raw string IDs, current/proposed preconditions, required nullable cost basis and owner-provided project admission. The examples must stand alone; do not require consumers to call private .NET domain helpers to construct HTTP bodies.

Review the Project Structure skill's HTTP fallback instruction. An independently authorized HTTP client can use its own allowed endpoint, but a runtime tool denial/missing grant does not authorize bypassing agent policy. Preserve the current authentication and operation permissions. [S04, P15]

Update the owner-contract document's stale snapshot/branch framing and link the vocabulary. Preserve historically meaningful evidence as historical, not as current operational instructions.

Route parity alone is insufficient: check request/response field descriptions, error envelopes, enum encodings, required/null semantics and source version. The API support package must install together with a selected API skill under existing installer rules; do not copy a separate spec into every skill. [S07]

## Validation and coordinated commits

Run the current forms of:

```powershell
.\tools\validation\Test-CanDoItAllWebOpenApi.ps1
.\tools\validation\Test-SharedInfo.ps1
```

Also run discovered skill-specific/parity validators and the new description-quality gates. Preserve existing provenance, route and hash checks. Do not weaken them because a refreshed artifact changes counts; update the legitimate manifest and assertions with evidence.

Create signed local commits in each repository and record which SharedInfo commit describes which product commit. Product source changes must precede the final snapshot provenance. Do not push, merge or deploy. The user can publish the mutually consistent commits afterward.
