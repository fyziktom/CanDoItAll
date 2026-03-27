# Target Solution

Keep runtime launching inside the shared workbench module:

- add a dedicated runtime-launch service that resolves `ProjectStructureNode` metadata into a typed PowerShell launch plan and executes it locally
- keep command derivation close to existing `ProjectScriptMetadata` and `ProjectEnvironmentMetadata` ownership instead of hiding it in Razor markup
- let `ProjectStructurePage` ask the service whether the selected node is launchable, render the two launch buttons, and display explicit launch feedback

Important boundaries:

- do not abuse the existing node-command routing pipeline, because it is designed for artifact navigation rather than local process launch
- do not add new create-definition fields unless a required launch input is genuinely missing from the current typed metadata
