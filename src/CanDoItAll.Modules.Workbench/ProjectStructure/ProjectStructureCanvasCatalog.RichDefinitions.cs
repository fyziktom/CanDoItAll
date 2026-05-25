using CanDoItAll.Components.CanvasLib;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.Workbench;

internal static partial class ProjectStructureCanvasCatalog
{
    private static readonly IReadOnlyList<CanvasWorkbenchInputOption> MeetingChannelOptions =
    [
        Option("msTeams", "MS Teams"),
        Option("googleMeet", "Google Meet"),
        Option("zoom", "Zoom"),
        Option("whatsApp", "WhatsApp"),
        Option("telegram", "Telegram")
    ];

    private static readonly IReadOnlyList<CanvasWorkbenchInputOption> RepeatCadenceOptions =
    [
        Option("none", "No repeat"),
        Option("daily", "Daily"),
        Option("weekly", "Weekly"),
        Option("biWeekly", "Every 2 weeks"),
        Option("monthly", "Monthly")
    ];

    private static readonly IReadOnlyList<CanvasWorkbenchInputOption> PythonProviderOptions =
    [
        Option("python", "Python"),
        Option("conda", "Conda")
    ];

    private static readonly IReadOnlyList<CanvasWorkbenchInputOption> RuntimeProtocolOptions =
    [
        Option("https", "HTTPS"),
        Option("http", "HTTP"),
        Option("both", "HTTP + HTTPS")
    ];

    private static readonly IReadOnlyList<CanvasWorkbenchInputOption> DeliveryChannelOptions =
    [
        Option("none", "None"),
        Option("email", "Email"),
        Option("whatsApp", "WhatsApp"),
        Option("telegram", "Telegram"),
        Option("teams", "Teams"),
        Option("sms", "SMS")
    ];

    private static readonly IReadOnlyList<CanvasWorkbenchInputOption> SendKindOptions =
    [
        Option("file", "File"),
        Option("offer", "Offer"),
        Option("email", "Email"),
        Option("message", "Message"),
        Option("invoice", "Invoice"),
        Option("money", "Money")
    ];

    private static readonly IReadOnlyList<CanvasWorkbenchInputOption> DnsRecordTypeOptions =
    [
        Option("A", "A"),
        Option("AAAA", "AAAA"),
        Option("CNAME", "CNAME"),
        Option("TXT", "TXT"),
        Option("MX", "MX")
    ];

    private static readonly IReadOnlyList<CanvasWorkbenchInputOption> DockerModeOptions =
    [
        Option("compose", "Compose"),
        Option("swarm", "Swarm"),
        Option("devContainer", "Dev container")
    ];

    private static readonly IReadOnlyList<CanvasWorkbenchInputOption> DatabaseTypeOptions =
    [
        Option("postgresql", "PostgreSQL"),
        Option("sqlServer", "SQL Server"),
        Option("mysql", "MySQL")
    ];

    private static readonly IReadOnlyList<CanvasWorkbenchInputOption> AiReferenceOptions =
    [
        Option("chatGptConversation", "ChatGPT conversation"),
        Option("codexThread", "Codex thread"),
        Option("localLlm", "Local LLM")
    ];

    private static readonly IReadOnlyList<CanvasWorkbenchInputOption> StoragePurposeOptions =
    [
        Option(nameof(StorageUsagePurpose.ProjectAsset), StoragePresentation.DescribeUsagePurpose(StorageUsagePurpose.ProjectAsset)),
        Option(nameof(StorageUsagePurpose.PromptAttachment), StoragePresentation.DescribeUsagePurpose(StorageUsagePurpose.PromptAttachment)),
        Option(nameof(StorageUsagePurpose.PromptExport), StoragePresentation.DescribeUsagePurpose(StorageUsagePurpose.PromptExport)),
        Option(nameof(StorageUsagePurpose.Evidence), StoragePresentation.DescribeUsagePurpose(StorageUsagePurpose.Evidence)),
        Option(nameof(StorageUsagePurpose.RecordingMedia), StoragePresentation.DescribeUsagePurpose(StorageUsagePurpose.RecordingMedia)),
        Option(nameof(StorageUsagePurpose.SnapshotPackage), StoragePresentation.DescribeUsagePurpose(StorageUsagePurpose.SnapshotPackage)),
        Option(nameof(StorageUsagePurpose.ReleasePackage), StoragePresentation.DescribeUsagePurpose(StorageUsagePurpose.ReleasePackage)),
        Option(nameof(StorageUsagePurpose.DeploymentMirror), StoragePresentation.DescribeUsagePurpose(StorageUsagePurpose.DeploymentMirror))
    ];

