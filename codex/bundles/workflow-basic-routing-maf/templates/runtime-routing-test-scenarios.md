# Runtime Routing Test Scenarios

## Predicate IF/ELSE

- Start payload: `{ "classification": "spam" }`.
- Predicate edge to spam executor: `$.classification == "spam"`.
- Predicate or default edge to normal executor: `$.classification != "spam"` or switch default.
- Expected: only spam executor is invoked for the spam payload.

## Switch Default

- Source executor emits `{ "decision": "needsHuman" }`.
- Switch cases: `approved`, `rework`.
- Default: human input executor.
- Expected: human input executor is invoked when no case matches.

## Fan-Out Multi-Selection

- Source executor emits `{ "channels": ["email", "slack"] }`.
- Fan-out targets: email executor index 0, slack executor index 1, ticket executor index 2.
- Expected: email and slack executors invoked; ticket executor skipped.

## Invalid Routes

- Unsupported language `artl-v1` without ARTL compiler rejects.
- Malformed JSON path rejects.
- Duplicate switch default rejects.
- Fan-out index out of range rejects.
