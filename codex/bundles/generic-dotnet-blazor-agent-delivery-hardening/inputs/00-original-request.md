# Original Request

User requested continuation of the genericity hardening work after a calculator-specific Blazor build skill was removed.

Core request:

- Determine whether a generic replacement exists for building, running, and testing .NET apps.
- Analyze default agents, instructions, skills, and tools.
- Improve them so process-run agents can build and test apps without calculator-specific or sample-specific hardcoding.
- Add a specialized Blazor app-building agent that can combine C#, small-scale JavaScript when needed, MCP servers, and the shared component libraries, primarily BaseLib/components.
- Feed the changes into the running web app and validate through the web app flow.
- Create simple project structures, add process nodes that start exact app-build processes, and let agents build two small random-topic apps under `C:\programovani\dotnet`.
- Observe the process without manually repairing the generated apps. If agents cannot complete the apps, repair only generic process, skill, tool, or agent instructions and retry.
