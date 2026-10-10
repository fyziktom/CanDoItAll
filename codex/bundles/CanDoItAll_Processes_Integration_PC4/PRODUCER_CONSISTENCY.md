# One executable selection across producer preparation and launch

## Required invariant

For a new intended launch, driver-derived variables, definition identity, compiled plan,
executor mapping and retained preparation must refer to the same captured effective
executable closure. Definition scope/authority must remain current at the real admission
boundary; capture is not permission. For replay, the retained preparation is authoritative:
no re-reading current definitions or recomputing variables before deciding whether it is replay.

The current HTTP gap is in `ProcessesApi.MapLaunchRequestAsync` ->
`ProjectStructureProcessNodeService.BuildProjectScopedLaunchVariablesAsync` ->
`CreateVariables` / `ApplyLaunchVariablePreparation`, not in the already corrected
`ProcessLaunchVariablePreparationService.WithDefinition` behavior alone.

## Implementation choices, not a forced framework

Resolve the canonical definition once after replay lookup and valid producer admission,
and pass its immutable/trusted selection through internal enrichment and into the native
request. Return the necessary captured value with the variables, or pass it into the existing
helper. Keep the public HTTP DTO independent of native executable content and authority.
`ProcessLaunchRequest.ResolvedDefinitions` is currently JsonIgnore; preserve that boundary.
Do not expose a deserializable 'trusted snapshot' that bypasses native capture or checks.

A public/legacy variables-only helper can remain as a compatibility wrapper when required,
but must not let the main HTTP path lose the selected closure. Do not create duplicate
selection policies for HTTP, Workbench and child launch. Reuse the native resolver and
canonical explicit identity checks. Audit all producer entrypoints using the helper.

Preserve original-input fingerprint and caller identity semantics. The raw producer input,
canonical internal selection and enriched variables are distinct values. Replaying a request
must not get a different fingerprint because a title, publication, profile, catalog, project
surface or driver setting changed. Changed input under the same intent remains a conflict.
Do not move project/node loads or current-publication resolution ahead of retained replay.

## Scope and input compatibility

Test DefinitionKey, DefinitionId-only, both consistent and both inconsistent, explicit unknown
identity, omitted key/id with configured profile/default, profile selecting another definition,
project+node, project without node and global. Keep the deliberate unknown explicit identity
refusal. Confirm existing wire trimming/case behavior and canonicalize recognized inputs once
before exact owner comparisons; reject ambiguous identities rather than guessing. Do not
case-fold database keys, fingerprints or arbitrary opaque identifiers as a universal fix.

A documented key precedence does not authorize bypassing contradictory explicit identifiers.
Align XML/OpenAPI text and coverage tests with the actual chosen safe contract, recording any
intentional tightened behavior. Do not re-enable 'unknown ID launches first template'.
Authored role/workflow bindings must not silently pick up a default live profile; verify
explicit profiles still behave as supported and incompatible profiles are rejected.

## Deterministic regression oracles

1. Use an actual known template with nonempty driver activations. Save/publish an override
   removing them. For HTTP project+node check and direct launch, capture the **actual context
   observed by registered contributors** and stored variables/plan. No template-only driver
   contribution may reappear. Do not use only an empty mock driver list.
2. Publish changed driver settings and bindings with distinctive sentinel values, and a
   different project override. Compare the resolved closure/source hash, stored variables,
   expected driver artifact references, plan and accepted assignment values across producers.
3. Hold new request A after closure selection/enrichment, publish B, then continue A. All of
   A must stay on its captured selection, or the entire request must be explicitly refused
   before acceptance under current policy. A mixed A/B preparation is never acceptable.
4. Repeat a retained check/launch after republish or node mutation. Count contributor calls
   and prepared/run identities: replay does not enrich again or allocate a second operation.
   Preserve existing replay after acceptance/archive and unaccepted archive refusal.
5. Retire/recreate the project or switch profile during the controlled boundary. Assert
   no accepted run/assignment is created in the successor lifetime, not only a displayed error.
6. A contributor failure/cancellation does not create a run. A lost postcommit response is
   recovered using the original intent and exact stored selection without repeating effects.

Use timing decorators around real services only to control scheduling or observe inputs.
They must not manufacture the expected closure, persistence, receipt or workflow result.
Add a real authenticated API case where an existing fixture supports it; preserve capability,
subject/expiry and local-operator distinctions. Do not implement a new auth subsystem.