    private static readonly ProjectStructureCreateLeafDefinition[] RichCreateLeafDefinitions =
    [
        new("add-block-deployment", ProjectObjectType.ProjectBlock, "deployment", "blocks", "Deployment block", "Map rollout work, release gates, and deployment readiness.", "ship", "warn", "Deployment block", "Block name", "Deployment lane", "Release", "Target release or environment", "Description", "What this deployment block covers"),
        new("add-block-repos", ProjectObjectType.ProjectBlock, "repos", "blocks", "Repos block", "Group repository, branch, and source-control concerns.", "repo", "mint", "Repos block", "Block name", "Repository map", "Scope", "App, service, or workspace", "Description", "How repositories are organized"),
        new("add-block-dockers", ProjectObjectType.ProjectBlock, "dockers", "blocks", "Dockers block", "Keep container topology, compose layers, and runtime hosting visible.", "docker", "accent", "Dockers block", "Block name", "Container stack", "Mode", "Compose, swarm, dev container...", "Description", "What this docker block tracks"),
        new("add-block-task-flow", ProjectObjectType.ProjectBlock, "task-flow", "blocks", "Task flow block", "Describe delivery flow, ownership lanes, and throughput checkpoints.", "flow", "primary", "Task flow block", "Block name", "Delivery flow", "Owner", "Team or stream", "Description", "How work moves through this flow"),
        new("add-block-backlog", ProjectObjectType.ProjectBlock, "backlog", "blocks", "Backlog block", "Group pending work, prioritization, and intake for future delivery.", "backlog", "sky", "Backlog block", "Block name", "Backlog lane", "Owner", "Queue owner", "Description", "What belongs in this backlog"),
        new("add-block-server", ProjectObjectType.ProjectBlock, "server", "blocks", "Server block", "Group server operations, environments, and infrastructure planning.", "server", "danger", "Server block", "Block name", "Server lane", "Scope", "Cluster, host, or service", "Description", "What this server block represents"),
        new("add-block-computer", ProjectObjectType.ProjectBlock, "computer", "blocks", "Computer block", "Group workstation, machine, or endpoint responsibilities without hiding them in generic notes.", "computer", "primary", "Computer block", "Block name", "Computer lane", "Scope", "Machine, workstation, or device", "Description", "What this computer block represents"),
        new("add-block-router", ProjectObjectType.ProjectBlock, "router", "blocks", "Router block", "Track router topology, routing rules, and network ownership on the canvas.", "router", "sky", "Router block", "Block name", "Router lane", "Scope", "Gateway, VLAN, or network segment", "Description", "What this router block covers"),
        new("add-block-wifi", ProjectObjectType.ProjectBlock, "wifi", "blocks", "WiFi block", "Track WiFi coverage, SSID planning, and wireless rollout work as a typed block.", "wifi", "mint", "WiFi block", "Block name", "WiFi lane", "Scope", "SSID, AP zone, or wireless slice", "Description", "What this WiFi block represents"),

        new(
            "add-meeting-online",
            ProjectObjectType.Meeting,
            "online",
            "meetings",
            "Online meeting",
            "Create an online meeting with schedule, channel, and join metadata.",
            "meeting",
            "sky",
            "Online meeting",
            "Meeting",
            "Weekly sync",
            "Agenda",
            "Topic or owner",
            "Notes",
            "What this meeting is for",
            false,
            string.Empty,
            "Drop a file here or choose one.",
            true,
            "Schedule meeting",
            [
                Field("startUtc", "Start", "datetime-local", "Choose start", true),
                Field("endUtc", "End", "datetime-local", "Choose end"),
                Field("channel", "Channel", "select", "Choose channel", true, MeetingChannelOptions),
                Field("meetingUrl", "Meeting URL", "url", "https://..."),
                Field("repeatCadence", "Repeat", "select", "Choose cadence", false, RepeatCadenceOptions),
                Field("participantRef", "Lead participant", "select", "Optional participant")
            ]),
        new(
            "add-meeting-onsite",
            ProjectObjectType.Meeting,
            "onsite",
            "meetings",
            "Onsite meeting",
            "Create an onsite meeting with address, map, and repeat metadata.",
            "location",
            "warn",
            "Onsite meeting",
            "Meeting",
            "Client workshop",
            "Agenda",
            "Topic or owner",
            "Notes",
            "What this meeting is for",
            false,
            string.Empty,
            "Drop a file here or choose one.",
            true,
            "Schedule meeting",
            [
                Field("startUtc", "Start", "datetime-local", "Choose start", true),
                Field("endUtc", "End", "datetime-local", "Choose end"),
                Field("address", "Address", "text", "Office, building, room", true),
                Field("mapUrl", "Map URL", "url", "https://maps.google.com/..."),
                Field("repeatCadence", "Repeat", "select", "Choose cadence", false, RepeatCadenceOptions),
                Field("participantRef", "Lead participant", "select", "Optional participant")
            ]),
        new(
            "add-recording",
            ProjectObjectType.Recording,
            string.Empty,
            "meetings",
            "Recording",
            "Track a meeting or standalone recording with source and storage metadata.",
            "recording",
            "accent",
            "Recording",
            "Recording",
            "Discovery call capture",
            "Source",
            "Camera, Teams, Zoom, phone...",
            "Notes",
            "Why this recording matters",
            false,
            string.Empty,
            "Drop a file here or choose one.",
            true,
            "Add recording",
            [
                Field("meetingRef", "Meeting", "select", "Optional meeting"),
                Field("recordingSource", "Recording source", "text", "Teams recording"),
                Field("storageReference", "Storage reference", "text", "SharePoint, folder, URL"),
                Field("durationMinutes", "Minutes", "number", "45")
            ]),
        new(
            "add-transcript",
            ProjectObjectType.Transcript,
            string.Empty,
            "meetings",
            "Transcript",
            "Create a transcript under a recording or as a standalone text artifact.",
            "transcript",
            "mint",
            "Transcript",
            "Transcript",
            "Client call transcript",
            "Summary",
            "Owner or context",
            "Transcript text",
            "Paste the transcript or a strong summary",
            false,
            string.Empty,
            "Drop a file here or choose one.",
            true,
            "Add transcript",
            [
                Field("recordingRef", "Recording", "select", "Optional recording"),
                Field("transcriptText", "Transcript", "textarea", "Paste transcript text")
            ]),

        new("add-participant-hr", ProjectObjectType.Participant, "hr", "people", "HR", "Add a reusable HR participant record.", "person", "sky", "HR contact", "Name", "HR partner", "Role", "Recruitment partner", "Notes", "Context for this participant", false, string.Empty, "Drop a file here or choose one.", true, "Add participant", ParticipantFields(ProjectParticipantKind.Hr)),
        new("add-participant-team-block", ProjectObjectType.Participant, "team-block", "people", "Team block", "Create a team-block node for a lightweight org chart.", "org", "primary", "Team block", "Name", "Platform team", "Role", "Department or stream", "Notes", "What this team block covers", false, string.Empty, "Drop a file here or choose one.", true, "Add participant", ParticipantFields(ProjectParticipantKind.TeamBlock)),
        new("add-participant-team-section", ProjectObjectType.Participant, "team-section", "people", "Team section", "Create a team-section node for a nested team structure.", "section", "primary", "Team section", "Name", "Backend section", "Role", "Capability or focus", "Notes", "How this section is organized", false, string.Empty, "Drop a file here or choose one.", true, "Add participant", ParticipantFields(ProjectParticipantKind.TeamSection)),
        new("add-participant-freelancer", ProjectObjectType.Participant, "freelancer", "people", "Freelancer", "Add a freelancer participant that can be reused by meetings and tasks.", "freelancer", "accent", "Freelancer", "Name", "Contract designer", "Role", "Specialist", "Notes", "Availability or engagement notes", false, string.Empty, "Drop a file here or choose one.", true, "Add participant", ParticipantFields(ProjectParticipantKind.Freelancer)),
        new("add-participant-partner", ProjectObjectType.Participant, "partner", "people", "Partner", "Add a partner organization or external collaborator.", "partner", "mint", "Partner", "Name", "Agency partner", "Role", "Partner role", "Notes", "What this partner contributes", false, string.Empty, "Drop a file here or choose one.", true, "Add participant", ParticipantFields(ProjectParticipantKind.Partner)),
        new("add-participant-ai-agent", ProjectObjectType.Participant, "ai-agent", "people", "AI agent", "Add an AI agent participant with a distinct reusable identity.", "ai", "accent", "AI agent", "Name", "Release copilot", "Role", "Analysis agent", "Notes", "Capabilities, scope, or model notes", false, string.Empty, "Drop a file here or choose one.", true, "Add participant", ParticipantFields(ProjectParticipantKind.AiAgent)),

        new("add-work-task", ProjectObjectType.WorkItem, "task", "work", "Task", "Track concrete work with assignee, due date, and optional repository linkage.", "task", "warn", "Task", "Task", "Implement export flow", "Status lane", "Sprint or owner", "Notes", "Definition of done or context", false, string.Empty, "Drop a file here or choose one.", true, "Add work item", WorkItemFields(ProjectWorkItemKind.Task)),
        new("add-work-issue", ProjectObjectType.WorkItem, "issue", "work", "Issue", "Capture a delivery issue with optional repository linkage and ownership.", "issue", "danger", "Issue", "Issue", "Toolbar overlap", "Severity", "P1, P2, blocked...", "Notes", "Describe the issue clearly", false, string.Empty, "Drop a file here or choose one.", true, "Add work item", WorkItemFields(ProjectWorkItemKind.Issue)),
        new("add-work-revision", ProjectObjectType.WorkItem, "revision", "work", "Revision", "Track a revision loop or rework pass explicitly.", "revision", "accent", "Revision", "Revision", "Revise onboarding copy", "Cycle", "Revision round", "Notes", "What must change", false, string.Empty, "Drop a file here or choose one.", true, "Add work item", WorkItemFields(ProjectWorkItemKind.Revision)),
        new("add-work-feedback", ProjectObjectType.WorkItem, "feedback", "work", "Feedback", "Capture structured feedback items without hiding them in notes.", "feedback", "sky", "Feedback", "Feedback", "Client feedback", "Source", "Reviewer or channel", "Notes", "The feedback itself", false, string.Empty, "Drop a file here or choose one.", true, "Add work item", WorkItemFields(ProjectWorkItemKind.Feedback)),
        new("add-work-payment", ProjectObjectType.WorkItem, "payment", "work", "Payment", "Track a payment request or status with amount and currency.", "payment", "mint", "Payment", "Payment", "Milestone invoice", "Status lane", "Pending, sent, paid...", "Notes", "Commercial context", false, string.Empty, "Drop a file here or choose one.", true, "Add work item", PaymentFields()),
        new("add-work-send", ProjectObjectType.WorkItem, "send", "work", "Send", "Track delivery intent, channel, and recipient handoff explicitly.", "send", "primary", "Send", "Send", "Send offer", "Recipient", "Who receives it", "Notes", "What is being sent", false, string.Empty, "Drop a file here or choose one.", true, "Add work item", SendFields()),

        new("add-file-pdf", ProjectObjectType.File, "pdf", "assets", "PDF", "Create a PDF file node with the correct visual profile.", "pdf", "danger", "PDF file", "File title", "Architecture spec", "Folder", "docs/architecture", "Purpose", "What this PDF explains", true, ".pdf,application/pdf", "Drop a PDF here or choose one.", true, "Attach PDF"),
        new("add-file-excel", ProjectObjectType.File, "excel", "assets", "Excel", "Create a spreadsheet node with typed visual differentiation.", "excel", "mint", "Spreadsheet", "File title", "Budget sheet", "Folder", "finance/reports", "Purpose", "What this spreadsheet tracks", true, ".xls,.xlsx,.csv,application/vnd.ms-excel,application/vnd.openxmlformats-officedocument.spreadsheetml.sheet,text/csv", "Drop a spreadsheet here or choose one.", true, "Attach spreadsheet"),
        new("add-file-docx", ProjectObjectType.File, "docx", "assets", "Docx", "Create a document node with Word-specific labeling.", "docx", "primary", "Word document", "File title", "Project brief", "Folder", "docs/briefs", "Purpose", "What this document covers", true, ".doc,.docx,application/vnd.openxmlformats-officedocument.wordprocessingml.document", "Drop a document here or choose one.", true, "Attach document"),
        new("add-file-text", ProjectObjectType.File, "text", "assets", "Text", "Create a plain text node for notes, configs, or snippets.", "text", "neutral", "Text file", "File title", "Runbook", "Folder", "docs/runbooks", "Content", "Paste or describe the text content"),
        new("add-file-json", ProjectObjectType.File, "json", "assets", "JSON", "Create a JSON node with structured file semantics.", "json", "neutral", "JSON file", "File title", "appsettings override", "Folder", "config", "Content", "Paste or describe the JSON content"),
        new("add-file-markdown", ProjectObjectType.File, "markdown", "assets", "Markdown", "Create a markdown node for docs, specs, and notes.", "markdown", "sky", "Markdown file", "File title", "README draft", "Folder", "docs", "Content", "Paste markdown content", true, ".md,.markdown,.txt,text/markdown,text/plain", "Paste markdown below, drop a markdown file here, or choose one.", true, "Add markdown"),
        new("add-file-mermaid", ProjectObjectType.File, "mermaid", "assets", "Mermaid", "Create a Mermaid diagram node with detected diagram type feedback.", "mermaid", "accent", "Mermaid diagram", "Diagram title", "Deployment flow", "Folder", "docs/diagrams", "Diagram", "Paste Mermaid source", false, string.Empty, "Drop a file here or choose one.", true, "Add diagram", [Field("mermaidText", "Mermaid source", "textarea", "graph TD\nA[Start] --> B[Done]", true)]),
        new("add-file-screenshot", ProjectObjectType.File, "screenshot", "assets", "Screenshot", "Attach a screenshot using clipboard, drag-drop, or file selection.", "screenshot", "danger", "Screenshot", "Image title", "UI regression capture", "Usage", "What this screenshot proves", "Notes", "Any validation context", true, "image/*", "Paste from clipboard, drop a screenshot, or choose an image.", true, "Attach screenshot"),
        new("add-file-log", ProjectObjectType.File, "log", "assets", "Log", "Attach a typed log file so operational evidence is explicit.", "log", "neutral", "Log file", "File title", "build-output.log", "Usage", "Runtime or job name", "Notes", "What this log shows", true, ".log,.txt,text/plain", "Drop a log file here or choose one.", true, "Attach log"),
        new("add-file-archive", ProjectObjectType.File, "archive", "assets", "Archive", "Attach a typed archive or bundle artifact.", "archive", "primary", "Archive", "File title", "release-bundle", "Usage", "Where this archive belongs", "Notes", "What the archive contains", true, ".zip,.rar,.7z,application/zip", "Drop an archive here or choose one.", true, "Attach archive"),
        new("add-file-audio", ProjectObjectType.File, "audio", "assets", "Audio", "Attach an audio file without collapsing it into a generic upload.", "audio", "accent", "Audio file", "File title", "Interview clip", "Usage", "Where this audio belongs", "Notes", "What the clip contains", true, "audio/*", "Drop an audio file here or choose one.", true, "Attach audio"),

        new("add-repository-remote", ProjectObjectType.Repository, "remote", "runtime", "Remote repo", "Model a remote GitHub repository with branch and path details.", "repo", "mint", "Remote repository", "Repo name", "frontend-app", "Owner / org", "team/repository", "Purpose", "What this repo is for", false, string.Empty, "Drop a file here or choose one.", true, "Add repository", [Field("repositoryMode", "Mode", "select", "Choose mode", true, [Option("remoteGitHub", "Remote GitHub")]), Field("repositoryUrl", "Repository URL", "url", "https://github.com/org/repo", true), Field("defaultBranch", "Default branch", "text", "main"), Field("relativePath", "Relative path", "text", "src/Web")], [DefaultValue("repositoryMode", "remoteGitHub")]),
        new("add-repository-local", ProjectObjectType.Repository, "local", "runtime", "Local repo", "Track a local repository with a manual path fallback.", "folder", "primary", "Local repository", "Repo name", "CanDoItAll", "Owner / path", "Workspace folder", "Purpose", "What this repo is for", false, string.Empty, "Drop a file here or choose one.", true, "Add repository", [Field("repositoryMode", "Mode", "select", "Choose mode", true, [Option("localRepository", "Local repository")]), Field("localPath", "Local path", "text", "C:\\repositories\\CanDoItAll", true), Field("defaultBranch", "Default branch", "text", "main"), Field("relativePath", "Relative path", "text", "src/Web")], [DefaultValue("repositoryMode", "localRepository")]),
        new("add-repository-folder", ProjectObjectType.Repository, "folder", "runtime", "Folder", "Track a local folder path that can be opened in File Explorer and reused as runtime context.", "folder", "sky", "Local folder", "Folder label", "Shared assets", "Folder path", "C:\\repositories\\shared-assets", "Purpose", "Why this folder matters", false, string.Empty, "Drop a file here or choose one.", true, "Add folder", [Field("repositoryMode", "Mode", "select", "Choose mode", true, [Option("localFolder", "Local folder")]), Field("localPath", "Folder path", "text", "C:\\repositories\\shared-assets", true), Field("relativePath", "Relative path", "text", "docs")], [DefaultValue("repositoryMode", "localFolder")]),
        new("add-script-powershell", ProjectObjectType.Script, "powershell", "runtime", "PowerShell script", "Track a PowerShell command and working directory with a realistic terminal handoff.", "powershell", "primary", "PowerShell script", "Script title", "Apply migrations", "Context", "Project or owner", "Notes", "What this script does", false, string.Empty, "Drop a file here or choose one.", true, "Add script", ScriptFields(ProjectScriptKind.PowerShell)),
        new("add-script-console", ProjectObjectType.Script, "console", "runtime", "Console script", "Track a console command with explicit path and working directory metadata.", "console", "accent", "Console script", "Script title", "Seed data", "Context", "Project or owner", "Notes", "What this command does", false, string.Empty, "Drop a file here or choose one.", true, "Add script", ScriptFields(ProjectScriptKind.Console)),
        new("add-script-ef-migration", ProjectObjectType.Script, "ef-migration", "runtime", "EF migration", "Store explicit EF migration commands and run context.", "migration", "warn", "EF migration", "Script title", "Add InitialCreate", "Context", "Project or db context", "Notes", "What this migration command should do", false, string.Empty, "Drop a file here or choose one.", true, "Add script", ScriptFields(ProjectScriptKind.EfMigration, "dotnet ef migrations add InitialCreate")),
        new("add-script-tailwind-watch", ProjectObjectType.Script, "tailwind-watch", "runtime", "Tailwind watch", "Store project-specific Tailwind watch commands on the same execution surface.", "tailwind", "mint", "Tailwind watch", "Script title", "Tailwind watch", "Context", "Project or theme owner", "Notes", "What this watch command builds", false, string.Empty, "Drop a file here or choose one.", true, "Add script", ScriptFields(ProjectScriptKind.TailwindWatch, "npx tailwindcss -i ./input.css -o ./output.css --watch")),
        new("add-environment-python", ProjectObjectType.Environment, "python", "runtime", "Python environment", "Persist provider and environment name for Python tooling.", "python", "mint", "Python environment", "Environment", ".venv", "Context", "Repo or project", "Notes", "How this environment is used", false, string.Empty, "Drop a file here or choose one.", true, "Add environment", [Field("environmentKind", "Kind", "select", "Choose environment", true, [Option("pythonEnvironment", "Python environment")]), Field("pythonProvider", "Provider", "select", "Choose provider", true, PythonProviderOptions), Field("environmentName", "Name", "text", ".venv", true), Field("projectPath", "Project path", "text", "C:\\repositories\\CanDoItAll")], [DefaultValue("environmentKind", "pythonEnvironment")]),
        new("add-environment-dotnet-runtime", ProjectObjectType.Environment, "dotnet-runtime", "runtime", ".NET runtime", "Model a runtime node backed by launch settings and localhost URLs.", "dotnet", "primary", ".NET runtime", "Runtime", "Web app runtime", "Context", "Project or service", "Notes", "Runtime context", false, string.Empty, "Drop a file here or choose one.", true, "Add environment", DotNetEnvironmentFields(ProjectEnvironmentKind.DotNetRuntime)),
        new("add-environment-dotnet-watch", ProjectObjectType.Environment, "dotnet-watch", "runtime", "dotnet watch", "Model a dotnet watch runtime with launch profile and localhost inference.", "watch", "accent", "dotnet watch", "Runtime", "dotnet watch", "Context", "Project or service", "Notes", "Runtime context", false, string.Empty, "Drop a file here or choose one.", true, "Add environment", DotNetEnvironmentFields(ProjectEnvironmentKind.DotNetWatch)),
        new("add-environment-dotnet-release", ProjectObjectType.Environment, "dotnet-release", "runtime", "Release run", "Model a release runtime and explicit protocol preferences.", "release", "warn", "Release runtime", "Runtime", "Release host", "Context", "Project or service", "Notes", "Runtime context", false, string.Empty, "Drop a file here or choose one.", true, "Add environment", DotNetEnvironmentFields(ProjectEnvironmentKind.DotNetRelease)),

        new("add-infrastructure-server", ProjectObjectType.Infrastructure, "remote-server", "infrastructure", "Remote server", "Capture host, provider, account, and capacity details without embedding credentials.", "server", "danger", "Remote server", "Server", "Primary VPS", "Environment", "Production / staging", "Notes", "Operational or business context", false, string.Empty, "Drop a file here or choose one.", true, "Add infrastructure", [Field("infrastructureKind", "Kind", "select", "Choose infrastructure", true, [Option("remoteServer", "Remote server")]), Field("host", "Host", "text", "server.example.com", true), Field("port", "Port", "number", "22"), Field("providerName", "Provider", "text", "Hetzner"), Field("providerUrl", "Provider URL", "url", "https://..."), Field("loginUrl", "Login URL", "url", "https://..."), Field("accountName", "Account", "text", "ops@example.com"), Field("cpuCores", "CPU", "number", "4"), Field("memoryGb", "RAM (GB)", "number", "16"), Field("storageGb", "Storage (GB)", "number", "160"), Field("monthlyPrice", "Monthly price", "number", "24"), Field("secretRef", "Secret reference", "select", "Optional secret")], [DefaultValue("infrastructureKind", "remoteServer")]),
        new("add-infrastructure-domain", ProjectObjectType.Infrastructure, "domain", "infrastructure", "Domain", "Track connected domains and ownership explicitly.", "domain", "sky", "Domain", "Domain", "app.example.com", "Owner", "Registrar or owner", "Notes", "Any renewal or routing context", false, string.Empty, "Drop a file here or choose one.", true, "Add infrastructure", [Field("infrastructureKind", "Kind", "select", "Choose infrastructure", true, [Option("domain", "Domain")]), Field("domainName", "Domain", "text", "app.example.com", true), Field("ownerName", "Owner", "text", "Example Ltd")], [DefaultValue("infrastructureKind", "domain")]),
        new("add-infrastructure-dns", ProjectObjectType.Infrastructure, "dns-record", "infrastructure", "DNS record", "Track DNS records as typed infrastructure nodes.", "dns", "accent", "DNS record", "Record", "A app.example.com", "Owner", "Zone or owner", "Notes", "Any propagation or provider context", false, string.Empty, "Drop a file here or choose one.", true, "Add infrastructure", [Field("infrastructureKind", "Kind", "select", "Choose infrastructure", true, [Option("dnsRecord", "DNS record")]), Field("domainName", "Domain", "text", "app.example.com", true), Field("dnsRecordType", "Type", "select", "Choose record type", true, DnsRecordTypeOptions), Field("dnsRecordValue", "Value", "text", "192.0.2.10", true)], [DefaultValue("infrastructureKind", "dnsRecord")]),
        new("add-infrastructure-docker", ProjectObjectType.Infrastructure, "docker-mode", "infrastructure", "Docker", "Track Docker mode, command, and working directory so it can launch from project structure.", "docker", "mint", "Docker runtime", "Docker", "Compose stack", "Environment", "Host or repo", "Notes", "Any container context", false, string.Empty, "Drop a file here or choose one.", true, "Add infrastructure", [Field("infrastructureKind", "Kind", "select", "Choose infrastructure", true, [Option("dockerMode", "Docker")]), Field("dockerMode", "Mode", "select", "Compose or swarm", true, DockerModeOptions), Field("runtimeCommand", "Runtime command", "text", "docker compose up", true), Field("runtimeArguments", "Arguments", "text", "--build"), Field("workingDirectory", "Working directory", "text", "C:\\repositories\\CanDoItAll"), Field("folderPath", "Folder path", "text", "C:\\repositories\\CanDoItAll"), Field("proxyProvider", "Proxy provider", "text", "Traefik")], [DefaultValue("infrastructureKind", "dockerMode"), DefaultValue("runtimeCommand", "docker compose up")]),
        new("add-infrastructure-database", ProjectObjectType.Infrastructure, "database", "infrastructure", "Database", "Track database type and connection reference as typed infrastructure.", "database", "primary", "Database", "Database", "PostgreSQL", "Environment", "Host or service", "Notes", "Any schema or migration context", false, string.Empty, "Drop a file here or choose one.", true, "Add infrastructure", [Field("infrastructureKind", "Kind", "select", "Choose infrastructure", true, [Option("database", "Database")]), Field("databaseType", "Database type", "select", "Choose database", true, DatabaseTypeOptions), Field("connectionReference", "Connection reference", "text", "Server=...;Database=..."), Field("secretRef", "Secret reference", "select", "Optional secret")], [DefaultValue("infrastructureKind", "database")]),
        new("add-infrastructure-folder", ProjectObjectType.Infrastructure, "deployment-folder", "infrastructure", "Deployment folder", "Track deployment folders explicitly on the infrastructure subtree.", "folder", "primary", "Deployment folder", "Folder", "/srv/app/current", "Environment", "Host or service", "Notes", "Any mount or sync context", false, string.Empty, "Drop a file here or choose one.", true, "Add infrastructure", [Field("infrastructureKind", "Kind", "select", "Choose infrastructure", true, [Option("deploymentFolder", "Deployment folder")]), Field("folderPath", "Folder path", "text", "/srv/app/current", true)], [DefaultValue("infrastructureKind", "deploymentFolder")]),
        new("add-infrastructure-storage", ProjectObjectType.Infrastructure, "storage-system", "infrastructure", "Storage", "Track workspace storage lanes, usage purpose, and path ownership as typed infrastructure.", "storage", "mint", "Storage lane", "Storage", "Project assets lane", "Purpose", "Storage role or owner", "Notes", "What this storage lane supports", false, string.Empty, "Drop a file here or choose one.", true, "Add infrastructure", [Field("infrastructureKind", "Kind", "select", "Choose infrastructure", true, [Option("storageSystem", "Storage")]), Field("storageCatalogId", "Storage catalog", "select", "Choose storage target", true), Field("storagePurpose", "Usage purpose", "select", "Choose purpose", true, StoragePurposeOptions), Field("storagePathPrefix", "Path prefix", "text", "projects/demo/assets"), Field("connectionReference", "Connection reference", "text", "/storage/assets")], [DefaultValue("infrastructureKind", "storageSystem"), DefaultValue("storagePurpose", nameof(StorageUsagePurpose.ProjectAsset))]),
        new("add-infrastructure-key", ProjectObjectType.Infrastructure, "key-reference", "infrastructure", "Key reference", "Track a key or secret reference without storing the secret itself.", "key", "danger", "Key reference", "Key", "Deployment key", "Owner", "Who owns this key", "Notes", "Usage context", false, string.Empty, "Drop a file here or choose one.", true, "Add infrastructure", [Field("infrastructureKind", "Kind", "select", "Choose infrastructure", true, [Option("keyReference", "Key reference")]), Field("connectionReference", "Reference", "text", "GitHub Actions secret", true), Field("secretRef", "Secret reference", "select", "Optional secret")], [DefaultValue("infrastructureKind", "keyReference")]),
        new("add-infrastructure-ai", ProjectObjectType.Infrastructure, "ai-link", "infrastructure", "AI link", "Track AI conversations and local LLM references as typed infrastructure nodes.", "ai", "accent", "AI link", "Reference", "ChatGPT design review", "Owner", "Project or operator", "Notes", "Why this AI reference matters", false, string.Empty, "Drop a file here or choose one.", true, "Add infrastructure", [Field("infrastructureKind", "Kind", "select", "Choose infrastructure", true, [Option("aiLink", "AI link")]), Field("aiReferenceKind", "Reference kind", "select", "Choose AI reference", true, AiReferenceOptions), Field("aiReferenceUrl", "Reference URL", "url", "https://..."), Field("providerName", "Tool", "text", "ChatGPT / Codex / Ollama")], [DefaultValue("infrastructureKind", "aiLink")])
    ];

