# Codex retry protocol

If any gate fails:

1. keep the task open,
2. inspect the failure,
3. fix the code or missing validation,
4. rerun all impacted gates,
5. only then continue.

Do not accept:
- “tests pass but browser is broken,”
- “browser is fine but a preserved feature disappeared,”
- “performance was claimed but not measured.”

The correct behavior is always:
**fix -> rerun -> verify -> continue**.
