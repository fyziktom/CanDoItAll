# Proof Capture Template

## Command Evidence

| Command | Purpose | Result | Artifact / Notes |
| --- | --- | --- | --- |
| `dotnet test ...` | Describe why the command is required | `Pass/Fail/Blocked` | Path to logs or short summary |

## Browser Evidence

| Route | Viewport | Assertions | Screenshot Path | Reviewed? |
| --- | --- | --- | --- | --- |
| `/example` | `1600x1000` | Describe the exact DOM/visual assertions | `evidence/example.png` | `Yes/No` |

## Closure Notes

- State whether the proof is strong enough to close the subbundle.
- State which downstream subbundles can now continue.
- State any explicit reopen trigger discovered during proof.
