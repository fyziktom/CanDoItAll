# Package validation

This section concerns the ZIP and instruction/evidence structure only. It does not certify
C#, native ownership, browser behavior, Docker execution, performance or evidence authenticity.
Actual measured package results are recorded in package-validation.json after running checks.

The manifest covers every package file except the root manifest itself, including the sealed
shared manifest. Shared content is compared byte-for-byte with the retained 22-file v3 input.
Local Markdown paths must resolve inside the extracted package; source identities use exact
Git refs and bounded reading descriptions. Evidence starts unexecuted and cannot pass closure.

Run from the extracted package:

```text
python tools/validate_handoff.py
python -m unittest discover -s tools -p "test_*.py"
python -m unittest discover -s shared/tools -p "test_*.py"
```

To validate a private external evidence document's structure:

```text
python tools/validate_handoff.py --evidence /private/task/evidence.json --require-complete
```

A consistent document is not proof that its commands or artifacts are genuine. Manually
review original native identities, source pairs, logs and mixed-run dispositions. The sealed
unexecuted template must fail --require-complete. Do not edit it to record implementation.
