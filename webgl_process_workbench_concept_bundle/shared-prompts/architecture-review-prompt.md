# Architecture review prompt

```text
Review the completed work only against the bundle architecture rules.

Check:
- Is the library still universal?
- Is the runtime still JS-owned for per-frame behavior?
- Is the default scene still a guided center-lane 3D scene rather than uncontrolled graph navigation?
- Are labels readable through a DOM mirror or equivalent overlay?
- Is the sandbox still isolated and non-persistent?
- Is the proof strong enough for the next phase?

If any answer is no, fail the gate, name the exact reason, and trigger the mapped corrective subbundle.
```
