# P24: Workspace policy editing through the generated SPA client

Status: qualified, 2026-10-23; see [P24 validation](../validation/2026-10-23-workspace-policy-client.md).
All ten backend cases, 100 SPA tests and ten rendered states pass. P23 is signed as `2e130acbf`; its 251 distinct backend
cases and SPA `0fa527a` qualify public planning selection. This slice closes the SPA
Workspace policy editor and its exact acceptance contract. It does not activate new
Process/Workflow policies or complete the remaining engine owners.

## Discovered prerequisite

The actual P23 OpenAPI document points both V1 and V2 Workspace PUT 200 responses at
the same `Accepted` schema. V2's generated response therefore drops `schemaVersion`
and `decisionDataPolicy` from the accepted snapshot. The existing HTTP test covers
the request and GET shape but never inspects the PUT acceptance. The runtime V2
response is already correct. A hand-written client cast would conceal the defect.

Give only the nested `WorkspaceSettingsSaveOutcomeV2` variants owner-qualified schema
identities through the existing Web OpenAPI registration. Keep unrelated names and V1
contracts unchanged. The owning schema helper defines the naming rule. Use the framework's
[schema reference customization](https://learn.microsoft.com/aspnet/core/fundamentals/openapi/customize-openapi#customize-schema-reuse)
for every other type. No domain, persistence or project-reference change is needed.
Extend the actual OpenAPI test to inspect V1 and V2 acceptance and all V2 policy fields.
Regenerate the SDK from a new isolated full export; never patch generated output manually.

## State, behavior and ownership

The existing SPA Workspace session owns the original V2 snapshot, six-field draft,
explicit policy draft, validation and write state. The existing page renders shared
Card/Input/Select/Button controls and forwards edits. A small pure policy codec translates
human-readable seconds to exact .NET duration ticks without JavaScript floating-point
conversion. Literal typed option values keep wire enums separate from visible labels.
The generated SDK owns transport shape; the existing bounded one-attempt adapter owns
request/response limits and the 30-second write deadline.

Load preserves explicit legacy absence; it never installs a policy automatically. An
operator may choose to configure a suggested StepSnapshot/RequireCurrent/PauseForReview
policy with ten-minute bounds, then review/edit it before saving. V2 saving requires a
complete valid policy. Existing saved policies retain every field, including exact
100-nanosecond duration precision. Unsupported versions or values are visible errors.

Freeze the original namespace/head, all six values and all policy fields before dispatch.
Disable the actual controls, including portal-backed selectors, while saving. Double
submission must not make a second attempt. Exact acceptance installs its returned head,
values and policy, including secondary diagnostics; do not follow it with an unrelated GET.
A conflict, refusal or unknown acknowledgement preserves the draft and blocks blind retry.
Explicit discard/reload obtains a new editing basis and does not prove an earlier unknown
outcome. No persistent browser storage is introduced without an actor-bound recovery
design; existing mounted-page draft retention remains an explicit limit.

The settings describe defaults for new supported decisions. Explain that existing runs
retain their admitted policy and individual producers can refuse unsupported choices.
RunSnapshot means one starting basis; StepSnapshot means a new basis at each supported
step boundary. Replanning is bounded and requires a positive count. Other change responses
require zero replans. Current publication authority remains enforced by the backend.

## Composition and cost

At the supported 1920x1080 desktop viewport use two editor columns: current defaults/notes
on the left, policy controls on the right. Keep the primary Save action visible and one
page scroll owner. No dashboard metrics, drawer, new navigation or responsive redesign.
Inspect normal, legacy-absence, validation, pending, accepted, conflict and unknown states,
plus each open selector for readable options, layering, clipping and action reachability.

The editor adds no polling, catalog scan, new server, package or dependency. One current
read loads values and policy coherently; one conditional write submits both. Parsing is
bounded by the maximum TimeSpan length and Int64 tick range. Provider work and database
locking remain unchanged. Reuse Release outputs, the isolated PostgreSQL server and the
existing schema probe. Record ownership of one temporary SPA preview before starting it
and close its browser context/host after validation. Preserve ordinary 5032.

## Validation and progression

Proof tier: Behavioral, with actual generated-client and compiled-browser behavior.
Backend scope: all five `WorkspaceSettingsV2HttpTests` plus all five
`WorkspaceSettingsHttpOutcomeTests` (10 cases), including actual SQL/HTTP acceptance,
stale heads, V1 compatibility, permission refusal and the repaired OpenAPI response.
Build Web and the owning Integration assembly serially in standard Release. Confirm
discovery before executing. Run full portability and reviewed no-write enforcement.

SPA scope: add generated SDK acceptance cases for both read modes with fractional ticks,
conflict and 503 with exactly one request. Add pure duration/enum/budget validation and
session cases for legacy absence, accepted normalization, frozen payload, no automatic
reload, original-condition conflict and retained unknown outcome. State exact discovery
counts before execution; reject zero or unexpected discovery. Run the affected app
typecheck/build and owned formatting/lint. Generation changes a shared public client, so
run all generator/client tests once at the frozen candidate after focused checks pass.

Use the compiled app with controlled HTTP at the large desktop viewport, inspect open
selectors and screenshots, and capture exact submitted V2 payloads and request counts.
This is frontend integration plus separate real backend HTTP proof, not a live combined
deployment claim. Broad Stable is reserved for the final engine checkpoint. Sign the
backend and SPA checkpoints separately; SPA stays on **codex/concurrent-mutations** with
no main merge or push. Record the repaired prerequisite and remaining Process launch UI,
broader policies, durable browser recovery and engine-wide closure honestly.