    private static IReadOnlyList<ProjectStructureCreateLeafDefinition> ResolveMeetingLeafDefinitions()
        => ResolveLeafDefinitions("add-meeting-online", "add-meeting-onsite", "add-recording", "add-transcript");

    private static IReadOnlyList<ProjectStructureCreateLeafDefinition> ResolveParticipantLeafDefinitions()
        => ResolveLeafDefinitions("add-participant-hr", "add-participant-team-block", "add-participant-team-section", "add-participant-freelancer", "add-participant-partner", "add-participant-ai-agent");

    private static IReadOnlyList<ProjectStructureCreateLeafDefinition> ResolveWorkLeafDefinitions()
        => ResolveLeafDefinitions("add-work-task", "add-work-issue", "add-work-revision", "add-work-feedback", "add-work-payment", "add-work-send");

    private static IReadOnlyList<ProjectStructureCreateLeafDefinition> ResolveRuntimeLeafDefinitions()
        => ResolveLeafDefinitions(
            "add-repository-remote",
            "add-repository-local",
            "add-repository-folder",
            "add-script-powershell",
            "add-script-console",
            "add-script-ef-migration",
            "add-script-tailwind-watch",
            "add-environment-python",
            "add-environment-dotnet-runtime",
            "add-environment-dotnet-watch",
            "add-environment-dotnet-release");

