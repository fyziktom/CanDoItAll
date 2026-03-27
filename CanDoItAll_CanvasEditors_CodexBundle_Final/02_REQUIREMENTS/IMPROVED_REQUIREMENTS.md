# Improved requirements

This document rewrites the raw notes into implementation-ready requirement groups. Each group maps directly to one item folder in `07_ITEMS`.

## I01 — Foundation: rich node schema, metadata, and compatibility

**Objective:** Create a stable data foundation for all new canvas nodes without exploding the schema or breaking existing project structure records.

**Normalized requirement:** Introduce a typed metadata strategy, moderate expansion of ProjectObjectType, disciplined ObjectSubtype usage, and backward-compatible persistence rules for the new node families.

**Scope:**
- Shared contracts and enums.
- Project object persistence model and schema initializer.
- Metadata serialization, validation, and round-trip helpers.
- Compatibility coverage in integration tests.

**Out of scope:**
- Implementing every downstream editor feature that depends on the new schema.
- A full polymorphic ORM rewrite.

**Covered original notes:**
- N001 — Project structure
- N038 — Requrements/Prerequisites
- N039 — Generic node

## I02 — Common starter blocks and project structure catalog refresh

**Objective:** Add the missing starter blocks for the project structure canvas and make them discoverable in a cleaner catalog.

**Normalized requirement:** Create first-class starter blocks for Deployment, Repos, Dockers, Task Flow, Backlog, and Server, with consistent visual profiles and creation paths.

**Scope:**
- Project structure create catalog.
- Default visual profiles for the new starter blocks.
- Any supporting factory methods or subtype mapping required to create them.

**Out of scope:**
- Deep feature implementation for each child node family such as repositories, servers, or workflows.

**Covered original notes:**
- N002 — Common Bloks
- N003 — Deployment
- N004 — Repos
- N005 — Dockers
- N006 — Task Flow
- N007 — Backlog
- N008 — Server

## I03 — Meeting nodes for online and onsite work

**Objective:** Add meeting nodes with real metadata for online and onsite scenarios and surface them naturally in the canvas and calendar flows.

**Normalized requirement:** Implement meeting blocks, meeting nodes, channel/address/repeating metadata, meeting-specific actions, and calendar integration for online and onsite meetings.

**Scope:**
- Meeting block and meeting node creation.
- Online meeting details including channel enum and repeat rules.
- Onsite meeting details including address and map link behavior.
- Meeting actions such as Add blocks, Add Tasks, Add progress, Add priority, Add Recording.

**Out of scope:**
- Full meeting synchronization with external calendar providers.

**Covered original notes:**
- N009 — Meetings
- N010 — Meetings
- N011 — Meeting block
- N012 — Online
- N013 — Channel (enum MSTeams, Google Meet, Zoom, WhatsApp, Telegram)
- N014 — Date
- N015 — Repeating (enum per day, per week, per 2 weeks, per month)
- N016 — RightClick Menu Options
- N017 — Add blocks
- N018 — Add Tasks
- N019 — Add progress
- N020 — Add priority
- N021 — Add Recording
- N022 — Onsite
- N023 — Address
- N024 — Click to google maps
- N025 — Date
- N026 — Repeating and right click same as online

## I04 — Recording, transcript, and LLM-backed actions

**Objective:** Model recordings and transcripts as proper nodes and wrap all LLM-powered actions in explicit confirmation and provider selection.

**Normalized requirement:** Add Recording and Transcript nodes, transcript generation from recordings, standalone transcript support, and confirmed LLM actions such as Summarize, Find my tasks, and Find others delivery to me.

**Scope:**
- Recording node creation and placement beneath meetings or independently.
- Transcript node creation from recordings or manual standalone creation.
- LLM action confirmation dialog, provider selector, and result persistence.

**Out of scope:**
- A production-grade speech-to-text engine implementation beyond integration placeholders and provider orchestration.

