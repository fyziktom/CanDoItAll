# Normalized Requirements

| Requirement | Source notes | Observable success criteria |
| --- | --- | --- |
| R001 Page title | N001 | Loaded project-structure pages render `PS - <project name>` as the browser title. Long names use a deterministic substring ending with `...`. |
| R002 Node catalog | N002, N003, N008 | Agents have a default internal project-structure node catalog tool that includes canonical object type, subtype, labels, create action, descriptions, and task/file/block guidance. |
| R003 Work task guidance | N002, N007 | Tool descriptions and catalog examples state that work task nodes are `ProjectObjectType.WorkItem` with `objectSubtype = "task"` and dependency links use `DependsOn`. |
| R004 Selected-node context | N005 | Contextual project-structure chat prompts and invocation metadata include the selected node IDs when selection exists. |
| R005 Selected nodes to new subproject | N004, N005, N006 | A one-call service/API/internal tool creates a named subproject under the current project and moves the selected node set, with descendants, into it. |
| R006 Parentage preservation | N006 | Moved root nodes are parented to the target project root; moved descendants keep moved parents; no moved node points to a source-project-only parent. |
| R007 Dependency preservation | N007 | Dependency/user links where both endpoints move are retained in the target project; cross-project links are removed; dependency query can read the target project after movement. |
| R008 Scenario workbook | N008, N009 | A verified `.xlsx` lists generic project-structure agent scenarios, ranks which should become tools, and includes architect-provided examples plus additional user stories. |
