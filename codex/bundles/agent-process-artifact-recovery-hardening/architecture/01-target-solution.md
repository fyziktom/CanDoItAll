# Target Solution

## Diagnosis Model

Artifact gaps must be classified by owner:

- `CurrentStepOutputMissing`: the current step was responsible for producing the required artifact.
- `UpstreamInputMissing`: the current step required an artifact from a previous step and that source step did not produce it.
- `ProjectionFailed`: an artifact existed but could not be mapped to the expected process artifact.
- `ValidationIncomplete`: required validation tools or proof tools did not run successfully.
- `AgentNoProgress`: the agent repeated identical tool calls or reran validation without a cause-directed change.

## Prompt Contract

For implementation steps, the prompt must make the final order explicit:

1. Inspect or scaffold the actual target.
2. Mutate source/test/project files.
3. Run required build/test validation.
4. Write every required artifact.
5. For DB-free work, write a migration/rollout checklist stating no DB/data migration is needed and naming rollout/rollback proof.
6. Return final response with exact required headings.

## Recovery Contract

- Retry the current step when missing artifacts or validation gaps are owned by the current step.
- Reopen or block upstream when a required input artifact is missing from a source step.
- Do not allow a downstream agent to satisfy an upstream artifact by writing an unrelated substitute.
- Surface the classification in run health and recovery directives.

## Mock Coverage Contract

Mock agents must be able to produce:

- Happy-path complete artifacts.
- Missing current-step artifact.
- Missing upstream artifact input.
- Repeated no-progress write failure.
- Build/test validation omission.
- Recovery success after an explicit directive.

## Proof Strategy

Start with isolated service/runtime tests. Only after those pass should browser or multi-agent process proof run.
