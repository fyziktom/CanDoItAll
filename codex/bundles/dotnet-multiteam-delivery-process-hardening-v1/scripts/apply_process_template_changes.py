from __future__ import annotations

import json
from pathlib import Path

ROOT = Path(r"C:\repositories\CanDoItAll")
PROCESSES = ROOT / "Templates" / "Processes" / "processes"


def load_json(path: Path):
    with path.open("r", encoding="utf-8") as handle:
        return json.load(handle)


def save_json(path: Path, value) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("w", encoding="utf-8", newline="\n") as handle:
        json.dump(value, handle, indent=2, ensure_ascii=True)
        handle.write("\n")


def save_text(path: Path, value: str) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(value.rstrip() + "\n", encoding="utf-8", newline="\n")


def role(
    key: str,
    resource: str,
    display: str,
    purpose: str,
    staffing: str,
    *,
    kind: str = "agent",
    assignment: str = "Reviewer",
    required: bool = True,
    fallback: bool = True,
    approval: bool = False,
    percent: int = 60,
    x: int = 40,
    y: int = 40,
    notes: str = ""):
    result = {
        "Key": key,
        "RoleResourceKey": resource,
        "DisplayName": display,
        "Purpose": purpose,
        "StaffingIntent": staffing,
        "PreferredExecutorKind": kind,
        "PreferredProjectAssignmentRole": assignment,
        "IsRequired": required,
        "AllowsFallback": fallback,
        "RequiresExplicitApproval": approval,
        "DefaultAllocationPercent": percent,
        "CanvasX": x,
        "CanvasY": y
    }
    if notes:
        result["Notes"] = notes
    return result


def assignment(
    role_key: str,
    kind: str = "Responsible",
    *,
    required: bool = True,
    fallback: int = 0,
    rebind: str = "Rebind only to a role-compatible operator for this step."):
    return {
        "RoleKey": role_key,
        "ResponsibilityKind": kind,
        "IsRequired": required,
        "FallbackOrder": fallback,
        "RebindPolicySummary": rebind
    }


def artifact(
    key: str,
    title: str,
    *,
    template: str = "",
    kind: str = "Evidence",
    trust: str = "ReviewRequired",
    sensitivity: str = "Internal",
    days: int = 365,
    future: str = "Reusable by downstream delivery steps.",
    validation: str = "Must contain enough durable evidence for downstream review.",
    child_step: str = "",
    child_title: str = ""):
    result = {
        "Key": key,
        "TemplateKey": template,
        "Title": title,
        "ArtifactKind": kind,
        "IsRequired": True,
        "TrustRequirement": trust,
        "SensitivityLevel": sensitivity,
        "RetentionDays": days,
        "AllowedFutureUsageSummary": future,
        "ValidationRequirementSummary": validation
    }
    if child_step:
        result["SubprocessChildStepKey"] = child_step
    if child_title:
        result["SubprocessChildArtifactTitle"] = child_title
    return result


def dep(step_key: str, branch: str = ""):
    return {
        "DependsOnStepKey": step_key,
        "DependsOnBranchOutcomeKey": branch
    }


def art_input(source_step: str, expectation_key: str):
    return {
        "ArtifactExpectationKey": expectation_key,
        "SourceStepKey": source_step
    }


def step(
    order: int,
    key: str,
    title: str,
    subtitle: str,
    notes: str,
    kind: str,
    *,
    depends: str = "",
    branch: str = "",
    deps=None,
    decision_role: str = "",
    target_hours: int = 2,
    x: int = 0,
    y: int = 0,
    branch_x: int = 0,
    branch_y: int = 0,
    roles=None,
    artifacts=None,
    inputs=None,
    outcomes=None,
    checklists=None,
    validations=None,
    prompts=None,
    docs=None,
    allowed=None,
    scope: str = "ExternalProductTargetReadOnly",
    approval: bool = False,
    decision_record: bool = False,
    manual_skip: bool = False,
    safe_refusal: bool = False,
    input_summary: str = "",
    output_summary: str = "",
    evidence_summary: str = "",
    decision_summary: str = "",
    exception_summary: str = "",
    subprocess_key: str = "",
    subprocess_snapshot: str = ""):
    result = {
        "Order": order,
        "Key": key,
        "Title": title,
        "Subtitle": subtitle,
        "Notes": notes,
        "StepKind": kind
    }
    if subprocess_key:
        result["SubprocessProcessKey"] = subprocess_key
    if subprocess_snapshot:
        result["SubprocessDefinitionSnapshotName"] = subprocess_snapshot
    result.update({
        "AllowsManualSkip": manual_skip,
        "AllowsSafeRefusal": safe_refusal,
        "RequiresApproval": approval,
        "RequiresDecisionRecord": decision_record,
        "InputContractSummary": input_summary,
        "OutputContractSummary": output_summary,
        "EvidenceContractSummary": evidence_summary,
        "DecisionRightsSummary": decision_summary,
        "ExceptionPolicySummary": exception_summary,
        "TargetLeadHours": target_hours,
        "DependsOnStepKey": depends,
        "DependsOnBranchOutcomeKey": branch,
        "DecisionRoleKey": decision_role,
        "CanvasX": x,
        "CanvasY": y,
        "BranchCanvasX": branch_x,
        "BranchCanvasY": branch_y,
        "Dependencies": deps if deps is not None else ([] if not depends else [dep(depends, branch)]),
        "RoleAssignments": roles or [],
        "ArtifactExpectations": artifacts or [],
        "ArtifactInputs": inputs or [],
        "BranchOutcomes": outcomes or [],
        "ChecklistRefs": checklists or [],
        "ValidationRefs": validations or [],
        "PromptRefs": prompts or [],
        "DocRefs": docs or [f"processes/{{process-key}}/steps/{key}.md"],
        "AllowedOperations": allowed or [
            "ReadProcessContext",
            "ReadProjectStructure",
            "ReadUpstreamArtifacts",
            "WriteManagedProcessArtifacts"
        ],
        "OperationTargetScope": scope
    })
    return result


def base_process(
    key: str,
    display: str,
    summary: str,
    value: str,
    owner: str,
    roles,
    steps,
    *,
    source_frameworks=None,
    shared_roles=None,
    shared_artifacts=None,
    metrics=None,
    risks=None,
    tailoring=None):
    return {
        "Kind": "process-template-definition",
        "Key": key,
        "DisplayName": display,
        "Summary": summary,
        "ValueStatement": value,
        "CustomerName": "Software delivery process",
        "OwnerName": owner,
        "InterfaceContractSummary": "Parent process supplies project structure context, upstream artifacts, current run identity, and target repository boundary; this subprocess returns durable managed artifacts and project-structure writeback receipts where applicable.",
        "GovernanceNotes": "Every step declares explicit operations and target scope so role permissions remain bounded and product mutation cannot leak into planning, review, validation, screenshot, or writeback work.",
        "ChangeSummary": "Adds .NET-specific delivery subprocess coverage for multi-team software delivery hardening.",
        "GovernancePolicySummary": "No subprocess may silently change product files unless its step explicitly allows product target mutation.",
        "ConstitutionRuleSummary": "Typed process artifacts, subprocess relationships, and project-structure nodes are the source of truth for delivery replay.",
        "OperatingModeSummary": "Governed live execution inside a parent software-delivery run.",
        "SimulationReadinessSummary": "Small enough for deterministic process projection and rich enough for live run validation.",
        "Criticality": "High",
        "AutonomyLevel": "Guarded",
        "OperatingMode": "GovernedLive",
        "SourceFrameworkKeys": source_frameworks or ["nist-ssdf", "owasp-samm", "slsa"],
        "SharedRoleRefs": shared_roles or ["delivery-manager", "solution-architect", "software-engineer", "qa-lead"],
        "SharedArtifactRefs": shared_artifacts or [
            "scope-boundary-packet",
            "project-structure-context-brief",
            "architecture-decision-record",
            "implementation-change-set",
            "test-evidence-pack",
            "regression-evidence-pack",
            "implementation-plan"
        ],
        "SharedChecklistRefs": [],
        "SharedValidationRefs": [],
        "SharedPromptRefs": [],
        "LocalRoleRefs": [],
        "LocalArtifactRefs": [],
        "LocalChecklistRefs": [],
        "LocalValidationRefs": [],
        "LocalPromptRefs": [],
        "Metrics": metrics or [],
        "Risks": risks or [],
        "TailoringRules": tailoring or [],
        "RoleUsages": roles,
        "Steps": steps,
        "DocPath": f"processes/{key}/definition.md"
    }


