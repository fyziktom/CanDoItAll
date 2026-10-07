# Workbench Planning WB1

**Execution target:** Codex GPT-6 Astra Max. **Foundation:** shared UI decoupling v3.

Preserve completed Agents AC1 and move to a substantial, coherent Workbench slice:
the project calendar, entire interactive Gantt scheduling surface, and both native-task
create/edit form families with their actual estimate/execution/resource/pricing children.
This is not the whole Project Structure canvas or a scheduling/runtime rewrite.

Start with [prompt.md](prompt.md). The [review](REVIEW.cs.md), [AC1 assessment](AC1_REVIEW.md),
[scope](SCOPE_AND_ARCHITECTURE.md), [Workbench observations](WORKBENCH_SOURCE_REVIEW.md),
and [validation matrix](VALIDATION_MATRIX.md) explain what to preserve and prove.

The package carries a source review and an unexecuted implementation assignment.
It contains no product patch or previous private test evidence. Reviewed SHAs are not
checkout instructions. Keep historical bundles intact during implementation.

```text
python tools/validate_handoff.py
python -m unittest discover -s tools -p 'test_*.py'
```

Those commands validate this package, not CanDoItAll. Product results belong in an external,
owned evidence ledger based on [the template](templates/evidence.json).
