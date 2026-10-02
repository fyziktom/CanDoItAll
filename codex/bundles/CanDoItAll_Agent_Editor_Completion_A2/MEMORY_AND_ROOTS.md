# Memory and external-root sub-boundaries

## Memory eligibility and editing

Current `AgentMemorySettingsPanel`/State (R06/R07) inject the application profile store and
installed driver enumeration. Eligibility currently means enabled + Healthy + exactly one matching
driver + a supported synchronous ContextQuery capability. Preserve these specific conditions in
the host/owner and project safe entries to the renderer. Do not use only IsEnabled, invoke a driver
for a preview, or copy secrets/transport options into a new UI DTO.

Preserve invocation Disabled/Automatic/ExplicitDirective; /mem alias meaning; automatic inclusion;
required/optional behavior; stable aliases; duplicate-alias/provider rejection; order; source-scope
flags. `CanUseMemoryTools`/`CanIngestSources` normalization belongs to a single policy implementation.
Where current state normalizes on parameter acceptance, characterize intended compatibility and
avoid turning arbitrary render/rebind into a repeated destructive write to a different draft.

Bindings and their add-row candidate have a lifetime. Same editor/section refresh preserves raw
alias, selected provider, requirement and errors. New editor resets only its own add-row data.
A parent Save must not implicitly Add an incomplete binding, and Enter in alias input must not
accidentally create a different intent. Read failures and retry cannot replace the whole agent.

R19's removal policy deliberately updates preferred/default/assignments/allow-list for the removed
provider; preserve case and identity rules and test hidden fields. Do not simplify remove to a
single list filter. Existing restricted/missing bindings remain visible with accurate availability;
no implicit substitution of a healthy provider. Preserve unsupported runtime refusals.

Async provider loads use per-editor/request cancellation and exact context. An old failure cannot
mark new references unavailable. Concurrent same-editor retries need an admission/generation, not
only a whole-session check. Do not repeat profile/driver enumeration for each input keystroke.

## External roots

R08/R09 currently combine substantial markup and `IExternalTargetPathRegistryFactory` calls.
Move the actual entry/list/error renderer, but keep native-path normalization, alias creation,
protected binding export and resolution at the host. Reuse current alias codec/normalization and
SelectedReferenceTable; no second token format or handwritten path policy.

A safe view can contain alias, approved display path, bound/unresolved status, removal capability
and validation text. Never put `ProtectedRootToken`, private configuration blobs or a complete
serialized agent into DOM attributes, receipts, logs or screenshots. A displayed path does not
confer runtime access. Use private task-root paths for proof, not the operator's real directories.

Test valid native task paths on the actual host, canonical aliases, duplicates, malformed entries,
saved unresolved bindings, same text/new editor, read-only/disabled states and delayed callbacks.
Preserve local candidate text across unrelated parent renders. Removing a selected root updates
its exact alias/binding association without touching other roots or storage choices.

Existing safe fallback behavior must be explicit and retain unresolved records. Failure to build a
registry must not manufacture a resolved binding or display a secret-bearing exception. Never
replace actual native handling with string-only examples and call that production proof.

## Sandbox separation

Use deterministic provider/catalog and root-display fixtures. Pure selection policy can be shared
if truly neutral; installed drivers and native root token issuers must not be registered merely to
render the sandbox. Simulated root acceptance is labelled synthetic and has a separate actual-host
integration control. The real neutral reference table and binding renderer remain the same.
