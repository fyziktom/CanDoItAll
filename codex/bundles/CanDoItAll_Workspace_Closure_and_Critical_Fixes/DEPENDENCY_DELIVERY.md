# Coherent Components delivery

## What was actually inspected

Application review SHA: `d9273a88973d28d73c4d29f8686f2b3d68ef8f3a`.
The previous validation consumed Components `f258ab6a959a97fa16c01d0858e7dc122728a11a`.
The queried branch inventory contained main at that SHA and development at
`ff5289746573a6dd6456754a7764844627ddd49f`, but no Components `components-decoupling`
branch. Recheck actual refs; branch metadata is not an immutable future promise. (META02)

Current app CI selects Components using `github.base_ref || github.ref_name` and records
the resolved commit as an output shared by its jobs. It does not expose a static
`CANDOITALL_COMPONENTS_COMMIT` setting. FileTools has a separate explicit pin. (R30)
A bare push to app `components-decoupling` is not selected by the current automatic
main/development push filter. A manual same-branch run without a matching Components ref
can fail dependency acquisition before tests; that is not a test pass or product failure.

## Required result

Record the exact repaired source pair, dependency mode, evaluated graph and asset inputs.
An uncommitted sibling patch or a test result using different Components sources cannot
certify this application. Keep a manifest such as `templates/dependency-delivery.json` in
the ignored proof output, with meaningful actual values rather than the template's nulls.

The bounded sibling authorization covers Dialog/DialogInterop and their owner tests/assets.
Use a task branch or the user's explicitly chosen working branch, preserving unrelated edits.
Read that repo's current instructions. Make signed scoped commits when allowed; verify their
signatures and tree contents. Do not rewrite commits or disable signing while awaiting pinentry.

Do not automatically publish packages, push/merge repositories or change CI's branch policy.
The ordinary safe delivery is a recorded Components fix committed for the appropriate target,
followed by application validation consuming that exact source and an operator handoff that
publishes/merges the dependency before the dependent application. If an explicit dependency
selector is genuinely required, treat it as a separate small reviewed build-policy change,
not an invented pre-existing parameter or silent fallback to main/latest.

## Verification checklist

- Confirm source-mode package-to-project replacement and exact assembly/static-asset origins.
- Build and test BaseLib at the corrected SHA, including disposal, nested dialog, module race,
  source/package standards and its own mandatory gates.
- Build the application and all impacted sandbox hosts with that same SHA. Run consumer
  component/browser tests against real BaseLib, not an assembly left in another bin folder.
- Compare protected Workspace graph and watch sets. No feature leaf acquires a backend or
  unrelated feature implementation. Zero unresolved references/cycles.
- Verify source and a fresh published Production host's dialog assets. If package-mode proof
  is claimed, restore a package built from the corrected source into an isolated feed and
  record its version/hash; a source-mode browser does not establish package-mode execution.
- Recheck the actual intended CI target resolution. Record whether the corrected immutable
  dependency is available to that path. Do not trigger remote CI or uploads without permission.

Separate `LOCAL_SOURCE_PAIR_VERIFIED` from `REMOTE_CONSUMPTION_VERIFIED`. Pending publication
is a delivery action, not a reason to destroy a good local fix; it must remain explicit in
the final readiness assessment. A local result is not evidence that another checkout/CI
runner will resolve the same dependency. The closure validator requires an explicit verified
delivery disposition for unqualified next-module readiness, not a Boolean guessed by the agent.
