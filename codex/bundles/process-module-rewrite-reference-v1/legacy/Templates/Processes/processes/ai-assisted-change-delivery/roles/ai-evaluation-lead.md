# AI evaluation lead

**Key:** `ai-evaluation-lead`  
**Scope:** local  
**Process:** ai-assisted-change-delivery  
**Preferred executor:** person  
**Preferred project role:** Reviewer  
**Seniority:** Senior ML evaluation or quality leadership  
**Minimum years in primary discipline:** 6  
**Minimum years in software delivery:** 8

## Summary
Owner for AI change evaluation design, benchmark selection, and evidence integrity.

## Purpose
Ensure AI-assisted work is judged using explicit evaluation criteria rather than optimism about model capability.

## Staffing intent
A technically rigorous evaluator for AI-assisted delivery or AI-enabled product changes.

## Snapshot summary
Owner for AI change evaluation design, benchmark selection, and evidence integrity.

## Domain tags
ai-evaluation, benchmarks, evidence-design

## Knowledge requirements
- Ability to define evaluation tasks, benchmark coverage, and pass/fail thresholds for AI-assisted change work.
- Knowledge of model behavior variability, failure patterns, and measurement caveats.
- Understanding of dataset representativeness, leakage risk, and annotation quality.
- Ability to separate convenience metrics from decision-grade evidence.
- Knowledge of how human review and automated evaluation should complement each other.
- Ability to document refusal or containment conditions for unsafe model output.

## Experience requirements
- Has designed or reviewed evaluation plans for an AI-enabled feature or workflow.
- Has interpreted benchmark or qualitative evaluation results in a release decision context.
- Has caught misleading evaluation setups such as leakage, cherry-picking, or unrepresentative samples.
- Has collaborated with product, engineering, and safety stakeholders on evaluation scope.
- Has updated evaluation logic after observing production drift or blind spots.

## Decision rights
- Approve evaluation scope and adequacy for AI-related change.
- Reject evidence sets that are not decision-grade.
- Escalate when measurement uncertainty is too high for delegated release approval.
- Require explicit human review where automated evaluation cannot justify confidence.

## Owned artifacts
- Evaluation plan
- Benchmark report
- Evaluation exception note

## Collaboration expectations
- Collaborate with AI safety, model risk, product, and engineering roles.
- Make evaluation assumptions visible to decision-makers.
- Protect evidence integrity when delivery pressure encourages shortcutting.
- Feed production observations back into evaluation design.

## Anti-patterns
- Treating anecdotal good examples as proof of readiness.
- Using metrics without explaining the dataset and test shape behind them.
- Approving AI output quality without reviewing harmful failure modes.
- Ignoring model or prompt changes that invalidate prior benchmarks.

## Fitness evidence
- Evaluation plans with explicit scope, thresholds, and limits.
- Benchmark results consumed successfully in governance decisions.
- Evidence of catching misleading or incomplete AI evidence.
- Post-release updates improving evaluation coverage.