**Covered original notes:**
- N027 — Recording
- N028 — Usually under some meeting block
- N029 — Right click Menu options
- N030 — Create transcript
- N031 — Transcript
- N032 — Usually under some recording node, but can be separately (for example someone will send me transcript to email
- N033 — Right click menu options
- N034 — Summarize
- N035 — Find my tasks
- N036 — Find others delivery to me
- N037 — All those actions with confirmation because it must send request to LLM (selector OpenAI API vs Local Ollama)

## I05 — Participants and CRM-lite registry

**Objective:** Introduce people and organization nodes without turning the app into an oversized CRM rewrite.

**Normalized requirement:** Add participant-related node types and a lightweight registry for HR, team blocks, team sections, freelancers, partners, and AI agents, reusable by tasks and meetings.

**Scope:**
- Participant node family and registry.
- HR, Team block, Team Section, Freelancer, Partner, and AI Agent variants.
- Selector reuse in downstream task assignment and meeting participation.

**Out of scope:**
- Sales pipelines, deal stages, or CRM-style account management.

**Covered original notes:**
- N040 — Prarticipants
- N041 — HR
- N042 — From CRM (need to add at least basic crm module)
- N043 — Team block (for example as start of organization chart)
- N044 — Team Section (for example HW department, etc.)
- N045 — Freelancer
- N046 — Partner
- N047 — AI Agent

## I06 — Task, issue, and assignment model

**Objective:** Create robust work-item nodes that connect the canvas to delivery ownership and basic execution planning.

**Normalized requirement:** Add Task and Issue nodes with what/when/who metadata, repo links or free-form description, and compatibility with participant selectors and attachments.

**Scope:**
- Task and issue node creation and editing.
- Assignment, due date, description, and repository linkage.
- Attachment compatibility points.

**Out of scope:**
- A full kanban or sprint board implementation.

**Covered original notes:**
- N048 — Tasks
- N049 — Task
- N050 — What, when, who (for already added HRs offered selector)
- N051 — Issue
- N052 — Possible link to repo
- N053 — Or pure description
- N054 — Attachments

## I07 — Attachments, feedback, payment, and send flows

**Objective:** Model delivery evidence and operational follow-up items explicitly on the canvas instead of burying them in generic notes.

**Normalized requirement:** Add typed attachment and follow-up nodes for video, screenshot, log, notes, revision, feedback, payment, and send with channel-aware options.

**Scope:**
- Video, screenshot, log, notes, revision, feedback, payment, and send node families.
- Send selector options such as File, Offer, Email, Message plus channel, Invoice, and Money.
- Attachment capture entry points and previews where appropriate.

**Out of scope:**
- Full email delivery infrastructure or accounting software integration.

**Covered original notes:**
- N055 — Video
- N056 — Screenshot (take last captured screenshot, or from clipboard)
- N057 — Log
- N058 — notes
- N059 — Revision
- N060 — Feedback
- N061 — Payment
- N062 — Send
- N063 — With selector for File, Offer, Email, Message (plus channel), invoice, money, etc.

## I08 — Typed file nodes and Mermaid viewer

**Objective:** Give file nodes clear meaning on the canvas and add diagram-aware viewing for Mermaid content.

**Normalized requirement:** Add typed file node visuals for pdf, excel, docx, txt, json, md, and Mermaid with color coding, icons, and diagram detection metadata.

**Scope:**
- Typed file node variants and visual mapping.
- Mermaid viewer and diagram-type detection feedback.
- Subtype labels and preview affordances.

**Out of scope:**
- A full-blown document editor for every file format.

**Covered original notes:**
- N064 — Files
- N065 — Own menu item for pdf, excel, docx, txt, json, md
- N066 — Each of those files nodes must have proper color (pdf red, excel green, docx blue, txt, json and similar probably gray with icon/text of type, etc)
- N067 — Mermaid (plus viewer)
- N068 — Auto identification of graph type and info about it on node.

## I09 — Repository nodes and resource integration

**Objective:** Connect repository nodes to the existing resource model so local and remote repositories become reusable assets instead of duplicated data islands.

**Normalized requirement:** Add repository nodes that can represent remote GitHub repositories and local repositories or folders, with selectors and folder-picking fallbacks.

**Scope:**
- Repository node creation and editing.
- Remote GitHub connection and repository selection.
- Local repository or folder selection and path fallback.
- Cross-linking to reusable resource records when sensible.

**Out of scope:**
- A full Git provider synchronization engine.

**Covered original notes:**
- N069 — Repository
- N070 — Remote
- N071 — GitHub connection
- N072 — Selection of specific repositoriy
- N073 — Local
- N074 — OpenFolder dialog

## I10 — Script nodes and terminal execution surface

**Objective:** Model executable scripts cleanly and provide a realistic terminal experience inside the web app instead of assuming native terminal launch from a browser.

**Normalized requirement:** Add PowerShell script and console script nodes plus an in-app, manager-backed terminal surface rooted to the working directory.

**Scope:**
- PowerShell and console script node families.
- Open terminal action rooted to the working directory.
- Integration points to runtime or manager-backed execution services.

**Out of scope:**
- A fully featured shell emulator with every terminal capability.

**Covered original notes:**
- N075 — Scripts
- N076 — Add PS script
- N077 — Console script
- N078 — All with button to “Open terminal” (automatically in work folder)

## I11 — Python environment nodes

**Objective:** Add lightweight environment nodes for Python toolchains so scripts and workflows can point at concrete runtimes.

**Normalized requirement:** Implement Python environment nodes with provider selection such as python or conda, plus identity metadata like environment name.

**Scope:**
- Python environment node creation and editing.
- Provider selection and environment identity fields.
- Visual association with related scripts or tasks.

**Out of scope:**
- Complete environment provisioning or package management automation.

**Covered original notes:**
- N079 — Environments
- N080 — Python Environment
- N081 — Provider (python, conda)
- N082 — Name, etc.

## I12 — .NET runtime, launch profile, and localhost nodes

**Objective:** Make .NET project runtime nodes truly useful by connecting them to launch profiles, project selection, localhost URLs, and run modes.

**Normalized requirement:** Implement .NET-related nodes that parse launchSettings, infer default addresses, expose localhost links, and support dotnet watch and release run variants.

**Scope:**
- Project selector and default launch profile parsing.
- Localhost URL discovery and click-to-open behavior.
- dotnet watch node settings.
- Release run node settings including http versus https.

**Out of scope:**
- A full IDE-grade debugger experience.

**Covered original notes:**
- N083 — Dotnet related
- N084 — Project default launch profile
- N085 — Project selector
- N086 — Then auto parse of launchprofile to get default addresses
- N087 — localhost run in node details – click to open in new tab
- N088 — Dotnetwatch
- N089 — Command to run specific project in dotnetwatch
- N090 — Ideal would be project selector
- N091 — Specify http vs https
- N092 — Run Release
- N093 — Specify http vs https
- N094 — Ideal would be project selector
- N095 — Address of release localhost run in node details – click to open in new tab

## I13 — EF migrations and Tailwind watch nodes

**Objective:** Add execution-oriented nodes for database migrations and Tailwind watch so common developer workflows live on the canvas.

**Normalized requirement:** Implement migration command nodes and Tailwind watch nodes with project-aware command storage and terminal execution reuse.

**Scope:**
- EF migration nodes and command selection or input.
- Tailwind watch nodes and command configuration.
- Execution handoff into the shared terminal or runtime surface.

**Out of scope:**
- Smart command generation for every possible custom repo layout.

**Covered original notes:**
- N096 — Apply Migration EF (add, update, etc)
- N097 — Select or input of command I call from ps for some migrations
- N098 — Tailwind watch run (for projects that use tailwind)
- N099 — Command how to run in ps tailwind for that specific project

## I14 — Remote server core model

**Objective:** Model remote server infrastructure as a structured canvas node with technical, commercial, and access-related metadata.

**Normalized requirement:** Add remote server nodes with capacity, price, address, provider links, login links, SSH, secret references, and account identity.

**Scope:**
- Remote server node metadata and editor.
- Capacity and business metadata.
- Provider and login links.
- SSH and secret-link metadata.

**Out of scope:**
- Direct secret value editing inside the canvas.

**Covered original notes:**
- N100 — Remote Server (common block)
- N101 — Parameters
- N102 — CPU, RAM, HDD/SSD cap, etc.
- N103 — Price and business related info
- N104 — Address
- N105 — Provider
- N106 — Link to provider website
- N107 — Link to login
- N108 — SSH connection (we need terminal component)
- N109 — Connection to secret for login
- N110 — Account name

## I15 — Domains, DNS, Docker, database, keys, and AI links

**Objective:** Complete the infrastructure subtree with typed child nodes for domains, DNS, containers, databases, deployment folders, keys, and AI-related references.

**Normalized requirement:** Add infrastructure-adjacent child nodes for connected domains, DNS records, docker mode, proxy provider, database info, deployment folder, keys, and AI links including ChatGPT, Codex, and local LLM references.

**Scope:**
- Domain name and owner nodes or metadata.
- DNS record nodes.
- Docker type and proxy provider representation.
- Database, deployment folder, and key references.
- AI links including ChatGPT conversation link, Codex thread link, and local LLM reference.

**Out of scope:**
- Automated DNS management or container orchestration execution.

**Covered original notes:**
- N111 — Connected Domains
- N112 — Domain name
- N113 — Owner
- N114 — DNS Records
- N115 — Docker type (compose vs swarm)
- N116 — Proxy provider
- N117 — Nginx, traefik, etc.
- N118 — Database
- N119 — Type, connection
- N120 — Deployment folder
- N121 — Keys
- N122 — AI
- N123 — ChatGPT conversation link
- N124 — Codex thread link
- N125 — Local LLM

## I16 — Progress, priority, and marker UX normalization

**Objective:** Resolve ambiguity around the small status controls and make them easier to use accurately.

**Normalized requirement:** Normalize click behavior for progress, priority, and markers, and enlarge compact-ring hit targets in the right-click menu.

**Scope:**
- Badge interaction behavior.
- Compact ring sizing and menu ergonomics.
- Associated adapter and component tests.

**Out of scope:**
- A total redesign of all node action affordances.

**Covered original notes:**
- N126 — Common
- N127 — Left click on Progress icon in node must show only selector of priority
- N128 — Markers and progress main circle in right click menu must have larger diameter

## I17 — Relationship editing, delete behavior, and borders

**Objective:** Make structural editing safer and more expressive when users reconnect nodes, delete them, or organize them into named borders.

**Normalized requirement:** Add unconnect and reconnect workflows, sensible delete confirmation rules, drag-to-border behavior, and border naming.

**Scope:**
- Unconnect and reconnect behavior.
- Delete confirmation rules by node complexity.
- Drag-and-drop onto borders or group frames.
- Border naming and display.

**Out of scope:**
- A full graph-history UI beyond existing undo or redo support.

**Covered original notes:**
- N129 — Unconnect node and connect it to some different node
- N130 — Delete (simple note without confirmation, more complex with confirmation)
- N131 — Drag and drop node to some border with other nodes
- N132 — Name for borders

## I18 — Arrow links, side-aware placement, and mindmap image export

**Objective:** Fix spatial logic and give users clearer connection semantics and exportable visuals.

**Normalized requirement:** Add directional arrow support, export the mindmap as an image, and fix child placement so new nodes are created on the side implied by the connection geometry.

**Scope:**
- Connection arrow rendering or settings.
- Side-aware placement policy.
- Mindmap image export flow.

**Out of scope:**
- A full vector export suite beyond the requested image export.

**Covered original notes:**
- N133 — Connection between nodes with additional arrow
- N134 — Export mindmap as image
- N135 — Node should be placed on the side where it should connect. For example if I move some node to left side of the canvas, it connects to parent node from right side, then I add new node that is connected under that node, it connects it from left side, but place it on right side of the node. It must place it to side where it is connected.

## I19 — Progress summary modal, tree checklist, and exports

**Objective:** Turn nested node progress into a real summary view that can also be exported.

**Normalized requirement:** Add a progress summary modal showing a tree of child statuses, inline status editing, XLSX export, and Mermaid Gantt export.

**Scope:**
- Progress summary entry points from the props panel and right-click menu.
- Tree view modal with inline progress selectors.
- XLSX export.
- Mermaid Gantt export.

**Out of scope:**
- Full project management analytics beyond the requested summary and exports.

**Covered original notes:**
- N136 — Controls
- N137 — Progress summary
- N138 — For nodes that have some nodes under it automated display of summary checklist of state items under it
- N139 — Click to button in props panel or right-click menu item => open modal with summary status, checklist of all statuses of items under it (as tree view), possibility to change status in that list (each item has on its line button with dropdown selector of progress)
- N140 — Posibility to export as xlsx
- N141 — Export as mermaid gantt graph

## I20 — Shared floating tool window host for canvas editors

**Objective:** Create one reusable floating tool-window shell that both canvas editors can use for pinned, movable, searchable auxiliary panes.

**Normalized requirement:** Generalize the existing floating inspector patterns into a shared floating tool-window host inspired by Visual Studio tool windows and constrained to the visible canvas.

**Scope:**
- Reusable floating tool-window shell.
- Canvas-bound movement and fit-to-visible-canvas behavior.
- Shared header actions such as show or hide, pin, move, and close where appropriate.
- Slots for tree views, search bars, and preview content.

**Out of scope:**
- Solving every individual toolbox content requirement by itself; those land in dedicated downstream items.

## I21 — Prompt Factory components toolbox redesign

**Objective:** Replace the current wrong Prompt Factory component toolbox behavior with a real, searchable floating tool window.

**Normalized requirement:** Redesign the Prompt Factory components surface from the existing toolbox-panel or accordion style into a Visual Studio-inspired floating tree view with search and internal scroll.

**Scope:**
- Prompt Factory components toolbox container and content layout.
- Search behavior and hierarchical grouping.
- Internal scrolling and stage-fit behavior inside the floating host.
- Creation flow from the redesigned toolbox.

**Out of scope:**
- Fixing the intermittent 44-node insertion bug by itself; that has its own item.

**Covered original notes:**
- N142 — Prompt factory
- N143 — Components
- N144 — Better search of components
- N145 — It must work as toolbar in visual studio\
- N147 — Toolbar with components must be available as classic floating window toolbar inside of canvas, that I can show/hide, pin, move, etc.
- N148 — Inside accordeons with sections of prompts components
- N149 — Search bar on top
- N150 — Vartical Scrollbar inside if too much component/sections are in it. Toolbar window must fit always into visible canvas

## I22 — Prompt Factory eye-preview popover

**Objective:** Make the component eye icon genuinely useful by showing a canvas-side floating preview popover with the component text.

**Normalized requirement:** Add hover or focus preview behavior for component rows so the eye icon opens a floating preview on the available side, preferring the right side.

**Scope:**
- Eye icon interaction behavior.
- Side-aware preview popover placement.
- Preview content rendering and overflow handling.

**Out of scope:**
- A full editor inside the preview popover.

**Covered original notes:**
- N146 — Mouseover on icon of eye on component line show inside canvas popup floating window on available side (if right available, then prefer it) with text of component.

## I23 — Project Structure standard blocks toolbox

**Objective:** Give the project structure canvas the same improved floating toolbox pattern requested for Prompt Factory.

**Normalized requirement:** Add a floating standard-blocks toolbox to the project structure canvas, using a tree-oriented layout inspired by the Visual Studio Solution Explorer screenshot.

**Scope:**
- Floating standard-blocks toolbox for project structure.
- Search and grouping behavior for starter blocks and future node families.
- Create-node flow from the toolbox.

**Out of scope:**
- Re-implementing every project structure editor feature unrelated to block creation.

**Covered original notes:**
- N151 — NOTE: Similar toolbar also for standard blocks in project structure

## I24 — Prompt Factory intermittent 44-node insertion bugfix

**Objective:** Root-cause and fix the intermittent bug where a single component insertion sometimes attempts to add dozens of nodes.

**Normalized requirement:** Instrument, reproduce, and eliminate the intermittent duplicate-add behavior in Prompt Factory, with a regression harness that proves the fix.

**Scope:**
- Reproduction strategy and diagnostics.
- Prompt Factory add-component pipeline hardening.
- Regression tests and evidence.

**Out of scope:**
- General Prompt Factory UX redesign unrelated to the duplicate-add bug.

**Covered original notes:**
- N152 — Bugs:
- N153 — Adding of any component wants to add 44 nodes (happens just sometimes, like 4/5 situations).

## I25 — Screenshot-driven validation suite and evidence protocol

**Objective:** Make screenshot-based validation a hard release gate for canvas-editor changes so visual regressions are not hand-waved away.

**Normalized requirement:** Add a dedicated screenshot validation protocol, naming convention, artifact checklist, and Playwright-first evidence strategy for all UI-changing items.

**Scope:**
- Artifact naming convention for screenshots.
- Validation checklist and semantic screenshot review template.
- Playwright coverage expansion where it pays off most.

**Out of scope:**
- Replacing functional tests with screenshots alone.
