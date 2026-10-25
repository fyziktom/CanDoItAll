# Runtime result codec compatibility

The [planned extraction](../plan/P04-runtime-result-codec.md) is implemented in the
internal MAF result codec. The former runtime context delegates capture, restoration
and disclosure decoding; its result responsibility shrank by 112 net lines. No project
reference or public serialization contract was introduced. The single explicit type
mapping preserves the historical nested CLR wire identity, enum values, envelope
version, package fingerprint and committed/pre-dispatch evidence.

Release Unit and Integration builds passed. The first candidate passed 136 integration
cases and 17 of 18 unit cases. Its negative type-identity fixture attempted a raw string
replacement, which missed the JSON-escaped `+`. Changing the parsed `Shape` property
corrects the fixture; no production repair was needed. The successor passed all 18
selected unit cases. Its source/runtime bridge confirms unchanged production inputs
and the preceding 136 integration passes remain applicable; they were not rerun.

Final portability enforcement passes with 15,604 protected findings. Nine moved MCP
codec findings were reviewed and relocated in the baseline; its 14,084 allowance count
is unchanged. Documentation, rendering and diff checks pass. CodeAnalytics snapshot
`snap-20261019031558-d550e953` reports 517 documents, 1,902 types, the same four type
cycles and no new project reference/cycle. The 189 diagnostics are existing package
advisories. There are no new non-advisory compilation errors.

Both candidates verified unchanged frozen source/runtime and zero remaining observed
processes or database-name delta. Unowned temporary paths were preserved. Exact proof:

- [Initial candidate](../proof/2026-10-19-planning-authority/c4-result-codec-validation.json), SHA256 `8892713A69DDFCC2C016EC7E1300BF7418E0AF7E2FF604A36310A924991E4D55`.
- [Fixture repair](../proof/2026-10-19-planning-authority/c4-result-codec-fixture-repair.json), SHA256 `8237FDE0FAD0B56D682932CD277FCCB961D188CEC43D714DA2BE275F2F23A32A`.

Architecture gate: pass for this extraction. Tests exercise historical data and the new
owner without constructing the old runtime; actual invocation/replay and host-failure
tests cover the delegating path. This does not activate planning policy or saved-effect
product actions. Those remain the next runtime work.
