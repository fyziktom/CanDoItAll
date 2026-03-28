# Original Request

## Raw Request

- Use `$candoitall-bundle-workflow` to prepare, validate, execute, and validate a bundle for adding a new MCP server for CanDoItAll project-structure access.
- Why we are building this:
- Codex must get new tasks directly from the project-structure mindmap.
- Codex should create subprojects and use them for larger work such as bundles so the main project stays organized.
- Codex should write status, troubles, approvals, plans, and work progress back into project structure during feature work, planning, reporting, and analysis.
- Codex should help create or improve an existing plan.
- Codex should use project structure for planning, reporting, analysing, and approvals, not only coding.
- Codex should add approval-request information into project structure when needed.
- Codex should help import projects described in other tools such as XMind, Mermaid, or DOCX.
- Codex must read information from project structure, including attached assets. Assets are readonly. If an asset changes, the new version must be added as a new asset under the original asset node.
- Codex should help prevent another agent from working in the same repository and branch to avoid merge conflicts.
- Main purpose of the server:
- allow communication with project structure
- create and manage projects, subprojects, and plan projects
- create, edit, and retrieve project-structure nodes and their parameters
- get project-structure information, filtered slices, and cross-project analysis
- keep fresh information about what is happening so shared parts are identified in time
- Additional MCP points:
- get filtered node subsets to reduce context
- filter out graph-helper data such as positions unless explicitly requested
- get a checklist of unfinished items including prerequisites
- support priority filtering with prerequisite propagation, including child-to-parent priority inheritance unless a parent is stopped, paused, or finished
- include a documentation or API part for project-management knowledge
- prepare for a future Knowledge DB or vector-search-backed guidance source
- provide trustworthy project-management best-practice guidance for Codex discussions
- include the explicit mission statement about making surroundings better and making projects successful
- use static best-practice guidance now, but keep the architecture ready for a future knowledge driver
- add settings inside CanDoItAll web that define what an agent can and cannot do through this MCP
- include approval limits such as under-15-minute tasks allowed in own branch, over-1-hour tasks requiring approval, and project-specific approval behavior
- add an internal automatic manager of access to projects so two MCPs on different computers cannot change the same part at the same time
- reuse shared MCP helpers and cores where possible
- isolate reusable parts that may help other MCPs later
- if shared parts are needed first, add them first and cover them with unit tests before the main MCP implementation
- support the deployment shape where CanDoItAll web, DB, and files run on one computer in the local network while four other computers run Codex and their own instances of this new MCP
- local MCP instances must connect to the address of the main CanDoItAll machine
- download or provide setup, install, and reinstall scripts for smooth use of the new MCP on different computers
- preferably expose setup information in UI, otherwise provide PowerShell scripts plus README and skill material
- Validation requirements:
- validation must include a real test of creating and obtaining nodes in project structure with correct data
- example validation chain: create a delivery block, then an Excel asset block, and read them back; expand that chain to cover MS Office blocks and PDF while saving time
- validate all covered user stories and confirm they are possible with the new MCP
- capture analytics data during validation for later post-implementation validation
- use cross-checking against checklists to see what really works
- the bundle is not complete until the new MCP is fully working

## Clarifying Interpretation

- The request is for a new initiative, not a small bug fix.
- The feature scope includes central web changes, a new MCP server project, test coverage, validation proof, and rollout/setup material.
