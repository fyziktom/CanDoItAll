# Semantic Invariants SB05

- No shallow-pass proof: Scheduler typed fields are rendered from persisted workflow `InputParameters`, not hard-coded for one template.
- No live Office365/Graph dependency in automated tests: option and validation coverage uses component stubs and local browser state only.
- No silent external write approval bypass: SB05 only configures schedule input and does not dispatch or mutate Office365/project data.
- No duplicate project output on retry: unchanged in this UI slice and deferred to SB06 idempotency proof.
- Raw JSON remains available as an advanced synchronized editor; typed fields do not hide the execution payload.
- Required-field validation is bidirectional: clearing a previously valid email removes it from JSON and prevents save.
- Code comments must be in English.

## Closure Evidence

- Component proof: `bundle://proof/SB05/transcripts/component-seed-and-scheduler-after-sb05-final.txt`.
- Browser proof: `bundle://proof/SB05/browser/scheduler-office365-watch-browser-proof.json`.
- Source proof: `bundle://proof/SB05/transcripts/source-assertions-scheduler-typed-input-after-sb05-final.txt`.