def write_process_docs(process: dict, flow: str, sequence: str) -> None:
    process_key = process["Key"]
    root = PROCESSES / process_key
    step_lines = []
    for item in sorted(process["Steps"], key=lambda s: s["Order"]):
        dep_text = ", ".join(
            d["DependsOnStepKey"] + (f"/{d['DependsOnBranchOutcomeKey']}" if d.get("DependsOnBranchOutcomeKey") else "")
            for d in item.get("Dependencies", [])
        ) or "None"
        subprocess = f"\n- Subprocess: `{item.get('SubprocessProcessKey')}`" if item.get("SubprocessProcessKey") else ""
        step_lines.append(
            f"### {item['Order'] + 1}. {item['Title']} (`{item['Key']}`)\n"
            f"- Step kind: {item['StepKind']}\n"
            f"- Operation target scope: {item.get('OperationTargetScope', '')}\n"
            f"- Depends on: {dep_text}{subprocess}\n"
            f"- Outputs: {item.get('OutputContractSummary', '')}\n"
            f"- Evidence: {item.get('EvidenceContractSummary', '')}\n"
        )
        save_text(
            root / "steps" / f"{item['Key']}.md",
            f"# {item['Title']}\n\n{item['Notes']}\n\n## Contract\n- Inputs: {item.get('InputContractSummary', '')}\n- Outputs: {item.get('OutputContractSummary', '')}\n- Evidence: {item.get('EvidenceContractSummary', '')}\n- Operation target scope: `{item.get('OperationTargetScope', '')}`\n"
        )
    markdown = (
        f"# {process['DisplayName']}\n\n"
        f"**Key:** `{process_key}`\n"
        f"**Criticality:** {process['Criticality']}\n"
        f"**Autonomy level:** {process['AutonomyLevel']}\n\n"
        f"{process['Summary']}\n\n"
        "## Value\n"
        f"{process['ValueStatement']}\n\n"
        "## Permission model\n"
        f"{process['GovernanceNotes']}\n\n"
        "## Steps\n"
        + "\n".join(step_lines)
    )
    save_text(root / "definition.md", markdown)
    save_text(root / "mermaid" / "flowchart.mmd", flow)
    save_text(root / "mermaid" / "sequence.mmd", sequence)
    save_json(
        root / "projection" / "current-module.compatibility-report.json",
        {
            "ProcessKey": process_key,
            "Status": "Compatible",
            "Notes": [
                "Uses current process definition fields and explicit operation contracts.",
                "Subprocess references are resolved by definition name during import or synchronization."
            ]
        })
    save_text(
        root / "projection" / "current-module.compatibility-report.md",
        "# Compatibility report\n\nThis template uses current process definition fields and explicit operation contracts. Subprocess references are resolved by definition name during import or synchronization."
    )


def create_architecture_process() -> dict:
    process_key = "dotnet-architecture-design-review"
    steps = [
        step(
            0,
            "classify-dotnet-application",
            "Classify .NET application type and project boundary",
            "App archetype and source root",
            "Read the project structure, repository files, requested work, and upstream scope to classify the .NET target as backend-only API/service, Blazor Server/SSR, Blazor WebAssembly, Blazor WASM PWA, worker, console app, class library, or mixed solution. Record product root, test root, runnable projects, UI routes if present, and any contradictions before design starts. Do not create, edit, build, test, or run product files in this step.",
            "Start",
            x=260,
            roles=[
                assignment("architecture-designer", rebind="Rebind only to an architect-capable reviewer with .NET application architecture experience."),
                assignment("delivery-manager", "Reviewer", rebind="Delivery manager reviews classification completeness without taking design authority.")
            ],
            artifacts=[artifact(
                "dotnet-application-classification",
                ".NET application classification and project context",
                template="project-structure-context-brief",
                kind="Brief",
                future="Reusable by design, implementation slice routing, runtime command writeback, screenshot applicability, QA, and release approval.",
                validation="Must identify backend-only, Blazor Server/SSR, Blazor WebAssembly, Blazor WASM PWA, worker, console, library, or mixed app type; product root; test root; runnable projects; UI routes or no-UI rationale; and project-structure process run node context."
            )],
            input_summary="Scope packet, project-structure node, repository context, and requested .NET deliverable.",
            output_summary="Typed .NET application classification with product root, test root, runtime surfaces, and UI/no-UI applicability.",
            evidence_summary="Project context, app type, route/runtime inventory, contradictions, and assumptions.",
            decision_summary="Architecture designer can block when project root, app type, or source-of-truth ownership is contradictory.",
            exception_summary="Block instead of guessing when the target app type or product root cannot be identified from current evidence.",
            docs=["processes/dotnet-architecture-design-review/steps/classify-dotnet-application.md"]),
        step(
            1,
            "draft-architecture-design",
            "Draft .NET architecture design",
            "Boundaries, models, services, and tests",
            "Draft the implementation architecture from the classification and scope. Separate UI component orchestration from application/domain logic, name models and DTOs that cover the user stories, identify services and service functions needed to satisfy acceptance criteria, define persistence/integration boundaries, and outline test seams. Do not implement code or mutate product files.",
            "Work",
            depends="classify-dotnet-application",
            x=1340,
            roles=[
                assignment("architecture-designer", rebind="Architecture design remains with the architect role and must not become implementation work."),
                assignment("implementation-advisor", "Reviewer", rebind="Engineer reviews buildability without taking over architecture authority.")
            ],
            artifacts=[artifact(
                "dotnet-architecture-design",
                ".NET architecture design draft",
                template="architecture-decision-record",
                kind="Decision",
                future="Reusable by architecture review and the implementation slice.",
                validation="Must describe UI/application/domain/infrastructure split, models and DTOs, services and functions needed for user stories, testability seams, app-type-specific runtime path, and rejected alternatives."
            )],
            inputs=[art_input("classify-dotnet-application", "dotnet-application-classification")],
            input_summary="Application classification, scope packet, user stories, and project structure context.",
            output_summary="Reviewable .NET architecture design draft with boundaries, service model, data model, runtime assumptions, and test strategy.",
            evidence_summary="Design options, selected approach, models, services, boundaries, test seams, and rejected alternatives.",
            decision_summary="Architecture designer proposes the path but cannot approve their own design review.",
            exception_summary="Block when the design cannot cover all acceptance criteria without hidden scope expansion.",
            docs=["processes/dotnet-architecture-design-review/steps/draft-architecture-design.md"]),
        step(
            2,
            "review-architecture-design",
            "Review .NET architecture design",
            "Independent design challenge",
            "Review the design before implementation. Ask explicitly: is logic properly split from Blazor/components/controllers; are models and DTOs well defined and complete for the user stories; do services expose the functions needed for acceptance criteria; are functions testable without full UI/runtime; are persistence, integration, security, and deployment boundaries clear; is runtime command and screenshot applicability known; and are risks or trade-offs recorded. Do not implement code or mutate product files.",
            "Review",
            deps=[dep("classify-dotnet-application"), dep("draft-architecture-design")],
            decision_role="architecture-reviewer",
            x=2420,
            roles=[
                assignment("architecture-reviewer", rebind="Architecture review must be performed by an architect-capable reviewer distinct from implementation execution."),
                assignment("qa-reviewer", "Reviewer", rebind="QA reviews testability and evidence expectations without changing product code.")
            ],
            artifacts=[artifact(
                "architecture-review-findings",
                ".NET architecture review findings",
                template="architecture-decision-record",
                kind="Decision",
                future="Reusable by implementation, QA, and release governance.",
                validation="Must answer whether component/controller logic is split from services/domain logic, models cover the user stories, service functions cover acceptance criteria, functions are testable, and boundary risks are explicit."
            )],
            inputs=[
                art_input("classify-dotnet-application", "dotnet-application-classification"),
                art_input("draft-architecture-design", "dotnet-architecture-design")
            ],
            allowed=["ReadProcessContext", "ReadProjectStructure", "ReadUpstreamArtifacts", "WriteManagedProcessArtifacts", "EscalateOrDecide"],
            input_summary="Architecture draft, application classification, scope packet, and acceptance criteria.",
            output_summary="Reviewed architecture decision with required fixes, approval rationale, or block reason.",
            evidence_summary="Checklist answers, design risks, testability assessment, and go/no-go architecture recommendation.",
            decision_summary="Architecture reviewer can require redesign or approve implementation readiness; they cannot mutate product files.",
            exception_summary="Artifact recovery: block when required architecture review findings are missing, invalid, or not materialized before routing back to redesign or approving implementation readiness. Block when design review identifies unowned boundaries, incomplete models, missing service functions, or untestable logic.",
            docs=["processes/dotnet-architecture-design-review/steps/review-architecture-design.md"]),
        step(
            3,
            "architecture-handoff",
            "Hand off reviewed .NET architecture",
            "Implementation-ready design packet",
            "Summarize the accepted architecture, application type, product root, test root, UI/no-UI applicability, runtime command expectations, and implementation slice start criteria. This step creates managed process artifacts only; no product files are changed.",
            "End",
            deps=[dep("classify-dotnet-application"), dep("review-architecture-design")],
            x=3500,
            roles=[
                assignment("delivery-manager", rebind="Delivery manager owns parent-process handoff coordination."),
                assignment("architecture-reviewer", "Reviewer", rebind="Architecture reviewer confirms the handoff matches the review decision.")
            ],
            artifacts=[artifact(
                "architecture-design-review-handoff",
                ".NET architecture design and review handoff",
                template="architecture-decision-record",
                kind="Decision",
                future="Reusable by parent implementation, QA, runtime-command, screenshot, and release steps.",
                validation="Must include approved architecture summary, app type, product root, test root, UI/no-UI applicability, service/model boundary, testability notes, and implementation slice start criteria."
            )],
            inputs=[
                art_input("classify-dotnet-application", "dotnet-application-classification"),
                art_input("review-architecture-design", "architecture-review-findings")
            ],
            input_summary="Classification, design draft, and architecture review findings.",
            output_summary="Parent-ready architecture handoff for .NET implementation slice routing.",
            evidence_summary="Accepted design, unresolved risks, implementation start criteria, runtime command expectations, and UI screenshot applicability.",
            decision_summary="Delivery manager may close only when architecture review has a clear implementation-ready disposition.",
            exception_summary="Do not close when design findings remain unresolved or contradictory.",
            docs=["processes/dotnet-architecture-design-review/steps/architecture-handoff.md"])
    ]
    return base_process(
        process_key,
        ".NET architecture design and review subprocess",
        "Splits .NET architecture design from independent architecture review before implementation starts.",
        "Improves maintainability and smaller-model reliability by forcing explicit app-type classification, design drafting, review challenge, and implementation-ready handoff.",
        "Architecture governance",
        [
            role("delivery-manager", "delivery-manager", "Architecture subprocess manager", "Coordinate handoff between parent delivery and architecture roles.", "Manager-capable operator that can keep architecture work bounded.", assignment="Manager", percent=50, x=40, y=40),
            role("architecture-designer", "solution-architect", ".NET architecture designer", "Draft the .NET architecture without implementing code.", ".NET architect with UI/application/domain/infrastructure boundary experience.", assignment="Architect", percent=80, x=900, y=40, approval=True),
            role("architecture-reviewer", "solution-architect", ".NET architecture reviewer", "Independently challenge the proposed architecture before implementation.", "Architect-capable reviewer who can identify design gaps and testability risks.", assignment="Reviewer", percent=70, x=1760, y=40, approval=True),
            role("implementation-advisor", "software-engineer", "Implementation advisor", "Review buildability without taking over design authority.", ".NET engineer familiar with the target codebase.", assignment="TeamMember", percent=30, x=2620, y=40),
            role("qa-reviewer", "qa-lead", "Architecture testability reviewer", "Challenge whether the design can be validated with focused tests and runtime proof.", "QA reviewer able to identify untestable or under-specified behavior.", assignment="Reviewer", percent=30, x=3480, y=40)
        ],
        steps,
        metrics=[
            "Share of architecture reviews that identify design fixes before implementation starts.",
            "Number of implementation blocks caused by missing app type, product root, or testability notes.",
            "Architecture review findings resolved before implementation slice launch."
        ],
        risks=[
            "Design review becomes a rubber stamp instead of a challenge.",
            "Architecture role starts coding despite read-only operation contracts.",
            "App type is misclassified and downstream runtime or screenshot work is wrong."
        ],
        tailoring=[
            "Classify mixed solutions by each runnable project and call out the selected delivery target.",
            "For backend-only API/service work, record no browser screenshot requirement but still define runtime and test commands.",
            "For Blazor SSR, Blazor WebAssembly, and Blazor WASM PWA, identify route-level screenshot candidates for the screenshot subprocess."
        ])


