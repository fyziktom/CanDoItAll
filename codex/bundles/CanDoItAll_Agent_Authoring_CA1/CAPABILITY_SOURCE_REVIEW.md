# Current capability-authoring review

These observations concern the still-unextracted authoring family, not defects attributed to
PP3. They are source-derived risks to characterize with failing-first tests. Do not manufacture
an incident, change a proven invariant merely for consistency, or assume every observation
requires a new abstraction.

## Existing useful protections

CapabilityDetailsDialog and the wizard already have owner cancellation, masked notifications,
per-request linked tokens, save busy guards and deep-copied save payloads [S09-S12]. Existing
child tests assert that canceled request tokens remain usable until the request unwinds [S18].
Native capability updates carry ExpectedFingerprint; missing update identity and duplicate
canonical kind/key are rejected by the actual catalog mutation [S14]. Verify is a separate
publication path. Keep these protections and the existing A2 parent recovery tests [S19].

## CA-R1: setup outcome belongs to an input snapshot

Details and wizard store toolSetupResult/mcpSetupResult without a configuration revision. The
UI displays Setup passed for the current kind; changing command/endpoint/input does not itself
establish a fresh test. TestSetup also passes the live editor model to the flow [S09,S11,S15].
The native service compiles a descriptor before invoking real external process/HTTP or MCP;
do not misdescribe those operations as harmless pure reads.

Reproduce: run a held setup for configuration A, edit fields or switch kind/transport/input,
then complete A successfully or with masked failure. Check current status, diagnostic ownership,
notification and any saved data. Preserve the result as labeled historical or retire it, not
silently present it as proof of B. Capture the entire request before the first await and track
its editor/kind/config/input identity. Retrying a read or reopening UI must not repeat setup.
Also test A-B-A, close/reopen, active cancellation, timeout after external acceptance and two
independent editors. Native diagnostics and invocation effect classification remain authoritative.

## CA-R2: save outcome and later input

The current dialogs catch broad save errors, display an unconfirmed message and then clear busy.
A returned save ID is only handled while the original view is current, and success closes the
dialog even if other editable fields changed while waiting [S09-S10]. Characterize real event
ordering before changing it. Retain the intended operator experience while preventing loss of
newer text, repeated Create after a confirmed write, and blind replay of an unknown result.
A real CapabilityCatalogRejectedException is a known rejection, not a fabricated unknown.

Important: the native current SaveCapabilityAsync treats model.Id != null as an UPDATE and
rejects a missing ID. model.Id == null creates and the owner assigns its ID [S14]. Copying PP1's
candidate-ID scheme without changing the correct native seam is wrong. If typed outcomes are
needed, add the smallest owner-specific operation/result consistent with existing store guarantees;
keep existing API and curator/non-UI callers compatible. A lookup by nonunique display name or
kind/key alone is not proof that this exact attempted create committed. Unknown remains blocked
until actual owner evidence establishes its outcome.

## CA-R3: typed/raw configuration is one meaningful draft

There are typed mcpState/skillState/toolState objects, rawConfigurationJson and a separate tags
list. PrepareEditorForSave normalizes and mutates them before all errors have been resolved.
In the raw-editable branch it assigns rawConfigurationJson from the model even after rejecting
invalid JSON [S09,S23]. Confirm supported raw-editable kinds on the current checkout; retain their
invalid text and diagnostics instead of replacing it with the previous valid JSON.

Preserve valid supported extension data and inline resources, plus intentional constraints and
removal of forbidden raw secret fields [S13,S24]. Do not fix validation by normalizing a bad
number to zero, coercing an unsupported transport or defaulting malformed saved typed JSON to
an empty successful configuration. For an invalid legacy document offer a truthful retained/raw
state with an explicit supported repair path. No new retired Memory capability support.

Round-trip MCP transport, hosted/server identity, endpoint/command/working roots, argument order,
allowed tools, environment/header bindings and approval mode. Round-trip skill file/inline/
registered modes supported by the current surface, inline resources and script trust. Round-trip
tool implementation/runtime identity, side-effect classifications, process allow-lists, HTTP
method/bindings, expected outputs, time and byte limits. Preserve values not displayed by this UI.

## CA-R4: upload and nested completion are not generic refresh

Wizard upload opens an IBrowserFile stream with the current 1,048,576-byte bound [S10]. The
uploaded text, filename and mode belong to that exact upload/editor lifetime. Test an over-limit
file, Unicode/BOM input, stream failure, late completion after manual edits/kind switch/close,
and a second upload. Keep byte limits and never execute uploaded text during preview.

Parent A2 behavior is already guarded [S19]: an existing-agent capability assignment can save
its entire unsaved draft; a new agent stages. A new capability whose parent catalog refresh fails
remains identified and is explicitly reviewed/assigned without recreation. Preserve this real
composition, as well as the global catalog's create/details and original cancellation callbacks
[S20]. Do not replace real DialogHost tests with directly calling a renderer delegate.

## Required owner map before changes

Trace definition create/update/delete, setup service, assignment, Verify, curator/tool callers,
profile scope and catalog store update to the actual commit. List intentional locks and supported
retries. Helpers can move or shrink, but do not duplicate runtime descriptor compilation or remove
native policy because the sandbox lacks a real external tool. Keep setup request content only in
its bounded active lifetime; report masked diagnostics and safe effect/identity metadata.