    private static IReadOnlyList<ProjectStructureCreateLeafDefinition> ResolveInfrastructureLeafDefinitions()
        => ResolveLeafDefinitions(
            "add-infrastructure-server",
            "add-infrastructure-domain",
            "add-infrastructure-dns",
            "add-infrastructure-docker",
            "add-infrastructure-database",
            "add-infrastructure-folder",
            "add-infrastructure-storage",
            "add-infrastructure-key",
            "add-infrastructure-ai");

    private static IReadOnlyList<ProjectStructureCreateLeafDefinition> ResolveLeafDefinitions(params string[] actionIds)
        => actionIds.Select(actionId => CreateLeafByActionId[actionId]).ToList();

    private static IReadOnlyList<CanvasWorkbenchInputField> ParticipantFields(ProjectParticipantKind participantKind)
        =>
        [
            Field("participantKind", "Kind", "select", "Choose participant kind", true, [Option(ToCamelCase(participantKind.ToString()), ResolveParticipantKindLabel(participantKind))]),
            Field("role", "Role", "text", "Role or responsibility"),
            Field("organization", "Organization", "text", "Company, team, or org"),
            Field("email", "Email", "email", "name@example.com"),
            Field("phone", "Phone", "tel", "+1 555 123 4567")
        ];

    private static IReadOnlyList<CanvasWorkbenchInputField> WorkItemFields(ProjectWorkItemKind kind)
        =>
        [
            Field("workItemKind", "Kind", "select", "Choose work item kind", true, [Option(ToCamelCase(kind.ToString()), ResolveWorkItemKindLabel(kind))]),
            Field("assigneeRef", "Assignee", "select", "Optional participant"),
            Field("dueUtc", "Due", "datetime-local", "Choose due date"),
            Field("repositoryRef", "Repository", "select", "Optional repository")
        ];

