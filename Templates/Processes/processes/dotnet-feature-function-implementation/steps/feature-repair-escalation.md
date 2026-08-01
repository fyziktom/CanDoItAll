# Escalate unresolved feature repair

Produce a parent no-go packet when the bounded repair attempt selected `repair-attempt-incomplete`. Do not run or reinterpret the skipped targeted recheck; the typed repair branch is the no-go decision.

Include:

- Original feature scope and acceptance criteria.
- Initial validation failure.
- Repair attempted and changed files.
- Repair validation or runtime gate findings that remain failing or incomplete.
- Why another repair is not safe inside this subprocess.
- Recommended next parent action: new subprocess scope, architecture review, environment repair, or explicit human decision.

Do not mark the feature accepted in this branch.
