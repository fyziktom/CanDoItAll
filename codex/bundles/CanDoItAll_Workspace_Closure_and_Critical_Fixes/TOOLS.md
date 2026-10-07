# Local handoff tools

Package validation never executes product code or contacts GitHub. It checks UTF-8 inventory,
metadata/source paths, local links and SHA-256 manifest. The source register has explicit
coverage and dependency revisions; a valid hash format is not source authenticity proof.

```text
python tools/validate_package.py --root .
python shared/tools/validate_bundle.py --root shared
python tools/test_package.py
python shared/tools/test_tooling.py
python tools/test_closure.py
```

Copy the result template outside this sealed bundle. To check bookkeeping while work is open:

```text
python tools/validate_closure.py --plan closure-plan.json --results /path/to/results.json
```

To check an asserted final complete result against actual local evidence hashes:

```text
python tools/validate_closure.py --plan closure-plan.json --results /path/to/results.json --evidence-root /path/to/evidence --require-ready
```

The initial template deliberately fails --require-ready. No test result is pre-filled.
These tools cannot establish that a human authorized spending, that a test really executed,
that a screenshot proves an effect, or that a claimed fix is correct. They reject inconsistent
or incomplete claims and stale source references; source/UI/owner review remains necessary.
