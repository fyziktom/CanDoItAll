# Files: collection browsing is not direct editing

Sources S16-S19. Preserve the actual semantic source collection, scope and content owners.

## Collection window

Extract the full floating collection window: title/context, source count and revision,
include subprojects, bounded search, compact list, empty/loading/error/retry, explicit preview,
back-to-files, download and supported preferred-application/containing-folder commands.
Project collection and node-specific collection remain distinct scopes. A projected parent
or subproject points to its real owner; a label does not authorize its files.

Keep existing collection constraints: native unique source IDs, compatible ordering,
MaximumScopes, source fingerprints, 50-row browse page and bounded search policy. At review
the coordinator budget is 32 containers, 2,000 inspected items, five seconds, one concurrent
request, 200 matches and 2 MiB retained data. Preserve policy unless a separately measured
bug justifies a bounded adjustment; do not hide failures by querying all files.

Every opened workspace, activation, content session and action carries its own captured
origin and input. Same Request.Identity is not a complete lifetime proof: include profile,
project lifetime, actual scope/source revision, actor and opening. Close/back/retry and a
new opening cannot publish from the old operation, even A-B-A with identical labels.

Audit current Open/Preview/action catch branches, snapshot callbacks and after-await cleanup:
some check the operation only on success; others dispose a shared field and then clear it
without confirming it still denotes the acquired instance. Detach the owned resource first,
retire its publication rights, await its cleanup and release its own cancellation source when
the operation unwinds. Cancellation sources must not be disposed beneath still-running code.
Do not serialize every user interaction globally merely to avoid races.

A download or launch already accepted by its native owner is a real effect. Preserve its
outcome even if its window goes away; do not replay it when the UI later reopens. Capture
the actual action/target/capability map offered to that row, not the current file with the
same name. Native FileTools authorization remains authoritative.

## Direct known-file interaction

This native route differs from browser activation. It can resolve Edit for a supported
storage/driver, obtaining an actual SaveTarget, yet initially opens in View. Preserve the
allowed View/Edit transition and refusal of unsupported Diff. A read-only source stays
read-only. Save checks the exact FileTools file handle and expected content revision;
propagate the actual persisted revision back to the real FileInteraction. A mismatch,
expired grant, read-only target or concurrent content edit must not become silent overwrite.

Move the real FileInteraction markup, status/mode UI, supplemental notes, metadata-edit
button, local actions and dirty/saving/conflict close guard. Keep its actual registered
composition and byte limit. A pending Save cannot be described as discarded or rolled back
by closing the overlay. Discard is local draft disposal, not deletion of stored content.
Old close/save/state/mode callbacks must not operate on a successor interaction.

The renderer receives narrow neutral requests/content capability/typed callbacks; it must
not reference ProjectStructureKnownFileInteractionCoordinator, EF or native storage drivers.
Native wrappers can adapt the existing session without exporting its implementation graph.
Do not create a second file-save owner or expose raw unrestricted paths.

## Notes and Mermaid

Supplemental notes comparison is an authorized content read; losing authority must not
fall back to treating Notes as the file's contents. Preserve the existing explicit comparison
rules, cancellation and error distinctions. The real governed .mmd route uses FileInteraction
and native content bytes. A legitimate legacy Mermaid-in-notes view may stay supported with
strict rendering and no HTML labels, but its reachability must be independently established.
No unsigned iframe, arbitrary URL, active Markdown HTML or unguarded path replaces it.

## Native proof, not simulated grants

Use actual scope resolution, activator/session factory/releaser/save target. Verify byte
hashes before and after edits, exact persisted revisions, original vs successor handle,
released-grant denial and two independent instances. Cover late ordinary exceptions,
cancel, source replacement, permission loss, clean/dirty/saving close, invalid revision,
partial/unavailable collection and maximum-budget search. Test local-open via a harmless
owned allowlisted process on a supporting host; container lack of desktop launching is not
permission to omit desktop behavior or mark an unexecuted test passed.