def create_runtime_process() -> dict:
    process_key = "dotnet-runtime-command-writeback"
    steps = [
        step(
            0,
            "resolve-dotnet-run-commands",
            "Resolve .NET run and test commands",
            "Run app and run tests",
            "Read current project structure, architecture handoff, implementation evidence, and QA proof to determine the product root, app type, runtime project, test project, working directory, environment variables, ports, and stop behavior. Produce commands for Run app and Run tests. If the delivery target is a class library or otherwise non-runnable, the Run app node must still be created with a not-applicable reason rather than being omitted.",
            "Start",
            x=260,
            roles=[assignment("runtime-command-recorder", rebind="Rebind only to a manager-capable operator that can write process-run project nodes.")],
            artifacts=[artifact(
                "dotnet-run-command-manifest",
                ".NET run command manifest",
                template="implementation-plan",
                kind="Evidence",
                future="Reusable by screenshot capture, QA reruns, release handoff, and future process-run replay.",
                validation="Must include process run node id, product root, app type, Run app node title and command or not-applicable reason, Run tests node title and command, working directories, environment/port notes, and stop/cleanup guidance."
            )],
            input_summary="Architecture handoff, implementation evidence, QA evidence, and project-structure run node context.",
            output_summary="Typed manifest for Run command, Run app, and Run tests project-structure nodes.",
            evidence_summary="App type, command strings, working directories, ports, environment notes, and no-run-app rationale when applicable.",
            decision_summary="Recorder may block when commands cannot be resolved from current run evidence.",
            exception_summary="Block on missing product root, missing test command, contradictory runtime target, or absent process run node.",
            docs=["processes/dotnet-runtime-command-writeback/steps/resolve-dotnet-run-commands.md"]),
        step(
            1,
            "write-run-command-nodes",
            "Write Run command project nodes",
            "Process-run runtime receipts",
            "Create or reuse the Run command parent node under the current process run node, then create or update child nodes Run app and Run tests. Each child node must include command, working directory, app type, source evidence, and any required environment or cleanup notes. Use project-structure write tools only through this externally controlled step; do not mutate product files.",
            "Review",
            depends="resolve-dotnet-run-commands",
            decision_role="runtime-command-recorder",
            x=1340,
            roles=[
                assignment("runtime-command-recorder", rebind="Runtime command writeback remains with the delivery manager role, not implementation."),
                assignment("qa-command-reviewer", "Reviewer", rebind="QA reviews command usefulness without writing product files.")
            ],
            artifacts=[artifact(
                "run-command-node-receipts",
                "Run command project-structure node receipts",
                template="implementation-plan",
                kind="Evidence",
                future="Reusable by screenshots, QA reruns, release, and later delivery replay.",
                validation="Must include Run command parent node id, Run app child node id, Run tests child node id, project id, process run node id, commands, working directories, write receipt ids, and blocker status."
            )],
            inputs=[art_input("resolve-dotnet-run-commands", "dotnet-run-command-manifest")],
            allowed=["ReadProcessContext", "ReadProjectStructure", "ReadUpstreamArtifacts", "WriteManagedProcessArtifacts", "ExecuteExternalAction"],
            scope="ExternalActionControlled",
            input_summary=".NET run command manifest and current process run project-structure node.",
            output_summary="Run command parent node with Run app and Run tests child nodes under the process run node.",
            evidence_summary="Project-structure write receipts, node ids, commands, and unresolved blockers.",
            decision_summary="Recorder can complete only after durable project-structure receipts exist.",
            exception_summary="Artifact recovery: block when required Run command node receipts are missing, invalid, or not materialized before branch routing. Block with exact missing tool or failed write receipt if project-structure nodes cannot be created.",
            docs=["processes/dotnet-runtime-command-writeback/steps/write-run-command-nodes.md"]),
        step(
            2,
            "runtime-command-handoff",
            "Hand off runtime command nodes",
            "Run command evidence index",
            "Summarize the Run command parent, Run app node, Run tests node, command values, command applicability, and any unresolved blockers for parent release approval and screenshot capture. This step writes managed artifacts only.",
            "End",
            depends="write-run-command-nodes",
            x=2420,
            roles=[assignment("runtime-command-recorder", rebind="Delivery manager owns the parent-ready command handoff.")],
            artifacts=[artifact(
                "runtime-command-handoff",
                ".NET runtime command handoff",
                template="implementation-plan",
                kind="Evidence",
                future="Reusable by parent release approval and later run analysis.",
                validation="Must reference the Run command parent node, Run app node, Run tests node, command manifest, storage receipts, and unresolved blockers or not-applicable reasons."
            )],
            inputs=[art_input("write-run-command-nodes", "run-command-node-receipts")],
            input_summary="Run command node receipts and command manifest.",
            output_summary="Parent-ready runtime command handoff.",
            evidence_summary="Node ids, commands, command applicability, receipts, and blockers.",
            decision_summary="Delivery manager may close only when Run app and Run tests node status is explicit.",
            exception_summary="Do not close without receipts for Run command, Run app, and Run tests nodes.",
            docs=["processes/dotnet-runtime-command-writeback/steps/runtime-command-handoff.md"])
    ]
    return base_process(
        process_key,
        ".NET runtime command project-structure writeback",
        "Creates durable Run command, Run app, and Run tests project-structure nodes under the current process run node.",
        "Makes local run and validation commands visible for QA, screenshots, release handoff, and future process replay.",
        "Delivery governance",
        [
            role("runtime-command-recorder", "delivery-manager", "Runtime command recorder", "Resolve and write runtime command nodes for the current process run.", "Manager-capable agent with project-structure write access.", assignment="Manager", percent=60, x=40, y=40),
            role("qa-command-reviewer", "qa-lead", "Runtime command reviewer", "Confirm command nodes are useful for validation and screenshot reruns.", "QA reviewer with .NET command and runtime proof experience.", assignment="Reviewer", percent=30, x=900, y=40)
        ],
        steps,
        shared_roles=["delivery-manager", "qa-lead"],
        metrics=[
            "Share of completed runs with Run command, Run app, and Run tests nodes present.",
            "Number of screenshot or QA reruns that can reuse recorded commands without chat recovery."
        ],
        risks=[
            "Commands are stored only in chat and cannot be replayed.",
            "Run app is omitted for non-runnable targets instead of carrying an explicit not-applicable reason.",
            "Project-structure writes are attempted by an implementation role."
        ],
        tailoring=[
            "For backend API/service apps, Run app should start the API/service and name the health or smoke URL when known.",
            "For Blazor SSR, Blazor WebAssembly, and Blazor WASM PWA, Run app must support browser screenshot capture.",
            "For libraries, create Run app with a not-applicable reason and Run tests with the smallest reliable test command."
        ])