    private static IReadOnlyList<CanvasWorkbenchInputField> PaymentFields()
        =>
        [
            ..WorkItemFields(ProjectWorkItemKind.Payment),
            Field("amount", "Amount", "number", "1000"),
            Field("currencyCode", "Currency", "text", "USD")
        ];

    private static IReadOnlyList<CanvasWorkbenchInputField> SendFields()
        =>
        [
            Field("workItemKind", "Kind", "select", "Choose work item kind", true, [Option("send", "Send")]),
            Field("sendKind", "Intent", "select", "Choose delivery intent", true, SendKindOptions),
            Field("deliveryChannel", "Channel", "select", "Choose channel", false, DeliveryChannelOptions),
            Field("assigneeRef", "Recipient", "select", "Optional participant"),
            Field("dueUtc", "Due", "datetime-local", "Choose due date")
        ];

    private static IReadOnlyList<CanvasWorkbenchInputField> ScriptFields(ProjectScriptKind kind, string? defaultCommand = null)
        =>
        [
            Field("scriptKind", "Kind", "select", "Choose script kind", true, [Option(ToCamelCase(kind.ToString()), ResolveScriptKindLabel(kind))]),
            Field("scriptPath", "Script path", "text", ".\\scripts\\task.ps1"),
            Field("command", "Command", "text", defaultCommand ?? "pwsh ./scripts/task.ps1"),
            Field("arguments", "Arguments", "text", "--project src/Web"),
            Field("workingDirectory", "Working directory", "text", "C:\\repositories\\CanDoItAll")
        ];

