# Planning review and HTTP action candidate

Candidate `c4-product-planning-review-api` follows signed checkpoint `e5c2d3506`.
It adds an ephemeral, current-authorized review to execution detail, including completed
and cancelled planning runs. Core retains exact expectations and action availability;
Workbench describes only original task content. Open work exposes no remaining budget
estimate or mutation action. The generic saved-effect continuation is absent for planning.

The HTTP endpoint requires WriteAgents, bounds the complete body to 8 KiB, rejects
unknown/duplicate members and validates the route before resolving the workspace owner.
Revision strings preserve positive Int64 values. It uses the existing activity admission
and exact saved action; refusal or a lost response never grants retry permission.

The scoped architecture snapshot is `snap-20261019110006-165f3713`: Models 138 documents,
Core 239, Workbench 386 and Web 169. Dependency/finding/inventory queries succeeded after
the full snapshot/dashboard response exceeded the tool IPC size limit. No new cycle,
project reference, service registration or execution-service partial was added. Existing
Workbench and owner/lease cycles remain; review records are data, not new owner services.
Direct Models/Core/Workbench/Web Release builds passed. The initial integration test
build found one fixture token-scope collection mismatch, repaired before discovery.

The mandatory portability scan found one intentional case-policy addition: ordinal
case-insensitive duplicate JSON member rejection. It has no filesystem or host-culture
dependency. The reviewed baseline adds exactly that finding (15,611 total); fresh
no-write enforcement is required. No scanner rules were relaxed.

Proof selection: five Unit cases for planning and historical revision contracts, and 25
Integration cases for initial entry, saved actions, waiting/completed read revocation and
two real HTTP tests. The API tests share their explicit environment/database leases with
the production planning fixture; they use the existing observed `candoitall-api-tests`
family and isolated PostgreSQL server. No new stack or ordinary-instance resources.
Native controls, further runtime policies, remaining writers and wider closure stay open.

The first candidate passed all five Unit and 20/25 Integration cases. Two action-review
failures exposed an overly broad classification: a pending prepared proposal has the
default Unknown effect value before dispatch. Review now recognizes only the exact
undispatched pending shape; entered/uncertain work stays blocked. Two read-revocation
tests incorrectly retained AllowAllProjects, which normalizes to read access; the repaired
fixture removes all grants and asserts the persisted result. The HTTP fixture also now
uses a distinct operation id for each distinct stale action, preserving duplicate checks.
The real HTTP publication case passed in the original candidate.

The [retained failed proof](../proof/2026-10-19-planning-authority/c4-product-planning-review-api.json)
has SHA-256 `249174380DE33045D792ECB95A6BB49A1AE49321E3171CD1A54EDF284156B5C3`.
Final verification matched 9,355 source files and 6,436 runtime files. The 252.902-second
observer covered both commands, with no remaining observed process or new database.
Six unowned temporary deltas were preserved; prior cleanup blockers remain. The freeze
was released before repair. The successor also checks OpenAPI revision strings and
current-read refusal/no-store behavior on both HTTP detail routes after publication.

The repair passed 29/30 cases, including all original failures and OpenAPI. The shared
agent-scoped projection helper caught typed read denial as InvalidOperationException
and returned 404 before the new outer mapper could run. Move the existing typed error
mapping into that shared helper, retain its ordinary unknown-run 404, and disable caching
for every scoped evidence projection. The HTTP case now checks all eight detail/evidence
paths after revocation. No new authorization rule or dependency is introduced.

The [repair proof](../proof/2026-10-19-planning-authority/c4-product-planning-review-repair.json)
has SHA-256 `354C23CCA71C16BE5A35023D452FD3426FBE3349B4C16313950073153C83B909`.
All 9,355 source and 6,436 runtime identities matched. The observer covered both commands
for 271.397 seconds; no observed process or new database remained. Seven unowned temporary
deltas were preserved. The freeze was released before the HTTP-only repair.

The HTTP-only successor passes both affected cases, including all eight read-denial
paths, OpenAPI, exact approval and duplicate dispatch. Web and Integration Release builds,
discovery (2), portability enforcement (15,611), rendering register, docs and diff gates
pass. The unchanged production bridge covers 5,230 files; only AgentsApi changed from
the preceding candidate, whose other 29 cases passed. This is combined focused evidence,
not a claim of a new 30-case run or broad-suite closure.

The [passing HTTP proof](../proof/2026-10-19-planning-authority/c4-product-planning-http-read-repair.json)
has SHA-256 `4B46F57005524F351AF872268FDCDDF59CA54BA729A0EC5A77A899D5A7999330`.
Final identities match 9,355 source and 6,436 runtime files. The 59.001-second observer
covered the 46.037-second test command. No observed process or new database remained;
four unowned temporary deltas were preserved. The freeze was released after proof retention.
