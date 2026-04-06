# Acceptance

- Wrap each worker iteration in exception isolation.
- Log failures with enough context for operators.
- Apply a safe backoff when an iteration fails.
- Keep the worker alive after a transient failure.