    private static IReadOnlyList<CanvasWorkbenchInputField> DotNetEnvironmentFields(ProjectEnvironmentKind kind)
        =>
        [
            Field("environmentKind", "Kind", "select", "Choose environment", true, [Option(ToCamelCase(kind.ToString()), ResolveEnvironmentKindLabel(kind))]),
            Field("projectPath", "Project path", "text", "C:\\repositories\\CanDoItAll\\src\\CanDoItAll.Web\\CanDoItAll.Web.csproj", true),
            Field("launchProfileName", "Launch profile", "text", "https"),
            Field("runtimeProtocol", "Protocol", "select", "Choose protocol", false, RuntimeProtocolOptions),
            Field("localhostUrl", "Localhost URL", "url", "https://localhost:5001")
        ];

    private static CanvasWorkbenchInputField Field(
        string key,
        string label,
        string inputMode = "text",
        string placeholder = "",
        bool isRequired = false,
        IReadOnlyList<CanvasWorkbenchInputOption>? options = null)
        => new()
        {
            Key = key,
            Label = label,
            InputMode = inputMode,
            Placeholder = placeholder,
            IsRequired = isRequired,
            Options = options?.ToList() ?? []
        };

    private static CanvasWorkbenchInputOption Option(string value, string label)
        => new()
        {
            Value = value,
            Label = label
        };

