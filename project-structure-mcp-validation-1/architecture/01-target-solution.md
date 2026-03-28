# Target Solution

## Validation Shape

- Keep `CanDoItAll Main` as the user-owned root project.
- Create a validation child project named `project-structure-mcp-validation-1` under `CanDoItAll Main`.
- Use the validation child project as the safe location for source-asset capture, raw import proof, approval-request or defect nodes, analytics references, and closure notes.
- Create or link additional subprojects under `CanDoItAll Main` for larger source branches where the mindmap meaning is broad enough to deserve an independently navigable structure.

## Semantic Mapping Rules

- Map broad capability domains such as `management of projects`, `mindmaps`, `knowledge db`, `AI`, and `phase 2` to subprojects when they are large, multi-level, or clearly initiative-sized.
- Map structural or thematic containers inside a project to `ProjectBlock` with a meaningful subtype such as `feature`, `architecture`, `implementation`, `delivery`, `deployment`, `repos`, `dockers`, `task-flow`, `backlog`, or `server`.
- Map actionable leaves to `WorkItem` with a subtype such as `task`, `issue`, `revision`, `feedback`, `payment`, or `send` when the leaf meaning fits.
- Map repo-, file-, environment-, script-, or infrastructure-specific topics to `Repository`, `File`, `Environment`, `Script`, or `Infrastructure` nodes when the meaning is explicit in the source.
- Use `Note` or `Decision` only when the source branch is explanatory rather than actionable or structural.

## Execution Strategy

- First prove lease acquisition and live workspace bootstrap before any broad import.
- Then run the XMind import into the validation workspace to test the generic import path with the real package.
- After the raw import succeeds, shape the high-value parts of the source into richer nodes and subprojects under `CanDoItAll Main`.
- Capture browser proof from the actual structure pages after the shaped hierarchy exists.
- Capture checklist and analytics evidence last, because those reports are only meaningful after the structure stabilizes.

## Defect Policy

- A defect is any MCP hang, transport failure, deterministic server error, wrong structure write, wrong structure readback, missing tool surface needed by the validation, or mismatch between MCP data and browser-visible structure.
- Each defect must be either repaired in code and revalidated, or recorded explicitly in the validation workspace and this bundle with the blocking proof attached.
