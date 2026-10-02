# Provider Sharing PP2 — completion and final validation

Start with [prompt.md](prompt.md). This is a continuation of the existing PP2 assignment,
not Provider History PP3 and not a replacement of the completed provider profile editor.

The reviewed application commit is `9052b2a443a4fdce1ac24dd0ccfce236fd35ab74` on
`components-decoupling`. Its parent `df9b7c8e1b563d56fef762c38aad2a14089bd332` added the
historical PP2 package. The reviewed commit implements S0 fixes and its native container tests.
The maintained PP2 record explicitly leaves structural extraction and final consumers pending.
The commit is provenance, not an execution pin. Work on the operator's actual current checkout.

**Keep S0, repair the local-settings refresh issue, finish Sharing/source UI, then validate the
final sources and images. Do not deliver another S0-only checkpoint as completed PP2.**

Read [review](PP2_REVIEW.md), [credited evidence and gaps](S0_CREDIT_AND_GAPS.md),
[local-settings finding](R1_IMPORTED_LOCAL_SETTINGS.md), [architecture](SCOPE_AND_ARCHITECTURE.md),
[contracts and effects](STATE_AND_EFFECTS.md), and the validation/run/commit documents named in
`bundle.json`. The original 38 acceptance groups are retained, with one added local-concurrency
group. Current results belong in a copy of [the template](templates/evidence.json), never in
historical sealed bundles.

The new UI remains large-desktop-only. No paid external inference, push, merge, key export,
ordinary-instance fixture reuse, or historical-package cleanup is authorized. Local disposable
publication, credentials, synchronization and signed commits remain authorized and required.

The reviewer inspected repository source and recorded outcomes. No .NET, Docker, browser or
watch execution was performed by the reviewer. Package checks and supplied-data checkers do not
prove product execution. See [sources](SOURCES.md) and [package validation](PACKAGE_VALIDATION.md).