def create_screenshot_process() -> dict:
    process_key = "dotnet-ui-screenshot-writeback"
    steps = [
        step(
            0,
            "resolve-ui-screenshot-applicability",
            "Resolve UI screenshot applicability",
            "UI routes or no-UI evidence",
            "Read the architecture handoff, runtime command nodes, QA evidence, and project structure to decide whether the .NET target has a visible UI. Blazor Server/SSR, Blazor WebAssembly, Blazor WASM PWA, MVC/Razor Pages, SPA-hosted .NET, and other browser surfaces require screenshot capture. Backend-only API/service, worker, console, and class library targets require explicit no-UI evidence. Identify the process run node and the Screenshots parent node target under it.",
            "Start",
            x=260,
            roles=[assignment("screenshot-manager", rebind="Rebind only to a manager-capable operator that can assess screenshot scope.")],
            artifacts=[artifact(
                "ui-screenshot-target-manifest",
                ".NET UI screenshot target manifest",
                template="project-structure-context-brief",
                kind="Evidence",
                future="Reusable by screenshot capture, storage, QA, and release approval.",
                validation="Must include app type, UI/no-UI decision, process run node id, Screenshots parent target, base URL or no-UI reason, route list, viewport set, Run app node reference, and Run tests node reference."
            )],
            input_summary="Architecture handoff, runtime command handoff, QA evidence, route nodes, and process run node context.",
            output_summary="Screenshot applicability manifest with UI routes or explicit no-UI evidence.",
            evidence_summary="App type, UI/no-UI decision, route list, viewport set, runtime command references, and Screenshots parent target.",
            decision_summary="Manager may block when UI applicability or route targets are ambiguous.",
            exception_summary="Block on missing process run node, missing Run app command for UI targets, empty route list for UI targets, or contradictory app type evidence.",
            docs=["processes/dotnet-ui-screenshot-writeback/steps/resolve-ui-screenshot-applicability.md"]),
        step(
            1,
            "capture-ui-screenshots",
            "Capture UI screenshots when required",
            "Current-run browser proof",
            "For UI targets, start or reuse the app using the recorded Run app node, navigate to each concrete route, capture screenshots, collect console messages, and write screenshot files as current-run managed artifacts. For no-UI targets, do not launch a browser; write an explicit no-UI screenshot receipt instead. Do not create project-structure nodes or image assets here; the storage step owns Screenshots writeback.",
            "Work",
            depends="resolve-ui-screenshot-applicability",
            x=1340,
            roles=[assignment("screenshot-capture-agent", rebind="Rebind only to an agent with runtime and browser screenshot capability.")],
            artifacts=[
                artifact(
                    "ui-screenshot-files",
                    ".NET UI screenshot files",
                    kind="Evidence",
                    future="Reusable by screenshot storage, QA, release, and future layout analysis.",
                    validation="For UI targets, must include one readable screenshot per required route and viewport. For no-UI targets, must include explicit no-UI receipt and no browser-launch claim."),
                artifact(
                    "ui-browser-evidence",
                    ".NET UI browser evidence",
                    kind="Evidence",
                    future="Reusable by QA and failure triage.",
                    validation="For UI targets, must include actual URLs, viewport, wait condition, console messages, screenshot paths, runtime command reference, and cleanup receipt. For no-UI targets, must include no-UI rationale.")
            ],
            inputs=[art_input("resolve-ui-screenshot-applicability", "ui-screenshot-target-manifest")],
            allowed=["ReadProcessContext", "ReadProjectStructure", "ReadUpstreamArtifacts", "LaunchRuntime", "CaptureRuntimeProof", "WriteManagedProcessArtifacts"],
            input_summary="Screenshot target manifest and Run app command node reference.",
            output_summary="Screenshot files and browser evidence for UI targets, or explicit no-UI receipt for non-UI targets.",
            evidence_summary="Screenshots, route URLs, console state, runtime command references, cleanup receipt, or no-UI evidence.",
            decision_summary="Capture agent can block on failed app launch, fatal browser errors, or missing required routes.",
            exception_summary="Do not substitute stale screenshots, chat-only claims, or screenshots from another process run.",
            docs=["processes/dotnet-ui-screenshot-writeback/steps/capture-ui-screenshots.md"]),
        step(
            2,
            "store-ui-screenshots",
            "Store screenshots under process run node",
            "Screenshots project-structure parent",
            "Create or reuse a Screenshots parent node under the current process run node. For UI targets, inspect each screenshot, reject blank/error/wrong-route images, and create image asset nodes under Screenshots for accepted screenshots. For no-UI targets, record a no-UI screenshot receipt under Screenshots or as managed evidence so the parent release step can see that screenshot capture was intentionally not applicable. Do not mutate product files.",
            "Review",
            deps=[dep("resolve-ui-screenshot-applicability"), dep("capture-ui-screenshots")],
            decision_role="screenshot-review-storage-agent",
            x=2420,
            roles=[
                assignment("screenshot-review-storage-agent", rebind="Rebind only to an agent with image review and project-structure asset write access."),
                assignment("screenshot-manager", "Reviewer", rebind="Manager reviews storage receipts and no-UI disposition.")
            ],
            artifacts=[artifact(
                "screenshots-node-storage-receipts",
                "Screenshots project-structure storage receipts",
                kind="Evidence",
                future="Reusable by parent release approval, project inspection, and later visual review.",
                validation="Must include process run node id, Screenshots parent node id, one image asset node id per accepted screenshot, route and viewport metadata, sourceWorkspacePath, inspection results, rejected screenshot reasons, and no-UI receipt when applicable."
            )],
            inputs=[
                art_input("resolve-ui-screenshot-applicability", "ui-screenshot-target-manifest"),
                art_input("capture-ui-screenshots", "ui-screenshot-files"),
                art_input("capture-ui-screenshots", "ui-browser-evidence")
            ],
            allowed=["ReadProcessContext", "ReadProjectStructure", "ReadUpstreamArtifacts", "CaptureRuntimeProof", "WriteManagedProcessArtifacts", "ExecuteExternalAction"],
            scope="ExternalActionControlled",
            input_summary="Screenshot files, browser evidence, and screenshot target manifest.",
            output_summary="Screenshots parent node under process run node and image asset storage receipts for accepted screenshots.",
            evidence_summary="Screenshots parent node id, image asset ids, inspection results, rejected images, and no-UI receipt when applicable.",
            decision_summary="Storage reviewer can reject unusable screenshots and request recapture instead of storing bad evidence.",
            exception_summary="Artifact recovery: block when required Screenshots storage receipts are missing, invalid, or not materialized before branch routing. Block when Screenshots parent or required image assets cannot be written, or when UI screenshots are blank, wrong-route, or missing.",
            docs=["processes/dotnet-ui-screenshot-writeback/steps/store-ui-screenshots.md"]),
        step(
            3,
            "screenshot-handoff",
            "Hand off screenshot writeback evidence",
            "Parent-ready visual proof",
            "Summarize screenshot applicability, captured routes, Screenshots parent node id, accepted image asset node ids, rejected images, no-UI evidence, and unresolved blockers for parent release approval. This step writes managed process artifacts only.",
            "End",
            depends="store-ui-screenshots",
            x=3500,
            roles=[assignment("screenshot-manager", rebind="Delivery manager owns parent-ready screenshot handoff.")],
            artifacts=[artifact(
                "ui-screenshot-writeback-handoff",
                ".NET UI screenshot writeback handoff",
                kind="Evidence",
                future="Reusable by parent release approval, project structure inspection, and run analysis.",
                validation="Must reference UI/no-UI applicability, process run node id, Screenshots parent node id, accepted image asset node ids, route metadata, storage receipts, rejected screenshots, and unresolved blockers."
            )],
            inputs=[art_input("store-ui-screenshots", "screenshots-node-storage-receipts")],
            input_summary="Screenshot storage receipts and target manifest.",
            output_summary="Parent-ready screenshot writeback handoff.",
            evidence_summary="Applicability, node ids, asset ids, route evidence, no-UI status, and blockers.",
            decision_summary="Manager may close only when UI targets have stored screenshots or no-UI evidence is explicit.",
            exception_summary="Do not close with missing Screenshots storage receipts for UI targets.",
            docs=["processes/dotnet-ui-screenshot-writeback/steps/screenshot-handoff.md"])
    ]
    return base_process(
        process_key,
        ".NET UI screenshot project-structure writeback",
        "Captures UI screenshots when a .NET delivery target has browser-visible UI and stores accepted screenshots under a Screenshots parent node below the process run node.",
        "Gives UI delivery runs durable visual proof while making backend-only/no-UI applicability explicit.",
        "Delivery governance",
        [
            role("screenshot-manager", "delivery-manager", "Screenshot writeback manager", "Resolve screenshot applicability and parent handoff.", "Manager-capable agent with project-structure context.", assignment="Manager", percent=50, x=40, y=40),
            role("screenshot-capture-agent", "qa-lead", "UI screenshot capture agent", "Start or reuse the app, capture browser proof, and write screenshot files.", "QA agent with runtime and browser screenshot capability.", assignment="AiAgent", percent=100, x=900, y=40, fallback=False),
            role("screenshot-review-storage-agent", "qa-lead", "Screenshot review and storage agent", "Inspect screenshots and store accepted image assets under Screenshots.", "QA agent with image inspection and project-structure write access.", assignment="AiAgent", percent=100, x=1760, y=40, fallback=False)
        ],
        steps,
        shared_roles=["delivery-manager", "qa-lead"],
        metrics=[
            "Share of UI deliveries with stored Screenshots node assets.",
            "Number of no-UI targets with explicit no-screenshot evidence.",
            "Screenshot storage failures caught before release approval."
        ],
        risks=[
            "UI screenshots remain only in run artifacts and are not linked under the process run node.",
            "Backend-only work wastes time launching browsers instead of recording no-UI evidence.",
            "Blank or wrong-route screenshots are stored as accepted proof."
        ],
        tailoring=[
            "Blazor Server/SSR, Blazor WebAssembly, and Blazor WASM PWA require route-level screenshot candidates.",
            "Backend-only API/service, worker, console, and library targets record no-UI evidence instead of browser screenshots.",
            "Screenshots are stored under a Screenshots parent node below the current process run node, not under unrelated delivery blocks."
        ])


