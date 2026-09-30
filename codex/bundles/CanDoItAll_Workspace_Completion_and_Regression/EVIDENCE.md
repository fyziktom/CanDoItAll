# Evidence model and final report

## Preserve proof layers

The handoff's Python tests prove only its own bookkeeping and structure. Source review proves only inspected code properties. Sandbox tests prove scenario/renderer behavior. Deterministic actual-owner tests prove real persistence/policy in controlled conditions. Production browser tests prove shipped composition and interaction. Live tests additionally require real provider activity and owner effects. These layers complement one another and do not substitute for missing layers.

Current-source conclusions and implementer-reported 325-case results are separated in REVIEW.md. None of the initial campaign results is already Passed.

## Working evidence directory

Keep raw output under a uniquely named ignored `artifacts/workspace-completion/<run-id>` and safe UI media under an equally unique ignored output directory. Store short maintained summaries and reproduction instructions in repository docs. Do not commit secrets, full payload traces, massive raw logs, `.pid` files or transient databases. A sealed evidence excerpt may be retained only under the repository's existing documentation policy.

For every campaign group, preserve attempts in order. Record result status, mode, HEAD and tree checkpoint hash, exact command/filter/selected subcases, expected/actual discovery and execution counts, exit code, elapsed observation, environment IDs and artifact hashes. Use a safe group manifest to connect its multiple tests. Logs must identify operator/test/runtime layers without passwords, raw tokens or connection strings.

A pass requires the required oracle, not a screenshot alone. Put the actual oracle assertions, original target identities and read-back evidence in a hashed manifest. For live results include actual provider/model, `execution=live`, nonzero counted model requests and original persisted run/tool/owner facts. Old live evidence and closed-gate or rehearsal results are not valid new proof. [WS22, WS24]

## Campaign bookkeeping tool

Copy `templates/campaign-results.json` to your evidence directory. Each `attempts` array is initially empty. Fill real attempts; do not edit the sealed plan to lower requirements.

```text
python <bundle>/tools/validate_campaign.py --plan <bundle>/campaign-plan.json --results <evidence>/campaign-results.json
python <bundle>/tools/validate_campaign.py --plan <bundle>/campaign-plan.json --results <evidence>/campaign-results.json --evidence-root <evidence> --require-complete
```

Without `--require-complete`, validation checks structure and reports incompleteness; it is never a product closure claim. Complete validation requires all required groups passed at a declared valid checkpoint and hashes for referenced evidence. It rejects a live group with only runner success or rehearsal, a deterministic result labelled live, missing owner/UI evidence, duplicate/missing group IDs and a latest failed attempt hidden by an older success. It cannot independently determine whether a JSON statement describes genuine execution; a human still reviews the cited evidence and test code.

## Required deliverables from Codex

Return one concise Czech summary with two explicit decisions: Workspace UI completion and application regression readiness. Keep all maintained project docs, prompts, code comments and UI strings in English. The report links:

1. final Workspace per-surface/per-file disposition and dependency map;
2. exact completed stages, source/sibling checkpoints and graph/watch measurements;
3. campaign table with PASS / FAIL / BLOCKED / NOT_RUN / REHEARSAL, not a single inflated total;
4. live provider usage, actual UI/owner proof and unmet prerequisites;
5. small fixes and their failing-first/passing evidence;
6. complex findings with attribution confidence and future repair-bundle inputs;
7. original broad results and every required revalidation after changes;
8. safe cleanup and no production-app changes.

A complex mapped blocker is a useful deliverable but is not release readiness. If Workspace is not complete, name exact unfinished render surfaces and why; do not say only 'validation incomplete'. Include replay commands that use owned fixtures and no secret-bearing literals.