    private static CanvasWorkbenchInputValue DefaultValue(string key, string value)
        => new()
        {
            Key = key,
            Value = value
        };

    private static string ResolveParticipantKindLabel(ProjectParticipantKind kind)
        => kind switch
        {
            ProjectParticipantKind.TeamBlock => "Team block",
            ProjectParticipantKind.TeamSection => "Team section",
            ProjectParticipantKind.AiAgent => "AI agent",
            _ => kind.ToString()
        };

    private static string ResolveWorkItemKindLabel(ProjectWorkItemKind kind)
        => kind.ToString();

    private static string ResolveScriptKindLabel(ProjectScriptKind kind)
        => kind switch
        {
            ProjectScriptKind.PowerShell => "PowerShell",
            ProjectScriptKind.EfMigration => "EF migration",
            ProjectScriptKind.TailwindWatch => "Tailwind watch",
            _ => kind.ToString()
        };

    private static string ResolveEnvironmentKindLabel(ProjectEnvironmentKind kind)
        => kind switch
        {
            ProjectEnvironmentKind.PythonEnvironment => "Python environment",
            ProjectEnvironmentKind.DotNetRuntime => ".NET runtime",
            ProjectEnvironmentKind.DotNetWatch => "dotnet watch",
            ProjectEnvironmentKind.DotNetRelease => "Release run",
            _ => kind.ToString()
        };

    private static string ToCamelCase(string value)
        => string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : char.ToLowerInvariant(value[0]) + value[1..];
}
