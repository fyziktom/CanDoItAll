
# User clarifications that materially changed the target architecture

These clarifications are treated as hard design constraints for the revised bundle:

1. **Node is not just a visual shell.**
   - The user works mindmap-first.
   - Brainstorming starts as quick/simple notes.
   - Those notes are later refined into richer typed blocks.

2. **Node evolution is a first-class workflow.**
   - A simple note may become a task, decision, or another block type later.
   - The historical path of that thinking is valuable.

3. **Mindmap spatial data is semantically meaningful.**
   - X/Y placement is not just for pretty rendering.
   - Markers are also meaningful signals.
   - Future cross-project analysis and improvement workflows will use those signals.

4. **CRM/HR now reaches into project structure.**
   - Nodes can now be associated with people, agents, and partners.
   - Work items, meetings, and participant nodes now cross the boundary between project graph and party directory.

5. **Wave-based stabilization is the intended delivery model.**
   - The user intentionally lands a feature wave quickly.
   - Then performs architectural stabilization/refactor.
   - Then starts the next feature wave.

## Consequence for the revised target

The bundle now explicitly rejects the simplistic direction of “node is only a view”.

The revised target is:

- **stable node identity for workbench-authored thinking**
- **typed facets and transition history**
- **canonical spatial semantics**
- **explicit actor-assignment ownership by scope**
- **assembled graph for projections**
- **module-native canonical owners retained where appropriate**
