# Native outcomes, compatibility and bounded repair policy

## One accepted operation is not one page render

Each family has its own opening, submitted snapshot, current mutable draft, expected owner
identity/version and actual result. Do not turn these into a global application operation
framework. Reuse existing concrete owner contracts, the current project admission and native
assignment/process mechanisms. New native result fields must reflect facts captured at the
commit/acquisition boundary, not guesses constructed by the renderer. [S15, S19-S21, S29]

| Operation | Facts that can become independently true |
|---|---|
| Quick create | Directory object created; optional activity/search follow-up; metadata read; original picker publication |
| Participant/meeting Save | Intended role replacement; native node metadata; projection/read-back |
| Create-and-use secret | Vault object created; safe metadata resolved; project reference created/updated; view refreshed |
| Runtime launch | Plan/admission accepted; process session acquired; early observation completed; URL serving; preview opened |
| Runtime Stop | Original session selected; termination requested/completed; exit observed; matching preview closed |

A view cancellation does not roll back any of those facts. A read-after-write failure does
not authorize replaying a create, role replacement, external launch or Stop of another process.
Keep historical accepted facts separate from a current attempt, with exact IDs and safe summaries.
Never include secret values, whole command environments or unauthorized directory details in
result records intended for display, logs or evidence.

## Concurrency and ownership

Expected source snapshots belong to the write they admitted. Do not combine old dirty fields
with arbitrary new concurrency tokens. Use real owner conflict/refusal results; conditional
assignment APIs must not silently degrade to unconditional writes. Capture mutable lists and
request values before an await. Only the completing operation may release its own UI gate.

Metadata/role operations must preserve unaffected fields, unknown JSON, correct affiliations,
primary flags and independent project defaults. Distinguish the database profile used by a
native owner from a selected future/startup profile; never fix a race by routing all old work
to the latest profile. Native authorities remain in their original module.

## Scope of allowed repairs

Small reproducible lifetime, mapping, callback, validation, capability or result-projection
repairs are part of WB5, including narrow native owner additions needed for exact results.
Update their affected callers and tests rather than leaving a fallback path without authority.

If reproduction proves a schema migration, global authorization change, cross-owner durable
protocol redesign or broad runtime-registry replacement is necessary, do not improvise it.
Produce a finding with current call graph, exact timeline, proven effects, minimum boundary,
consumer/test map and proposed follow-up. Continue safe independent WB5 work, but leave the
affected required group explicitly blocked; do not call that family complete. A large codebase
alone is not a reason to abandon the requested implementation.

## Compatibility proof

Inventory Razor descendants, dynamic dialog calls, type registrations, public moved values,
CSS/JS ownership and native consumers. Preserve former public identities with a justified
compatibility adapter/forward where needed. Verify real loaded types as well as project graphs.
A historical namespace does not itself create a dependency; an imported implementation assembly
does. No interface quotas, file-count targets or tests equating fewer lines with decoupling.

No production renderer may instantiate test fixtures. No sandbox may resolve the native module
through a service locator. No old branch may be removed merely because a new fake covers it.
