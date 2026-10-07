# Exact source pair and delivery

Main review head: b3aec979eae708edd0a53b42cef41b9d52fbb49b. Reported final executable PP3:
17452fc2c892de1edb62cbdbb284b5a44b4536b2. Later test/docs changes are qualified by the report.

Remote Components development at review remained 4a858412d2c2a3f6123bf23d8c4584f05b47627d.
Reported tested Tooltip commit b495d4c4a28f0a6588ba10bfaa7be6e8409eae18 could not be fetched
from the connected GitHub. This is an actual delivery limitation. It is not authorization to
reimplement an existing local fix, reset a sibling, or declare source equivalence from version alone.

At entry inspect each sibling working tree and actual commit. Verify the existing Tooltip fix
and test family locally, intended signing identity, loaded assembly version/informational commit,
static assets and main source-mode mapping. Record full executable inputs and dirty deltas if any.
FileTools 3a080ecd31068a77c1e1bd639f7a78e21c93db85 is previous report provenance, not a forced ref.

CI resolves a matching Components branch and records its actual commit in the current workflow;
inspect the current workflow rather than assuming all consumers pin the same ref. Separate:
1. local tested source pair,
2. signed local changes,
3. remote accessibility,
4. actual target-CI/package consumption.

A local verified pair may support continued development. Remote delivery still pending must be
explicit in the final report and next-step readiness; do not describe an externally reproducible
release closure. Only the owner separately authorizes push/release. Provide exact sibling commits
and intended target branch without exporting private keys or binary/font bundles.
