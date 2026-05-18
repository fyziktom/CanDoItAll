# Assumptions And Risks

## Assumptions

- The local API can continue using the PostgreSQL profile and database created during the realistic project validation run.
- The realistic project ids remain present in the active database during execution.
- The existing Cognitive Memory page is the correct UI surface for the Dialogue Workbench.
- A minimum repair path can reuse consolidation candidate review/application semantics instead of inventing a second mutation-application pipeline.

## Critical Path Risks

- If probe feedback cannot be transformed into a concrete consolidation or mutation candidate, UI feedback will remain performative and the user will still be unable to repair memory.
- If review approval is not connected to probe feedback repair candidates, the system will collect corrections but never improve canonical memory.
- If the workbench hides recall trace/source refs, the user cannot judge what memory knows wrong.
- If UI proof uses only synthetic records, it may miss the real failure mode from large project memories where many similar source slices compete.

## Validation Risks

- The app may be running and locking assemblies during test/build; stop or restart the API intentionally around build steps.
- Browser validation requires a working local server and a project id query string.
- The existing probe answer stores `ContextPack.Summary`, which is too small for a useful chat answer unless the UI also renders the recall context pack returned by the ask call.
- PostgreSQL state may contain prior probe records; validation scripts must create unique session titles or filter by returned ids.

## Reopen Triggers

- Reopen subbundle 01 if a probe correction review item still cannot create/update a canonical memory record after approval.
- Reopen subbundle 01 if high-risk corrections bypass review or feedback can directly mutate memory.
- Reopen subbundle 02 if the UI cannot ask a new question from the page or cannot show trace/source evidence beside the answer.
- Reopen the bundle if AI Tap or Curacao Glass validation passes only through API scripts but not through the browser-visible workflow.