def update_dotnet_development_slice() -> None:
    path = PROCESSES / "dotnet-development-slice" / "definition.json"
    definition = load_json(path)
    target = next(s for s in definition["Steps"] if s["Key"] == "add-tests-and-proof")
    target["Title"] = "Validate tests and targeted proof"
    target["Subtitle"] = "Read-only validation before handoff"
    target["Notes"] = "Review the child implementation run, test additions, and validation evidence, then run the smallest reliable validation command needed to prove the slice behavior. Do not add, edit, or repair product tests in this step; missing or inadequate tests must route back to implementation repair instead of being silently fixed by QA."
    target["OutputContractSummary"] = "Test and validation evidence review tied to the implemented behavior, with explicit repair direction when evidence is inadequate."
    target["EvidenceContractSummary"] = "Commands, exit codes, warnings, executed tests, browser/runtime proof when UI is touched, and explicit assessment of whether child-run tests satisfy acceptance criteria."
    target["DecisionRightsSummary"] = "Validation owner can require focused implementation rework when proof or tests do not match acceptance criteria, but cannot mutate product files."
    target["ExceptionPolicySummary"] = "Block on missing tests, failing build, validation that is too broad to diagnose failures, or artifact recovery when required validation evidence is missing or invalid before branch routing; route test changes back to implementation."
    target["AllowedOperations"] = [
        "ReadProcessContext",
        "ReadProjectStructure",
        "ReadUpstreamArtifacts",
        "RunValidation",
        "CaptureRuntimeProof",
        "WriteManagedProcessArtifacts"
    ]
    target["OperationTargetScope"] = "ExternalProductTargetReadOnly"
    for expectation in target.get("ArtifactExpectations", []):
        if expectation["Key"] == "slice-test-evidence":
            expectation["ValidationRequirementSummary"] = "Must include command, exit code, proof tied to acceptance criteria, and review of child-run test coverage. If tests are missing or inadequate, the artifact must route repair to implementation instead of recording QA-authored product changes."
    save_json(path, definition)
    save_text(
        PROCESSES / "dotnet-development-slice" / "steps" / "add-tests-and-proof.md",
        "# Validate tests and targeted proof\n\nReview child-run tests and run targeted validation without mutating product files. Missing or inadequate tests must route back to implementation repair; QA does not add or edit product tests in this step.\n"
    )
    save_text(
        PROCESSES / "dotnet-development-slice" / "definition.md",
        "# .NET implementation slice with atomic validation\n\n**Key:** `dotnet-development-slice`\n**Criticality:** High\n**Autonomy level:** Guarded\n\nReusable child process for breaking a large implementation lane into intake, architecture check, optional solution setup subprocess, feature/function implementation subprocess, read-only validation proof, and handoff evidence. Product mutation is confined to nested implementation subprocesses; slice-level QA validates and routes repair.\n\n## Steps\n- Capture implementation slice boundary.\n- Check architecture and source-of-truth impact.\n- Prepare solution skeleton subprocess.\n- Implement bounded code change through feature/function subprocess.\n- Validate tests and targeted proof without product mutation.\n- Hand off implementation slice.\n"
    )
    save_text(
        PROCESSES / "dotnet-development-slice" / "mermaid" / "flowchart.mmd",
        "flowchart LR\n    A[Slice intake] --> B[Architecture check]\n    B --> C[Solution setup subprocess]\n    C --> D[Feature implementation subprocess]\n    D --> E[Read-only validation proof]\n    E --> F[Slice handoff]\n"
    )


