# Test Impact Inventory

Minimum focused tests to update or add:

- candidate header selection parity: run closed, run failed/in-progress exception, lease expiry, step status eligibility, ordering by sequence;
- candidate hydration: subprocess candidate, workflow-backed role candidate, direct-agent candidate;
- technical-agent binding: missing binding warning/skip, bound agent pass-through, project-structure read access already present, project-structure read access newly granted and saved;
- recovery: recoverable execution run id, manual recovery directive after current attempt start, artifact recovery execution reuse;
- route preservation: database requirement, upstream materialization, stranded recovery, subprocess, workflow, direct-agent;
- architecture guardrails: no Process Core, no production driver API, no hidden MAF/Tooling dependency, no prohibited viewport proof artifacts.
