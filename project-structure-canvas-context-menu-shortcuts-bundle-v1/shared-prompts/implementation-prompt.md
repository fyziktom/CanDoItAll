# Implementation Prompt

Implement only the active subbundle for `C:\repositories\CanDoItAll\project-structure-canvas-context-menu-shortcuts-bundle-v1`.

Constraints:

- Preserve the architect-mandated shortcut letters for the named menu groups and named nested leaves.
- Assign deterministic collision-free shortcuts to the remaining visible siblings in the same menu layer.
- Keep shortcut handling scoped to the open context menu and do not break existing global canvas shortcuts or editable-field behavior.
- Prefer shared action metadata over one-off hard-coded JavaScript maps.
- If maintainability extraction is needed, keep it focused and preserve runtime asset load order.

Required execution behavior:

1. Re-read the owning subbundle README and confirm its prerequisites are satisfied before editing.
2. Implement only the files and tests owned by that subbundle.
3. Capture the proof listed in the subbundle README before marking the phase complete.
4. Update the execution report with commands, browser analytics, screenshots, and gate outcomes while evidence is fresh.
5. Stop and reopen the bundle if proof is weak, collisions remain, or downstream assumptions no longer hold.
