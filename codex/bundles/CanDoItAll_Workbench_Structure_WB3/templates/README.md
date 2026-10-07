# Evidence template

Copy `evidence.json` to an owned ignored artifact directory before execution.
Do not edit the sealed template or populate it with inherited green statuses.
Keep original attempts, source fingerprints, commands, discovered/executed counts
and safe artifact references. A PASS group cites an actual passing attempt;
a QUALIFIED/FAIL/BLOCKED group explains its disposition. Native readback/image
provenance is distinct from browser evidence and from report text.

Use the existing repository evidence conventions rather than inventing a second
validation platform. The optional checker validates structure only. Its strict
completion mode intentionally rejects qualified groups; a separately documented
bounded decision must never relabel a mixed test run just to satisfy that mode.