def subprocess_parent_step(
    order,
    key,
    title,
    subtitle,
    subprocess_key,
    snapshot,
    depends_step,
    branch_key,
    canvas_x,
    canvas_y,
    role_key,
    artifact_key,
    artifact_title,
    child_step,
    child_title,
    notes,
    deps_extra=None):
    input_key = (
        "regression-evidence-pack"
        if depends_step == "qa-validation"
        else "repaired-regression-evidence-pack"
        if depends_step == "qa-recheck"
        else ""
    )

    return step(
        order,
        key,
        title,
        subtitle,
        notes,
        "Subprocess",
        subprocess_key=subprocess_key,
        subprocess_snapshot=snapshot,
        deps=(deps_extra or []) + [dep(depends_step, branch_key)],
        x=canvas_x,
        y=canvas_y,
        roles=[assignment(role_key, rebind="Rebind only to a role-compatible operator that can observe the subprocess and record parent evidence.")],
        artifacts=[artifact(
            artifact_key,
            artifact_title,
            template="implementation-plan",
            kind="Evidence",
            future="Reusable by release approval and process-run replay.",
            validation=f"Must point to child step {child_step}, include child artifact {child_title}, current process run node context, write receipts, and unresolved blockers.",
            child_step=child_step,
            child_title=child_title
        )],
        inputs=[art_input(depends_step, input_key)] if input_key else [],
        allowed=["ReadProcessContext", "ReadProjectStructure", "ReadUpstreamArtifacts", "WriteManagedProcessArtifacts", "ExecuteExternalAction"],
        scope="ExternalActionControlled",
        input_summary="Accepted QA evidence, architecture handoff, implementation evidence, and process run node context.",
        output_summary=f"Observed {snapshot} child run with parent-ready writeback evidence.",
        evidence_summary="Child run status, managed artifacts, project-structure receipts, node ids, and blockers.",
        decision_summary="Parent role coordinates subprocess completion and cannot mutate product files.",
        exception_summary="Block when the child subprocess blocks, fails, or returns missing required evidence.",
        docs=[f"processes/software-delivery/steps/{key}.md"])


