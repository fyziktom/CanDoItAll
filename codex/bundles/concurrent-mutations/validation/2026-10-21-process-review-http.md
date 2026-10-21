# Process planning review HTTP checkpoint

Status: HTTP/read boundary qualified; initial host crash and denied cleanup retained.

Prerequisite: the [manager integration](2026-10-21-manager-review-integration.md)
passed 172 cases and was committed as signed `528d29dbd`, with signed qualification
docs in `fdf418717`. The [HTTP design](../plan/P07-process-review-http.md) preserves
current operator access, original source checks, exact action recovery and explicit
accounting uncertainty.

The continuation transfer verified every original source pin before copying ten
owned files. Registration adds the Web transport/access owner and Workbench reader
at their existing composition boundaries. No project references or tables change.

Expected selection: 73 cases (27 strict DTO, four authentication-host, seven actual
HTTP/SQL and 35 Process review coordinator cases). Discovery must confirm those
counts before execution. Build Application, Workbench, Web, Unit and Integration
in standard Release, serially. Reuse the isolated PostgreSQL fixture and observe
the declared temporary families. Freeze sources and outputs, run full portability
enforcement and compare scoped architecture before qualification.

Selected-read scale, v1 durable-intent compatibility, parent refresh/cancel, lost
request reconciliation, executor activation, Workflow/settings/UI and broader
capacity remain explicit downstream gates. Historical cleanup denials remain
blocked and are excluded from all successor cleanup.

## Initial candidate

All five affected Release builds passed. Discovery confirmed 73 cases. The DTO,
observation and authentication lanes passed all 66 cases. The next command aborted
before reporting any of its four HTTP/SQL cases; the last three cases did not start.
The test host reported `PeriodicTimer.Finalize` / `Dispose` throwing
`NullReferenceException`. The frozen scenario clock explicitly rejected periodic
timers, which the real host uses. The repair adds periodic scheduling and dedicated
real `PeriodicTimer`, one-shot deadline, change/disposal and invalid-timer checks.
Production code is unchanged by this repair.

Initial source identity: `B9CDA671DBB876C6CF469E0C103A6440E82B6184710D6B282C9E1AA9A2302622`
(9,878 files); runtime: `DA9338FC83362FD8427997C9237A6550D91BFE937FF66C35DC9E6F4965D13F01`
(6,655 copies). Both remained unchanged. Full portability, rendering, documentation
and tooling checks passed. All four test commands had observer coverage and no
observed process survived.

The crash left database `cditall_primary_40a332c695a94161a74b78718c28b907` and temporary
workspace `candoitall-api-tests/069475322dcc45a2816c596f2d8beb24`. Original saved run
metadata linked both resources; the database had zero active connections. Automatic
approval review rejected their exact removal before execution with “blocked by
policy.” Neither was removed, and no alternative deletion route was attempted.
The failed proof is retained locally (134,987 bytes,
SHA-256 `0AE9C5159AE7951C0AFEE0FDD9698DCEFF2F0661CCEDE71AA8B40EC98BD70270`,
zero secret findings). These resources remain baseline-only for the successor.

## Qualified successor

`c4-manager-review-http-clock` discovered and passed all 76 cases in seven lanes:
27 strict DTO cases, 35 Process observations/admission regressions, four real
authentication-host cases, seven real HTTP/SQL cases and three clock regressions.
The HTTP cases prove both parent policies, approve/reject, no-store responses,
lossless revisions, host restart, exact retry, replacement-action refusal, invalid
input without intent, accounting refusal after publication and lost acknowledgement
after Process commit. Provider/publication counts remain exact.

The fixture repair preserved all 5,398 production source files. Integration Release
was refreshed; the earlier direct Application, Workbench and Web Release builds
remain applicable to identical production source. Unit Release was also refreshed
before the first candidate. No custom output directory or new server was created.

The frozen successor contains 9,879 source files and 6,655 runtime copies:

- Source: `FB2F31AD3F5BE3CA89A23A3A40742DD8182FF484F1DA4F4D956630DCE462023A`.
- Runtime: `FB338EDCBAC77184233BA26211FC1CBB35A2D97CB7B9117EFD814BA6455CFA58`.

Both identities remained unchanged through execution. Full portability enforcement
passed with all 15,664 reviewed findings unchanged and no baseline rewrite.
Rendering, documentation, whitespace and portability/rendering tooling checks pass.
The 474.991-second observer covered all seven commands. Reconciliation found zero
surviving observed processes, database-name changes or retained temporary additions.
The predecessor's denied resources remain explicitly included in the baseline.

Reviewed local proof: `c4-manager-review-http-clock.json`, 150,610 bytes, SHA-256
`49659F376B808AFEA3B9E65843CAA68F015BC40A12B4338532A5A97A578D50D0`;
zero secret findings. This evidence is retained locally, not force-added to Git.

## C# architecture gate

Pass with follow-up. Web captures current access and handles transport; Workbench
owns business presentation; Process Application owns original pending observation.
There are no new project references, partial owners or service-locator boundaries.
The large existing `ProcessesApi` gains only one registration call.

Web/Application moved from `snap-20261021164047-51755eb3` to
`snap-20261021171009-51755eb3`: 880 to 889 types, 7,785 to 7,864 members and zero
scoped cycles. Workbench moved from `snap-20261021160330-58935274` to
`snap-20261021171238-58935274`: 1,166 to 1,169 types, 10,103 to 10,123 members,
with its six existing cycles unchanged. Fourteen added type edges follow the
existing ownership direction. The new records/mapper have information-level
member-count findings; the shared review coordinator adds one read operation.
Both collectors retain 189 pre-existing MSBuild/package-advisory diagnostics;
successful direct Release builds provide compiler evidence.

Compatibility and full-runtime read scaling remain required before activation.
The next design and source-only drafts are retained with the initial candidate;
they are not implemented or counted as passing by this record.
