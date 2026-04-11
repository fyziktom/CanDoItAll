import json
import sys
from pathlib import Path

DEFAULT_ROUTE_KEY = "__default__"
ERROR_ROUTE_KEY = "__error__"

def load_json(path: Path):
    return json.loads(path.read_text(encoding="utf-8"))

def main():
    root = Path(sys.argv[1]) if len(sys.argv) > 1 else Path(__file__).resolve().parents[1] / "repo-overlay" / "output" / "process-template-pack"
    manifest = load_json(root / "manifest.json")

    errors = []
    warnings = []

    shared = {
        "roles": {p.stem for p in (root / "shared" / "roles").glob("*.json")},
        "artifacts": {p.stem for p in (root / "shared" / "artifacts").glob("*.json")},
        "checklists": {p.stem for p in (root / "shared" / "checklists").glob("*.json")},
        "validations": {p.stem for p in (root / "shared" / "validations").glob("*.json")},
        "prompts": {p.stem for p in (root / "shared" / "prompts").glob("*.json")},
    }

    process_count = 0
    step_count = 0
    dependency_count = 0
    artifact_input_count = 0

    expectations = {
        "software-delivery": {"step": "release-approval", "dependency_count": 3, "artifact_input_count": 3, "title": "Approve release readiness"},
        "hotfix-rollout": {"step": "approve-emergency-release", "dependency_count": 2, "artifact_input_count": 2, "title": "Approve emergency release window"},
        "branching-code-review": {"step": "route-review-disposition", "decision_role": "review-lead", "required_branch_keys": {"repairs-required","qa-validation","security-review","architecture-review","ready-for-merge",DEFAULT_ROUTE_KEY,ERROR_ROUTE_KEY}},
    }

    for entry in manifest["Processes"]:
        process_count += 1
        proc_dir = root / entry["RelativePath"]
        definition = load_json(proc_dir / "definition.json")
        local = {
            "roles": {p.stem for p in (proc_dir / "roles").glob("*.json")},
            "artifacts": {p.stem for p in (proc_dir / "artifacts").glob("*.json")},
            "checklists": {p.stem for p in (proc_dir / "checklists").glob("*.json")},
            "validations": {p.stem for p in (proc_dir / "validations").glob("*.json")},
            "prompts": {p.stem for p in (proc_dir / "prompts").glob("*.json")},
        }
        available = {k: shared[k] | local[k] for k in shared}
        step_keys = {step["Key"] for step in definition["Steps"]}
        role_keys = {role["Key"] for role in definition.get("RoleUsages", [])}
        artifact_expectations_by_step = {step["Key"]: {item["Key"] for item in step.get("ArtifactExpectations", [])} for step in definition["Steps"]}
        branch_keys_by_step = {step["Key"]: {item["Key"] for item in step.get("BranchOutcomes", [])} for step in definition["Steps"]}

        for ref in definition.get("SharedRoleRefs", []):
            if ref not in shared["roles"]:
                errors.append(f"{definition['Key']}: missing shared role ref {ref}")
        for ref in definition.get("SharedArtifactRefs", []):
            if ref not in shared["artifacts"]:
                errors.append(f"{definition['Key']}: missing shared artifact ref {ref}")
        for ref in definition.get("SharedChecklistRefs", []):
            if ref not in shared["checklists"]:
                errors.append(f"{definition['Key']}: missing shared checklist ref {ref}")
        for ref in definition.get("SharedValidationRefs", []):
            if ref not in shared["validations"]:
                errors.append(f"{definition['Key']}: missing shared validation ref {ref}")
        for ref in definition.get("SharedPromptRefs", []):
            if ref not in shared["prompts"]:
                errors.append(f"{definition['Key']}: missing shared prompt ref {ref}")
        for ref_name, bucket in [("LocalRoleRefs","roles"),("LocalArtifactRefs","artifacts"),("LocalChecklistRefs","checklists"),("LocalValidationRefs","validations"),("LocalPromptRefs","prompts")]:
            for ref in definition.get(ref_name, []):
                if ref not in local[bucket]:
                    errors.append(f"{definition['Key']}: missing local ref {ref_name}/{ref}")

        for step in definition["Steps"]:
            step_count += 1
            deps = step.get("Dependencies", [])
            if not deps and (step.get("DependsOnStepKey") or step.get("DependsOnBranchOutcomeKey")):
                deps = [{"DependsOnStepKey": step.get("DependsOnStepKey",""), "DependsOnBranchOutcomeKey": step.get("DependsOnBranchOutcomeKey","")}]
            dependency_count += len(deps)
            artifact_input_count += len(step.get("ArtifactInputs", []))
            for dep in deps:
                dep_step = dep.get("DependsOnStepKey","")
                dep_branch = dep.get("DependsOnBranchOutcomeKey","")
                if dep_step and dep_step not in step_keys:
                    errors.append(f"{definition['Key']}/{step['Key']}: invalid dependency step {dep_step}")
                if dep_branch and dep_branch not in branch_keys_by_step.get(dep_step, set()):
                    errors.append(f"{definition['Key']}/{step['Key']}: invalid dependency branch {dep_step}/{dep_branch}")
            if step.get("DecisionRoleKey") and step["DecisionRoleKey"] not in role_keys:
                errors.append(f"{definition['Key']}/{step['Key']}: invalid decision role {step['DecisionRoleKey']}")
            for assignment in step.get("RoleAssignments", []):
                if assignment["RoleKey"] not in role_keys:
                    errors.append(f"{definition['Key']}/{step['Key']}: invalid role assignment {assignment['RoleKey']}")
            for expectation in step.get("ArtifactExpectations", []):
                if expectation.get("TemplateKey") not in available["artifacts"]:
                    errors.append(f"{definition['Key']}/{step['Key']}: invalid artifact template {expectation.get('TemplateKey')}")
                if not expectation.get("Key"):
                    errors.append(f"{definition['Key']}/{step['Key']}: artifact expectation missing key")
            for artifact_input in step.get("ArtifactInputs", []):
                source_step = artifact_input.get("SourceStepKey","")
                artifact_key = artifact_input.get("ArtifactExpectationKey","")
                if source_step and source_step not in step_keys:
                    errors.append(f"{definition['Key']}/{step['Key']}: artifact input references missing step {source_step}")
                if source_step and artifact_key and artifact_key not in artifact_expectations_by_step.get(source_step, set()):
                    errors.append(f"{definition['Key']}/{step['Key']}: artifact input references missing expectation {source_step}/{artifact_key}")
            for checklist_ref in step.get("ChecklistRefs", []):
                if checklist_ref not in available["checklists"]:
                    errors.append(f"{definition['Key']}/{step['Key']}: invalid checklist ref {checklist_ref}")
            for validation_ref in step.get("ValidationRefs", []):
                if validation_ref not in available["validations"]:
                    errors.append(f"{definition['Key']}/{step['Key']}: invalid validation ref {validation_ref}")
            for prompt_ref in step.get("PromptRefs", []):
                if prompt_ref not in available["prompts"]:
                    errors.append(f"{definition['Key']}/{step['Key']}: invalid prompt ref {prompt_ref}")

        if definition["Key"] in expectations:
            exp = expectations[definition["Key"]]
            target = next((step for step in definition["Steps"] if step["Key"] == exp["step"]), None)
            if target is None:
                errors.append(f"{definition['Key']}: missing expected step {exp['step']}")
            else:
                if "title" in exp and target["Title"] != exp["title"]:
                    errors.append(f"{definition['Key']}/{exp['step']}: expected title '{exp['title']}' but found '{target['Title']}'")
                if "dependency_count" in exp:
                    deps = target.get("Dependencies", [])
                    if not deps and (target.get("DependsOnStepKey") or target.get("DependsOnBranchOutcomeKey")):
                        deps = [{"DependsOnStepKey": target.get("DependsOnStepKey",""), "DependsOnBranchOutcomeKey": target.get("DependsOnBranchOutcomeKey","")}]
                    if len(deps) != exp["dependency_count"]:
                        errors.append(f"{definition['Key']}/{exp['step']}: expected {exp['dependency_count']} dependencies, found {len(deps)}")
                if "artifact_input_count" in exp and len(target.get("ArtifactInputs", [])) != exp["artifact_input_count"]:
                    errors.append(f"{definition['Key']}/{exp['step']}: expected {exp['artifact_input_count']} artifact inputs, found {len(target.get('ArtifactInputs', []))}")
                if "decision_role" in exp and target.get("DecisionRoleKey") != exp["decision_role"]:
                    errors.append(f"{definition['Key']}/{exp['step']}: expected decision role {exp['decision_role']}, found {target.get('DecisionRoleKey')}")
                if "required_branch_keys" in exp:
                    actual = {item["Key"] for item in target.get("BranchOutcomes", [])}
                    missing = exp["required_branch_keys"] - actual
                    if missing:
                        errors.append(f"{definition['Key']}/{exp['step']}: missing branch outcomes {sorted(missing)}")

            if definition["Key"] == "branching-code-review":
                target = next((step for step in definition["Steps"] if step["Key"] == "validate-qa-lane"), None)
                if target is None or len(target.get("ArtifactInputs", [])) != 1:
                    errors.append("branching-code-review/validate-qa-lane: expected exactly one artifact input")

    baseline = load_json(root / manifest["SeedCatalog"]["BaselineScenariosPath"])
    expected_baselines = {
        "software-delivery",
        "branching-code-review",
        "hotfix-rollout",
        "customer-onboarding",
        "incident-response",
    }
    actual_baselines = {item["ProcessTemplateKey"] for item in baseline}
    if actual_baselines != expected_baselines:
        errors.append(f"baseline scenarios mismatch: expected {sorted(expected_baselines)}, found {sorted(actual_baselines)}")
    for scenario in baseline:
        proc_entry = next((item for item in manifest["Processes"] if item["Key"] == scenario["ProcessTemplateKey"]), None)
        if proc_entry is None:
            errors.append(f"Scenario {scenario['Key']}: missing process {scenario['ProcessTemplateKey']}")

    result = {
        "ProcessCount": process_count,
        "StepCount": step_count,
        "BaselineScenarioCount": len(baseline),
        "DependencyCount": dependency_count,
        "ArtifactInputCount": artifact_input_count,
        "WarningCount": len(warnings),
        "Warnings": warnings,
        "ErrorCount": len(errors),
        "Errors": errors
    }
    print(json.dumps(result, indent=2))
    return 1 if errors else 0

if __name__ == "__main__":
    raise SystemExit(main())
