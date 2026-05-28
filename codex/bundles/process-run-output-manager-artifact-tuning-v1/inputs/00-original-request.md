# Original Request

User feedback from the completed Blazor app delivery run:

1. The process wrote the created app under `C:\Users\lucys\AppData\Local\CanDoItAll\workspace\output\scopes\organization\e5df9ad633dbc6974a0678a74976013c\process-runs\801f259d-8a52-41b8-a99f-cc96a2fc1947\TetrisGame`, but the project structure requested `C:\programovani\dotnet-demo\output`. For a new app, workspace-first can be acceptable during construction, but the process must still move or deliver to the requested destination before completion. Repairs must respect the defined folder directly.
2. In the Processes page Manager tab, selecting a specific run still reports that a manager must be connected. The user expects to select a run and chat with the manager about what happened and about created artifacts.
3. Project structure adds too many child nodes under the process run, one for each artifact or artifact subfolder. It should add only a node for the process workspace folder so the run folder can be opened. Some process runs legitimately have multiple workspace folders.

Instruction: use `$candoitall-bundle-workflow` to solve this.
