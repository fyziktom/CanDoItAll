# Evidence templates

Copy these templates to a task-owned evidence directory. Do not fill the sealed copies inside this
bundle. All groups start NOT_RUN. Add original attempts with exact test identity, command, source
fingerprint, artifact hashes/references and actual counts. Keep failures alongside follow-ups.

For passing `kind: test` attempts record positive expected/actual discovery, executed/passed/failed/skipped
counts and a method-level expansion note when discovery and execution differ. A passing review/build
attempt needs its actual command/action, fingerprint and artifacts as well. Private originals remain
private; reviewed exports must state their scope. A path/hash is provenance, not proof that a tool ran.

The validator checks structure and integrity only. It cannot authenticate artifacts or infer product
correctness. Completion checking is stricter than ordinary validation and requires successful acceptance
groups, exact source revisions, actual signed checkpoints and no unresolved in-scope product blocker.
Local-only dependency delivery must stay explicitly qualified; it is not remote publication.

G30 may document a passing *accounting review* of a qualified mixed broad attempt. Retain the broad test
as FAIL and all original counters. Do not use this convention to mark an unresolved behavior group green.
The round-trip checklist requires native fixtures and concrete comparison assertions; it is not itself
an executable graph validator or evidence of preserved data.
