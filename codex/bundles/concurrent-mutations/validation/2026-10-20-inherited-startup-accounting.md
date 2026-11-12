# Inherited initial and recovered startup accounting

Status: 189 distinct startup-accounting behaviors pass; additional opening-boundary
regression subsequently closed before product activation.

The [accepted design](../plan/P07-inherited-startup-accounting.md) requires the exact
original allocation and explicit fresh-enrollment or existing-only entry. Core
records initial preparation as closed child work. Recovery starts local preparation
inside the child's existing live interval. Missing journals cannot restart under
recovery, and closed pre-provider startup failures settle measured work explicitly.
No Process, Workbench or persistence dependency enters Core.

Models, Core, Unit and Integration Release builds passed. The initial candidate
discovered and executed all 189 cases: 171 passed and 18 failed. Four failures came
from enumerating a default immutable provider-dispatch array after the expected
production outcome; fourteen came from the fixture adding a second participant
for ProjectTaskPlan after the real participant became registered. Production's
strict single-owner check remains correct and unchanged.

The repair changes two assertions and replaces the fixture participant registration
explicitly. Both direct classes and both fixture consumers repeat: 44 cases. The
remaining 145 passing cases are valid only under exact unchanged production source,
configuration, other fixtures and production outputs.

All 44 repeated cases pass. The exact comparison contains only the three test files
and three documentation files, with 666 identical production outputs. Together with
the other 145 passes this gives 189 distinct passing behaviors. Source contains
9,776 files, SHA-256
`801972272961FCF6503E0381E7E53C23B51B28420626B1CFBB9CDE3A472A62CA`;
runtime contains 2,917 files, SHA-256
`D1CA1CB6AD52C589ADD35B9CBC4304BCDAD4192CE3EDDCC7B93651C832003F0E`.
Both stayed unchanged throughout the four test commands. The 387.035-second
observer reports zero remaining observed processes and database-name changes;
14 unowned TEMP entries are preserved. Release build, full portability enforcement
without writing the baseline, documentation, rendering and whitespace checks pass.

Final proof: `proof/2026-10-19-planning-authority/c4-inherited-startup-accounting-final.json`,
88,973 bytes, SHA-256
`4344C797D2AA18CC382394BDE77DBA52514B193AB430E3F1F2A5C2FCFEB07AF8`.
It is secret-reviewed and includes the exact correction boundary and prior proof
identity. A pre-test bridge-helper check rejected a mistyped expected source hash;
the literal was corrected before any test, with no product or test source change.

Initial source: 9,775 files, SHA-256
`A4739A2BEACA23D1D7B80968C4677F262EBAD443953DA673692E5A8C22E6E524`.
Runtime: 2,917 files, SHA-256
`E135FF3F1DD967CA7FC6A9B0FF246A10F1B523BBB1DE37B5EAD390283632761C`.
Both remained unchanged throughout execution. The observer covered all 12 commands
for 505.426 seconds. No observed process or database-name delta remains; 53 unowned
TEMP entries are preserved.

Initial proof: `proof/2026-10-19-planning-authority/c4-inherited-startup-accounting.json`,
373,698 bytes, SHA-256
`CB357F138D4D707370A57D250628B0DE5F4D7DB9A428BC12F91208098F39A1C5`.
The proof is secret-reviewed and retains every failure, discovery, build and static
command with exact source/runtime/resource identities.

The full portability gate passes with 15,652 unchanged reviewed findings, without
writing the baseline. Documentation, rendering register and whitespace checks pass.
The repair must repeat the full scan/enforcement and these checks.

CodeAnalytics `snap-20261021003627-0b4ceac4` covers Core and Models: two projects,
one Core-to-Models edge, no project cycles and the same three existing type cycles.
The 189 existing workspace diagnostics remain; direct builds pass. One new
informational member-count finding concerns the cohesive 98-line execution
coordinator, whose 12 members now include measured recovery preparation.

Actual business binding, Process preflight integration, manager continuation and
waiting-step completion, finite-capacity qualification, Workflow occurrence ownership,
settings/UI and full writer closure remain open. Broad Stable retains its historical
17 failures. SPA stays on its own branch; historical cleanup denials remain explicit.

Review additionally identified remaining-time setup outside the coordinator's
startup catch. Budget expiry immediately after opening the durable interval must
settle known closed work, and cancellation must prevent callback entry. The next
bounded regression addresses this before activation; the 189-case result does not
claim that untested boundary is complete. The subsequent
[40-case candidate](2026-10-20-inherited-startup-boundary.md) now closes it.
