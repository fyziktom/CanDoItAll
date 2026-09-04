# Shared implementation prompt

Implement only the current subbundle of `CDA-UI-SEAMS-AGENTS-01-v1`.

Read the shared base and all architecture decisions first. Preserve current visible and
URL behavior. Move the owned responsibility fully out of the Razor component; do not add
another partial, wrapper component, service bag, or forwarding interface. Reuse stable
existing models inside the new aggregate/session contracts. Keep host presentation at the
page/editor boundary and I/O inside the planned query/controllers.

Before editing, run the subbundle baseline discovery and capture failing-first evidence
for each new durable seam. After editing, build the changed production project, list and
run the exact focused tests, inspect the diff for out-of-scope changes, and update proof
and the execution report. Stop on an unapproved fourth interface, URL change, project
reference, or duplicate state owner.