def update_software_delivery() -> None:
    path = PROCESSES / "software-delivery" / "definition.json"
    definition = load_json(path)
    definition["Summary"] = ".NET-focused multi-team delivery template for planned software change with explicit app-type classification, architecture design and review, subprocess-backed implementation, QA, runtime command writeback, UI screenshot writeback, security, release, deployment, and retrospective governance."
    definition["ValueStatement"] = "Delivers .NET application changes through typed, observable subprocesses that keep architecture, implementation, validation, runtime commands, screenshots, release authority, and project-structure evidence explicit."
    definition["InterfaceContractSummary"] = "The process targets .NET software delivery first: backend-only/API/service, Blazor Server/SSR, Blazor WebAssembly, Blazor WASM PWA, worker, console, library, or mixed solution. JavaScript-specific delivery should use a separate process rather than hidden branching here."
    definition["GovernanceNotes"] = "Architecture, implementation, validation, runtime command writeback, screenshot writeback, security, and release gates have explicit operation contracts. Architects and QA reviewers do not mutate product files; implementation and repair remain the only product-mutable lanes."
    definition["ChangeSummary"] = "Hardens multi-team software delivery for .NET by routing architecture design/review, implementation, runtime command writeback, and UI screenshot writeback through subprocesses with explicit permissions."
    definition["GovernancePolicySummary"] = "Release readiness requires reviewed .NET architecture, implementation evidence, QA evidence, security posture, Run command nodes, UI screenshot or no-UI evidence, rollback readiness, and explicit residual-risk ownership."
    for shared_artifact in ["test-evidence-pack", "implementation-plan"]:
        if shared_artifact not in definition.get("SharedArtifactRefs", []):
            definition["SharedArtifactRefs"].append(shared_artifact)
    steps = {s["Key"]: s for s in definition["Steps"]}

    feature = steps["feature-intake"]
    feature["Title"] = "Clarify .NET scope and app type boundary"
    feature["Subtitle"] = ".NET delivery contract and no-go constraints"
    feature["Notes"] = "Capture the requested outcome, user or operational impact, target delivery window, known dependencies, explicit exclusions, and .NET delivery target. Classify or request classification evidence for backend-only/API/service, Blazor Server/SSR, Blazor WebAssembly, Blazor WASM PWA, worker, console, library, or mixed solution. Preserve explicit project-structure requirements as source-of-truth constraints; they must not be downgraded to optional, excluded, or follow-up work unless the project structure or an accepted decision record says so."
    feature["OutputContractSummary"] = "Decision-ready .NET scope packet with acceptance boundary, app-type hypothesis, dependency map, assumptions, exclusions, and validation hooks."
    feature["EvidenceContractSummary"] = "Intake notes, acceptance criteria, .NET app-type hypothesis, product root hints, UI/no-UI hints, run/test command hints, known exclusions, assumptions, and unresolved dependency register."
    for expectation in feature.get("ArtifactExpectations", []):
        if expectation["Key"] == "scope-boundary-packet":
            expectation["ValidationRequirementSummary"] = "Must capture no-go constraints, user or operational impact, acceptance boundary, .NET app-type hypothesis, product root hints, UI/no-UI hints, and run/test command hints. Must preserve explicit project-structure source-of-truth requirements without downgrading them to optional, excluded, non-acceptance, or follow-up work unless the project structure itself says the item is optional or deferred."

    arch = steps["architecture-review"]
    arch.update({
        "Title": "Run .NET architecture design and review subprocess",
        "Subtitle": "Design, challenge, and app-type routing",
        "Notes": "Launch and observe the .NET architecture design and review subprocess. The child process must classify the app type, draft architecture, independently review the design, and return implementation-ready architecture evidence. This parent step coordinates the subprocess and must not perform product mutation or implementation work.",
        "StepKind": "Subprocess",
        "SubprocessProcessKey": "dotnet-architecture-design-review",
        "SubprocessDefinitionSnapshotName": ".NET architecture design and review subprocess",
        "InputContractSummary": "Scope packet, project-structure context, requested .NET deliverable, and acceptance criteria.",
        "OutputContractSummary": "Observed child architecture run with app-type classification, reviewed design decision, implementation-ready handoff, and unresolved architecture risks.",
        "EvidenceContractSummary": "Child run status, .NET app classification, architecture decision, review findings, implementation start criteria, and UI/no-UI applicability.",
        "DecisionRightsSummary": "Delivery manager coordinates the child run; architecture authority remains inside the subprocess review and cannot mutate product files.",
        "ExceptionPolicySummary": "Block when the child architecture subprocess blocks, fails, or returns missing app type, design review, or implementation-ready handoff evidence.",
        "TargetLeadHours": 14,
        "RoleAssignments": [
            assignment("delivery-manager", rebind="Delivery manager launches and observes the architecture subprocess without taking architecture authority."),
            assignment("solution-architect", "Reviewer", rebind="Architecture role reviews subprocess evidence without implementing code.")
        ],
        "ArtifactExpectations": [
            artifact(
                "project-structure-context-brief",
                "Project structure context brief",
                template="project-structure-context-brief",
                kind="Brief",
                days=365,
                future="Reusable for implementation, review, QA, security, runtime command writeback, screenshot writeback, and release decisions.",
                validation="Must map to child app classification evidence and capture process run node, product root, test root, .NET app type, UI/no-UI applicability, runtime surfaces, and downstream artifact expectations.",
                child_step="classify-dotnet-application",
                child_title=".NET application classification and project context"),
            artifact(
                "architecture-decision-record",
                "Architecture decision record",
                template="architecture-decision-record",
                kind="Decision",
                days=730,
                future="Reusable for implementation, review, QA, runtime command writeback, screenshot writeback, and later forensic replay.",
                validation="Must map to the reviewed child architecture handoff and include selected option, rejected options, source-of-truth choice, service/model/component boundaries, testability notes, and migration ownership.",
                child_step="architecture-handoff",
                child_title=".NET architecture design and review handoff")
        ],
        "ArtifactInputs": [art_input("feature-intake", "scope-boundary-packet")],
        "AllowedOperations": ["ReadProcessContext", "ReadProjectStructure", "ReadUpstreamArtifacts", "WriteManagedProcessArtifacts", "ExecuteExternalAction"],
        "OperationTargetScope": "ExternalActionControlled"
    })

    implementation = steps["implementation"]
    implementation.update({
        "Title": "Run .NET implementation slice subprocess",
        "Subtitle": "Observed implementation and validation slice",
        "Notes": "Launch and observe the .NET implementation slice subprocess for the approved scope and architecture. The child implementation slice owns solution setup, feature/function implementation, tests, and targeted proof. This parent step records child-run evidence and does not mutate product files directly.",
        "StepKind": "Subprocess",
        "SubprocessProcessKey": "dotnet-development-slice",
        "SubprocessDefinitionSnapshotName": ".NET implementation slice with atomic validation",
        "InputContractSummary": "Approved .NET architecture path, app classification, scope packet, unresolved technical questions, and implementation-slice start criteria.",
        "OutputContractSummary": "Observed child implementation slice with reviewable change set, test evidence, blockers, rollout inputs, and parent-ready handoff.",
        "EvidenceContractSummary": "Child run status, change-set projection, validation outputs, output-placement notes, migration steps when applicable, touched-surface inventory, and blockers.",
        "DecisionRightsSummary": "Parent delivery manager owns subprocess sequencing and escalation; implementation changes remain inside child implementation roles.",
        "ExceptionPolicySummary": "Block when the child implementation slice blocks, fails, omits required project-structure requirements, or returns missing change-set, test, or handoff evidence.",
        "TargetLeadHours": 36,
        "RoleAssignments": [
            assignment("delivery-manager", rebind="Delivery manager launches and observes child implementation without editing product code."),
            assignment("lead-engineer", "Reviewer", rebind="Engineering role reviews child implementation evidence and owns follow-up repair when assigned.")
        ],
        "ArtifactExpectations": [
            artifact(
                "implementation-change-set",
                "Implementation change set",
                template="implementation-change-set",
                kind="Deliverable",
                days=365,
                future="Reusable for peer review, QA, runtime command writeback, screenshot writeback, release approval, and later defect forensics.",
                validation="Must point to the observed .NET implementation slice child run and include changed files, behavioral intent, validation proof tied to acceptance criteria, and confirmation that explicit project-structure requirements were not dropped or deferred.",
                child_step="implement-code-change",
                child_title="Slice implementation change set"),
            artifact(
                "migration-rollout-preparation-checklist",
                "Migration and rollout preparation checklist",
                template="rollback-plan",
                kind="Checklist",
                days=365,
                future="Reusable for release rehearsal and rollback planning.",
                validation="Must name data changes or none, operational preconditions, publish/output-placement steps, rollback/removal steps, and child-run evidence used to derive them.",
                child_step="slice-handoff",
                child_title="Implementation slice handoff packet")
        ],
        "AllowedOperations": ["ReadProcessContext", "ReadProjectStructure", "ReadUpstreamArtifacts", "WriteManagedProcessArtifacts", "ExecuteExternalAction"],
        "OperationTargetScope": "ExternalActionControlled"
    })

    record_runtime = subprocess_parent_step(
        8,
        "record-runtime-commands",
        "Record .NET run commands under process run node",
        "Run command, Run app, Run tests",
        "dotnet-runtime-command-writeback",
        ".NET runtime command project-structure writeback",
        "qa-validation",
        "quality-accepted",
        6740,
        0,
        "delivery-manager",
        "runtime-command-writeback",
        ".NET Run command writeback",
        "runtime-command-handoff",
        ".NET runtime command handoff",
        "Launch and observe the .NET runtime command writeback subprocess after first-pass QA acceptance. It must create or reuse Run command under the current process run node and child nodes Run app and Run tests. This step coordinates subprocess evidence only and does not mutate product files.",
        deps_extra=[dep("implementation"), dep("architecture-review")])
    screenshots = subprocess_parent_step(
        9,
        "capture-ui-screenshots",
        "Capture and store .NET UI screenshots",
        "Screenshots under process run node",
        "dotnet-ui-screenshot-writeback",
        ".NET UI screenshot project-structure writeback",
        "record-runtime-commands",
        "",
        7820,
        0,
        "qa-lead",
        "ui-screenshot-writeback",
        ".NET UI screenshot writeback",
        "screenshot-handoff",
        ".NET UI screenshot writeback handoff",
        "Launch and observe the .NET UI screenshot writeback subprocess after runtime command nodes exist. UI targets must capture screenshots and store accepted image assets under a Screenshots parent node below the current process run node. Backend-only or no-UI targets must produce explicit no-UI evidence.",
        deps_extra=[dep("qa-validation", "quality-accepted")])
    record_runtime_repair = subprocess_parent_step(
        14,
        "record-runtime-commands-after-repair",
        "Record repaired .NET run commands under process run node",
        "Run command, Run app, Run tests",
        "dotnet-runtime-command-writeback",
        ".NET runtime command project-structure writeback",
        "qa-recheck",
        "quality-accepted",
        8900,
        2080,
        "delivery-manager",
        "runtime-command-writeback-after-repair",
        ".NET Run command writeback after repair",
        "runtime-command-handoff",
        ".NET runtime command handoff",
        "Launch and observe runtime command writeback after repaired QA acceptance. Refresh or confirm Run command, Run app, and Run tests nodes under the current process run node using repaired evidence.",
        deps_extra=[dep("quality-repair"), dep("implementation"), dep("architecture-review")])
    screenshots_repair = subprocess_parent_step(
        15,
        "capture-ui-screenshots-after-repair",
        "Capture and store repaired .NET UI screenshots",
        "Screenshots under process run node",
        "dotnet-ui-screenshot-writeback",
        ".NET UI screenshot project-structure writeback",
        "record-runtime-commands-after-repair",
        "",
        9980,
        2080,
        "qa-lead",
        "ui-screenshot-writeback-after-repair",
        ".NET UI screenshot writeback after repair",
        "screenshot-handoff",
        ".NET UI screenshot writeback handoff",
        "Launch and observe UI screenshot writeback after repaired runtime command nodes exist. UI targets must store repaired screenshots under Screenshots below the process run node; no-UI targets must carry explicit no-UI evidence.",
        deps_extra=[dep("qa-recheck", "quality-accepted")])
    screenshots["ArtifactExpectations"][0]["ValidationRequirementSummary"] = "Must point to child step screenshot-handoff, include child artifact .NET UI screenshot writeback handoff, current process run node context, Screenshots parent node, accepted image asset or no-UI receipts, write receipts, and unresolved blockers."
    screenshots_repair["ArtifactExpectations"][0]["ValidationRequirementSummary"] = "Must point to child step screenshot-handoff, include child artifact .NET UI screenshot writeback handoff, current process run node context, Screenshots parent node, accepted image asset or no-UI receipts, write receipts, and unresolved blockers."

    release = steps["release-approval"]
    release["CanvasX"] = 8900
    release["Dependencies"] = [
        dep("implementation"),
        dep("architecture-review"),
        dep("qa-validation", "quality-accepted"),
        dep("security-review"),
        dep("record-runtime-commands"),
        dep("capture-ui-screenshots")
    ]
    release["InputContractSummary"] = "QA evidence that names the shipped entrypoint and referenced runtime, security outcome, Run command nodes, UI screenshot or no-UI evidence, rollback/removal plan, support ownership, and declared release boundary."
    release["EvidenceContractSummary"] = "Approval note, residual risk register, rollback/removal ownership record, declared-boundary confirmation, Run command node references, Screenshots parent/image asset or no-UI evidence, and confirmation that QA proof matches the actual shipped entrypoint rather than stale or unreferenced artifacts."
    release["ExceptionPolicySummary"] = "Reject release when security review, Run command nodes, UI screenshot/no-UI evidence, rollback/removal ownership, support readiness, or proof required by the declared release boundary remains incomplete."
    release["ArtifactInputs"] = [
        art_input("implementation", "migration-rollout-preparation-checklist"),
        art_input("qa-validation", "regression-evidence-pack"),
        art_input("security-review", "security-exception-assessment"),
        art_input("record-runtime-commands", "runtime-command-writeback"),
        art_input("capture-ui-screenshots", "ui-screenshot-writeback"),
        art_input("architecture-review", "project-structure-context-brief")
    ]

    release_repair = steps["release-approval-after-repair"]
    release_repair["CanvasX"] = 11060
    release_repair["Dependencies"] = [
        dep("implementation"),
        dep("architecture-review"),
        dep("qa-recheck", "quality-accepted"),
        dep("security-review-after-repair"),
        dep("record-runtime-commands-after-repair"),
        dep("capture-ui-screenshots-after-repair")
    ]
    release_repair["InputContractSummary"] = "Repaired QA evidence that names the shipped entrypoint and referenced runtime, post-repair security outcome, Run command nodes, UI screenshot or no-UI evidence, rollback/removal plan, support ownership, and declared release boundary."
    release_repair["EvidenceContractSummary"] = "Approval note, residual risk register, rollback/removal ownership record, declared-boundary confirmation, repaired Run command node references, Screenshots parent/image asset or no-UI evidence, and confirmation that repaired QA proof matches the actual shipped entrypoint rather than stale or unreferenced artifacts."
    release_repair["ExceptionPolicySummary"] = "Reject release when post-repair security review, Run command nodes, UI screenshot/no-UI evidence, rollback/removal ownership, support readiness, or proof required by the declared release boundary remains incomplete."
    release_repair["ArtifactInputs"] = [
        art_input("implementation", "migration-rollout-preparation-checklist"),
        art_input("qa-recheck", "repaired-regression-evidence-pack"),
        art_input("security-review-after-repair", "security-exception-assessment"),
        art_input("record-runtime-commands-after-repair", "runtime-command-writeback-after-repair"),
        art_input("capture-ui-screenshots-after-repair", "ui-screenshot-writeback-after-repair"),
        art_input("architecture-review", "project-structure-context-brief")
    ]

    steps["security-review"]["Order"] = 7
    steps["security-review"]["CanvasX"] = 6740
    steps["quality-repair"]["Order"] = 5
    steps["qa-recheck"]["Order"] = 6
    steps["security-review-after-repair"]["Order"] = 13
    steps["security-review-after-repair"]["CanvasX"] = 8900
    steps["repair-escalation"]["Order"] = 16
    steps["repair-escalation"]["CanvasX"] = 8900
    steps["repair-escalation"]["CanvasY"] = 4160
    steps["release-approval"]["Order"] = 10
    steps["execute-release-rollout"]["Order"] = 11
    steps["execute-release-rollout"]["CanvasX"] = 9980
    steps["post-release-learning"]["Order"] = 12
    steps["post-release-learning"]["CanvasX"] = 11060
    steps["release-approval-after-repair"]["Order"] = 17
    steps["execute-release-rollout-after-repair"]["Order"] = 18
    steps["execute-release-rollout-after-repair"]["CanvasX"] = 12140
    steps["post-release-learning-after-repair"]["Order"] = 19
    steps["post-release-learning-after-repair"]["CanvasX"] = 13220

    ordered_keys = [
        "feature-intake",
        "architecture-review",
        "implementation",
        "peer-review",
        "qa-validation",
        "quality-repair",
        "qa-recheck",
        "security-review",
        "record-runtime-commands",
        "capture-ui-screenshots",
        "release-approval",
        "execute-release-rollout",
        "post-release-learning",
        "security-review-after-repair",
        "record-runtime-commands-after-repair",
        "capture-ui-screenshots-after-repair",
        "repair-escalation",
        "release-approval-after-repair",
        "execute-release-rollout-after-repair",
        "post-release-learning-after-repair"
    ]
    steps["record-runtime-commands"] = record_runtime
    steps["capture-ui-screenshots"] = screenshots
    steps["record-runtime-commands-after-repair"] = record_runtime_repair
    steps["capture-ui-screenshots-after-repair"] = screenshots_repair
    definition["Steps"] = [steps[key] for key in ordered_keys]
    for index, item in enumerate(definition["Steps"]):
        item["Order"] = index
        item["DocRefs"] = [ref.replace("{process-key}", "software-delivery") for ref in item.get("DocRefs", [])]

    save_json(path, definition)
    write_process_docs(
        definition,
        "flowchart LR\n    A[Clarify .NET scope] --> B[Architecture design/review subprocess]\n    B --> C[.NET implementation slice subprocess]\n    C --> D[Peer review]\n    D --> E{QA validation}\n    E -- quality accepted --> F[Security review]\n    E -- quality accepted --> G[Run command writeback]\n    G --> H[UI screenshot writeback]\n    F --> I[Release approval]\n    H --> I\n    I --> J[Release rollout]\n    J --> K[Post-release learning]\n    E -- repair required --> L[Repair findings]\n    L --> M{QA recheck}\n    M -- quality accepted --> N[Security review after repair]\n    M -- quality accepted --> O[Run command writeback after repair]\n    O --> P[UI screenshot writeback after repair]\n    N --> Q[Release approval after repair]\n    P --> Q\n    Q --> R[Repaired rollout]\n    R --> S[Repaired learning]\n    M -- repair escalation --> T[Repair escalation]\n",
        "sequenceDiagram\n    participant PO as Product owner\n    participant DM as Delivery manager\n    participant ARCH as Architecture subprocess\n    participant DEV as .NET implementation slice\n    participant QA as QA lead\n    participant SEC as Security reviewer\n    participant PS as Project structure\n    PO->>DM: .NET scope and app type boundary\n    DM->>ARCH: Start architecture design/review subprocess\n    ARCH-->>DM: App classification and reviewed architecture handoff\n    DM->>DEV: Start .NET implementation slice subprocess\n    DEV-->>DM: Change set, tests, and validation handoff\n    QA->>DM: QA accepted or repair required\n    alt quality accepted\n        SEC->>DM: Security review\n        DM->>PS: Write Run command, Run app, Run tests\n        QA->>PS: Write Screenshots assets or no-UI receipt\n        DM->>DM: Release approval\n    else repair required\n        DM->>DEV: Repair findings\n        QA->>DM: Recheck accepted or escalation\n    end\n"
    )


