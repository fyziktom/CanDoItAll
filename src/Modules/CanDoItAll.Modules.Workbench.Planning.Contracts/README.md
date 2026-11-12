# Planning values

This project contains the native task estimate, execution and resource value
contracts used by both Workbench services and the independent planning renderer.
It has no project dependencies. Existing namespaces, enum values and JSON members
are retained, with type forwarding from the Workbench assembly.

Project admission, EF records, assignments and their revisions, historical cost
basis, persistence and pricing orchestration remain in the native Workbench module.
The mixed `ProjectWorkbenchModels.cs` file remains with that owner.
