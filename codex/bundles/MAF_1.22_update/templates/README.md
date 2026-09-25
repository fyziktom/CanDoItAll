# Execution scaffolds

These files are templates, not tool outputs or test results. Copy them to a working evidence directory before filling them; keep the original preparation package immutable.

`impact_request.example.json` records the verified field shape. Replace paths with absolute local paths and populate `changes` solely from the actual diff before calling the discovered tool. An empty template is not evidence of zero affected tests.

`suite_results.csv` separates platform/gate outcomes and starts at NOT_RUN. Add all actual suites/lanes, counts, commands, revisions and evidence paths. A row labeled “full” must use the unfiltered documented command.

`scenario_results.csv` separates deterministic regression, UI and real-provider proof. Populate all attempts, not only the successful retry. Applicability is a source-based decision, not an excuse for a missing test environment.
