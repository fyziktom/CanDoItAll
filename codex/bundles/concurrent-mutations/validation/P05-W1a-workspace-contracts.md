# W1a: dormant Workspace defaults contracts

Status: source authored in the managed continuation checkout on 2026-10-16. No build,
discovery, execution, static scan, database operation or runtime activation is claimed.
The main checkout and siblings remain frozen. This is the contracts-only prerequisite
of [the Workspace owner refinement](../plan/P05-workspace-first-owner-refinement.md),
not its enabled writer or caller cutover.

## Scope

Two new files in `src/Modules/CanDoItAll.Modules.Workspace.Contracts` contain immutable
settings values/head/expectation/snapshot/replace command/outcomes and one expectation
JSON converter. The project adds the reviewed direct inward SharedKernel reference for
`DatasetNamespaceId`. SharedKernel's project file has no project references. No new
project, Infrastructure/Projects dependency, interface or service registration is added.
An evaluated project graph has not run for this candidate.

The existing mutable six-field `WorkspaceSettingsModel`, its defaults and its Copy helper
are unchanged. `WorkspaceSettingsValues.Capture` freezes all six fields before an await;
strings are immutable and no mutable model is retained. `ToEditorModel` returns a fresh
editor object. Submitted non-null text, including blank/whitespace/lowercase values, is
preserved exactly. There is no normalization or default insertion in these contracts.
A provider selection is null or a nonempty Guid; constructor validation performs no
provider lookup. Owner validation/normalization remains W1b work.

The nonempty head contains row ID and revision. Expectation explicitly distinguishes
Empty from Existing and always contains its original dataset namespace. Empty requires
a null head; Existing requires a validated nonempty head. Enum zero/unknown states are
invalid. Snapshot and replace command require both their original expectation and values.

Accepted requires an Existing snapshot and may carry a typed activity, currency-publication
or logging diagnostic. The diagnostic cannot turn that snapshot into rejection or
uncertainty. Rejected has a validated typed InvalidValues/HeadChanged/DatasetChanged/
TransactionConflict reason. Unconfirmed is a distinct case with no invented operation ID,
receipt, retry instruction or replay horizon. These are application outcomes; no existing
HTTP route or native owner interface uses them yet.

## Wire decisions

The values object keeps the existing camelCase six-field names. All six fields are required,
including `defaultProviderProfileId`: explicit null clears it, while omission fails.
Null text fails constructor validation; a nullable annotation is not treated as runtime
validation. Unknown DTO members are rejected. The expectation has this exact shape:

```json
{"namespace":"11111111-1111-1111-1111-111111111111","state":"empty","head":null}
```

Existing uses `"state":"existing"` and `"head":{"id":"...","revision":"..."}`.
Namespace/state/head must all appear; null expectation, a missing/default/unknown state,
duplicate expectation or nested-head members and contradictory state/head combinations
fail. Namespace
uses the existing SharedKernel UUID-string converter. The command has `expected` and
`values`; the snapshot has `expectation` and `values`. Outcomes use the `status` discriminator
with accepted/rejected/unconfirmed variants. Existing standard Web enum serialization is
used for the two diagnostic/reason enums; no global serializer options are changed.

Ordinary immutable DTOs use constructor validation, private init properties and explicit
JsonInclude/JsonRequired/JsonPropertyName annotations. The nullable provider uses Never-ignore
so ambient null omission cannot accidentally change a complete replacement into omission.
Only the conditional expectation needs a converter. This avoids duplicating normal object
serialization in independent converters. In the .NET 10 runtime, required getter-only
properties otherwise depend on required-constructor option behavior; private init plus
JsonInclude makes the normal required-property path usable with current options.
See [required-property behavior](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/required-properties)
and the [versioned runtime property validation](https://github.com/dotnet/runtime/blob/v10.0.4/src/libraries/System.Text.Json/src/System/Text/Json/Serialization/Metadata/JsonPropertyInfo.cs).

W1b must still put bounded raw request/capture validation before allocations, reject a null
top-level command, reject duplicates elsewhere in ordinary DTO members, and translate constructor validation
and malformed JSON into the correct pre-dispatch HTTP refusal. This dormant contract is not
a streaming HTTP parser. Missing required fields and malformed expectation throw JsonException;
normal constructor violations use argument exceptions. No new durable or generated-client
wire compatibility claim follows before the actual endpoint/OpenAPI/client cutover is tested.

## Source-derived validation expectation

New file: `tests/Unit/CanDoItAll.Tests.Unit/WorkspaceSettingsMutationContractTests.cs`.
It contains exactly **15 rows: nine Facts and six InlineData rows**, no dynamic providers.

```text
FullyQualifiedName~CanDoItAll.Tests.Unit.WorkspaceSettingsMutationContractTests
```

The cases prove six-field capture without editor aliasing, raw-value preservation and
invalid null/provider inputs, nonempty heads, explicit state/dataset identity, Empty/Existing
golden wire and malformed states, every missing value field including nullable provider,
snapshot/command roundtrip, missing/null original evidence, Accepted-versus-Rejected-versus-
Unconfirmed separation, and unknown-member rejection. Serialization uses actual System.Text.Json
with normal Web options; the expectation test also checks different naming/null-ignore options.
These are pre-execution expectations, not results.

An independent source review found no authority, immutability, required-field or accepted-
head defect. Its nested-head duplicate-member concern was repaired within the one existing
expectation converter; the malformed-input Fact now includes duplicate row ID and revision.
The count remains 15. That review performed no validation execution.

Parent-owned direct Workspace.Contracts/changed-producer builds, exact discovery, focused
execution, evaluated inward-reference checks and portability enforcement remain required.
W1b entity/current-head migration, ordinary writer, bootstrap/transfer participation and all
callers remain unchanged. W1c durable recovery and P07 policy defaults remain separate gates.
