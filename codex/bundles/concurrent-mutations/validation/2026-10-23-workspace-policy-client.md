# Workspace policy client qualification

Status: qualified, 2026-10-23. [P24 design](../plan/P24-workspace-policy-client.md)
closes the SPA Workspace decision-policy editor and its generated V2 acceptance contract.
The backend baseline is signed P23 `2e130acbf`; the SPA baseline is `0fa527a` on
**codex/concurrent-mutations**. The qualified SPA checkpoint is signed as `5b86b89`;
its exact staged tree and parent are verified, and its worktree is clean. No SPA main
merge or push is authorized or performed.

## Contract defect and repair

The actual P23 OpenAPI export mapped V1 and V2 Workspace PUT 200 responses to `Accepted`.
Its V1 snapshot shape omitted the V2 schema version and decision policy, so the generated
client discarded both from real V2-shaped responses. Two generated SDK acceptance cases
failed before repair; both conflict/unconfirmed cases passed. That four-case failed
candidate is retained in `sdk-before-repair.log`.

Web's existing Workspace schema owner now supplies distinct reference identities for the
three nested V2 outcome variants. The existing OpenAPI registration installs that rule;
other types use the framework's default naming. No runtime writer, permission check,
database schema, project reference or business partial changed. The existing HTTP schema
test now inspects both PUT acceptance bodies, every policy property, and refusal/unknown
discriminants in addition to its original request/GET checks.

The full isolated export preserves all **317 routes**, adds only the three V2 outcome
components and removes none. It has **4,530,921 bytes**, SHA-256
`642d8744fbf1e0e5e7a22f2b02c8c95a1695ccc5c93de3246302efb63b9c604f`.
The SDK is regenerated from those pinned bytes using installed repository tools.
Generated output was not manually patched and no dependency installation was used.

## Editor ownership and behavior

The existing Workspace session owns the original V2 namespace/head, all six defaults,
the policy draft and write state. The page renders the existing shared controls. A pure
typed policy codec uses integer ticks for exact duration conversion, accepts only known
enum/schema values and enforces the replan-budget relationship and Int64 duration limit.
Legacy absence is visible; Configure creates an editable suggestion but performs no write.

One bounded attempt freezes all proposed fields with their original condition. Inputs
and portal-backed selectors are disabled during the write. Exact acceptance installs the
returned values, head and policy without a current GET. Secondary diagnostics remain
visible. Known rejection, malformed acceptance and lost acknowledgement preserve the
draft and prohibit blind replay. Discard/reload is explicit and does not resolve an earlier
unknown result. Draft retention remains scoped to the mounted page; actor-bound durable
browser recovery and Process launch selection remain separate work.

## Executed checks

Proof root: `.artifacts/concurrent-mutations/validation/c4-workspace-policy-client`.

| Check | Actual result |
| --- | --- |
| Web and Integration builds, standard Release/local libraries, serialized | Pass |
| `FullyQualifiedName~CanDoItAll.Tests.Integration.WorkspaceSettingsV2HttpTests.` | 5 discovered, 5 passed |
| `FullyQualifiedName~CanDoItAll.Tests.Integration.WorkspaceSettingsHttpOutcomeTests.` | 5 discovered, 5 passed |
| Generated client tests, including actual bounded SDK calls | 12 passed |
| Generator tests after the changed public export | 25 passed |
| App Node tests, including 28 policy-codec cases and 11 Workspace session cases | 63 passed |
| Full app typecheck and production build | Pass; existing large-chunk warning retained |
| Owned formatting and lint, whitespace checks | Pass |
| Full portability, rendering register and tooling checks | Pass; 15,689 reviewed executable findings unchanged |

Build/test arguments, exact discovery names, result hashes and durations are retained in
the command records and `run-summary.json`. The final full scan covers 10,077 files and
47,392 findings without truncation. Enforcement ran without writing the baseline. This
change introduces no reviewed baseline delta. Broad Stable was not repeated; its final
engine checkpoint and historical failures remain open.

All **1,231 backend source pins and 14 outputs** remain unchanged through both HTTP topics
and the full schema export. SPA's **11 changed-source identities and 124 compiled files**
remain unchanged through browser execution. The remaining documentation edits only record
closure. The narrow schema-rule change adds no dependency edge or construction owner;
existing pure codec/session tests require no backend host. Actual SQL/HTTP tests qualify
the transport and current permission boundary. The scoped C# architecture gate passes.

## Browser and cleanup

The compiled SPA passed ten states at **1920x1080**: legacy absence, configured, each of
three open selectors, invalid budget, pending, accepted, conflict and unconfirmed.
The scenario submits both snapshot modes, exact fractional durations, original/accepted
revisions and the preserved provider value. It observes **three writes and two reads**;
the second read occurs only after explicit discard/reload. No provider or ordinary 5032
request is made. This is frontend integration with controlled responses plus separate
real backend SQL/HTTP proof, not a live combined-deployment claim.

The first screenshots caught selector fade-in frames. The same compiled candidate was
rechecked after animations settled; both behavioral runs and their separate images are
retained. Reviewed normal/unknown and all three settled open overlays have readable
labels/options, correct foreground placement and no clipping. The two-column editor has
one page scroll owner, no horizontal overflow and a visible Save action ending at y=1056
even with the unknown-result explanation. Screenshots are under
`output/playwright/concurrent-mutations-spa-p24-20261023/settled`.

The observer covers all three runtime commands; final reconciliation finds zero live
owned processes, database-name delta or temporary-family residue. Initial observer setup
failed before any test/export fixture because its adjacent ownership file was absent.
The existing earlier in-directory record was mirrored with an explicit before-fixture-use
state; the helper verifies its ordering against the saved baseline. The first source
method enumeration also refused a nested helper before build; Fact-qualified enumeration
then confirmed the expected ten cases. Neither setup failure is counted as runtime proof.

Both owned browser contexts closed. The single preview exited normally after its stop
marker. Standard build outputs and reviewed schema/proof are retained; the isolated
PostgreSQL server is reused by subsequent lanes. Historical denied cleanup targets remain
unchanged and prevent a whole-bundle cleanup claim.

Both owned bulk portability files were removed after preserving their hashes, summary and
no-write enforcement evidence. Artifact review covers 116 text files with no password
sentinel finding, oversized omission or unreadable file. Its three lexical findings are
the private file-read and literal variable expressions in the runner. The browser scan
excludes the 20 PNG files, which were reviewed visually; it is not image-content scanning.

Behavioral closure: **Pass for the P24 editor and acceptance contract**. The signed
checkpoint identities are recorded after commit. Broader Process/Workflow behavior,
durable draft recovery, remaining mutable owners, sustained capacity, recovery activation
and final engine E2E remain open.