def update_manifest() -> None:
    manifest_path = ROOT / "Templates" / "Processes" / "manifest.json"
    manifest = load_json(manifest_path)
    new_entries = [
        {"Key": "dotnet-architecture-design-review", "RelativePath": "processes/dotnet-architecture-design-review"},
        {"Key": "dotnet-runtime-command-writeback", "RelativePath": "processes/dotnet-runtime-command-writeback"},
        {"Key": "dotnet-ui-screenshot-writeback", "RelativePath": "processes/dotnet-ui-screenshot-writeback"}
    ]
    new_keys = {item["Key"] for item in new_entries}
    entries = [entry for entry in manifest["Processes"] if entry["Key"] not in new_keys]
    insert_index = next((i for i, entry in enumerate(entries) if entry["Key"] == "dotnet-development-slice"), 2) + 1
    manifest["Processes"] = entries[:insert_index] + new_entries + entries[insert_index:]
    save_json(manifest_path, manifest)


def create_new_process_templates() -> None:
    for process in [create_architecture_process(), create_runtime_process(), create_screenshot_process()]:
        process_key = process["Key"]
        for item in process["Steps"]:
            item["DocRefs"] = [ref.replace("{process-key}", process_key) for ref in item.get("DocRefs", [])]
        save_json(PROCESSES / process_key / "definition.json", process)
        if process_key == "dotnet-architecture-design-review":
            flow = "flowchart LR\n    A[Classify .NET app type] --> B[Draft architecture design]\n    B --> C[Review architecture design]\n    C --> D[Architecture handoff]\n"
            sequence = "sequenceDiagram\n    participant Parent\n    participant Designer\n    participant Reviewer\n    Parent->>Designer: Scope and project structure\n    Designer-->>Parent: App classification\n    Designer->>Reviewer: Architecture design draft\n    Reviewer-->>Parent: Review findings\n    Reviewer-->>Parent: Architecture handoff\n"
        elif process_key == "dotnet-runtime-command-writeback":
            flow = "flowchart LR\n    A[Resolve run commands] --> B[Write Run command nodes]\n    B --> C[Runtime command handoff]\n"
            sequence = "sequenceDiagram\n    participant Parent\n    participant Recorder\n    participant ProjectStructure\n    Parent->>Recorder: Accepted QA and architecture evidence\n    Recorder->>ProjectStructure: Create Run command, Run app, Run tests\n    ProjectStructure-->>Recorder: Node receipts\n    Recorder-->>Parent: Runtime command handoff\n"
        else:
            flow = "flowchart LR\n    A[Resolve UI screenshot applicability] --> B[Capture UI screenshots or no-UI evidence]\n    B --> C[Store under Screenshots]\n    C --> D[Screenshot handoff]\n"
            sequence = "sequenceDiagram\n    participant Parent\n    participant Manager\n    participant Capture\n    participant Storage\n    participant ProjectStructure\n    Parent->>Manager: Runtime commands and QA evidence\n    Manager->>Capture: UI routes or no-UI decision\n    Capture-->>Storage: Screenshot files and browser evidence\n    Storage->>ProjectStructure: Create Screenshots parent and image assets\n    Storage-->>Parent: Screenshot writeback handoff\n"
        write_process_docs(process, flow, sequence)


def main() -> None:
    create_new_process_templates()
    update_dotnet_development_slice()
    update_software_delivery()
    update_manifest()


if __name__ == "__main__":
    main()
