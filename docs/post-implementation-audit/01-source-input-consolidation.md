# 01 - Source Input Consolidation

This document converts the original Czech prompt and later tuning clarifications into one English requirement baseline for implementation and QA.

## 1. Product mission

CanDoItAll is a local-first software delivery workstation for one technical lead, architect, or developer who wants to manage:

- projects
- project phases and dates
- delivery prompts
- prompt templates and prompt flows
- project-linked technical objects
- validation and review
- testing and evidence
- development acceleration with Codex and Playwright

The product must feel like one coherent workstation, not a set of disconnected CRUD pages.

## 2. Baseline requirements from the original prompt

### 2.1 Platform and architecture

- .NET 10, C#, Blazor Server or Blazor Web App with Interactive Server rendering
- Tailwind CSS plus shared custom components
- modular monolith now, microservice-ready later
- SQLite or PostgreSQL via EF Core
- `IDbContextFactory` at runtime
- in-memory database option for tests and development
- future horizontal and vertical growth without UI fragmentation

### 2.2 Provider and infrastructure support

- OpenAI integration
- Ollama local integration
- Ollama remote integration
- secure storage of API keys, passwords, SSH keys, and other secrets
- managed local storage plus database-backed metadata

### 2.3 Project management

- create project with name and description
- manage project dates and phase dates
- manage statuses
- manage notes on all relevant selections
- store project stack profile decisions:
  - primary and secondary languages
  - database choice
  - UI framework choice
  - external API usage
  - storage strategy

### 2.4 Linked project objects

The product must support attaching and managing at least:

- folders
- files
- web links
- FTP connections
- PowerShell scripts
- repositories
- Docker or Docker Compose assets
- SSH connections
- keys and secrets
- prompts
- prompt galleries

### 2.5 Prompt management

- prompt gallery and prompt reuse
- tags and search
- usage history by project, repository, commit, or time
- prompt drafts and saved templates
- prompt factory and guided prompt generation
- save partially built prompt work
- export or send prompts to an LLM provider

### 2.6 Validation and testing workflow

- user stories and use-cases
- ASCII layout preparation
- architecture definition and revision
- implementation planning and revision
- implementation
- validation of prototype against plan
- test planning
- test execution and evidence

The product must support those delivery stages repeatedly for new features, not only for the initial project setup.

## 3. Added requirements from later clarifications

### 3.1 Internal application tabs

- the UI must have its own internal tab system
- tabs must support open, close, reorder, pin, sleep, wake, and restore
- tabs must survive refresh, reconnect, close, or crash through browser storage
- tabs must represent real work items such as:
  - opened projects
  - project structure surfaces
  - calendars
  - prompt wizard sessions
  - validation runs
  - test plans
- the product must avoid forcing many browser tabs because Blazor Server circuits are expensive

### 3.2 Project structure canvas

- the project structure surface is a core product capability
- it must reuse the documented canvas engine approach from the playlist-builder source pack
- JavaScript owns rendering and interaction capture only
- C# owns state, models, validation, persistence, and commands
- the canvas must support branching prompt work from any step
- the grouped hexagonal right-click menu is mandatory
- canvas modals or overlays may exist for visual quality, but they still dispatch typed C# intents

### 3.3 Project calendar canvas

- the project calendar is also a core workbench surface
- it must reuse the documented calendar engine approach
- it must link project phases, validations, tests, and related events
- linked artifacts must open into internal tabs

### 3.4 Shared prompt blocks and prompt flows

- prompt wizard logic must use centrally governed shared blocks
- repeated delivery stages such as architecture, review, implementation plan, implementation, security review, and testing must be reusable from one place
- prompt nodes must carry state and lineage
- multiple prompt branches must be able to run in parallel
- prompt steps must be visible in the workbench canvas

### 3.5 Unified project object model

The current thread clarifies that project-linked items should not behave like unrelated isolated records.

The intended model is:

- one shared base contract for project-linked objects
- typed subclasses or descriptors for object-specific behavior
- different visual appearance in the canvas by object type
- graph relationships between objects
- ability to create, connect, and inspect these objects directly from the project canvas

### 3.6 UX standards

- major create and edit flows must be wizard-driven
- wizards may open in modal form or in their own internal tab
- project authoring and prompt sequencing must feel canvas-driven where appropriate
- lists should default to cards, not dense tables
- the main left menu should follow the stronger enterprise-style layout direction already proven in ZyphoNote

### 3.7 Development acceleration manager

- a separate local manager must supervise `dotnet watch`
- manager output must be machine-readable and queryable
- manager must expose status, history, ready semantics, and watch events for Codex
- manager must generate compressed source capsules from structured source comments
- manager must support a dev-only tuning mode
- tuning mode must be able to include screenshot or clipboard image context
- Codex work should only be considered review-ready after the app is watch-ready again

## 4. Non-negotiable principles

These principles appear repeatedly across the source inputs and should be treated as mandatory:

- C# stays authoritative for business logic
- JavaScript workbench engines are adapters, not domain owners
- secrets must stay protected
- prompt reuse must be centrally governed
- the application must remain modular and extensible
- the UI must feel coherent and comfortable for daily work
- implementation prompts for Codex must be split into manageable, sequential slices

## 5. Implication for auditing

The current implementation should be evaluated against the intended workstation model, not merely against whether pages compile or save records.
