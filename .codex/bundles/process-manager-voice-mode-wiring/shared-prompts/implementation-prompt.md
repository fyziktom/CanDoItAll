# Implementation Prompt

Implement only the active subbundle.

Prerequisites:
- Read the subbundle README and this bundle's `analysis/01-current-state.md`, `requirements/01-normalized-requirements.md`, and `inventories/01-voice-surface-inventory.md`.
- Confirm the worktree diff before editing and do not revert unrelated user changes.

Hard constraints:
- Keep `ChatWorkspacePanel` as the shared voice control surface.
- Keep provider-specific audio behavior behind `IAgentVoiceService`, `IAgentVoiceDriverFactory`, `ProviderRuntimeVoiceDriver`, and typed provider drivers.
- Do not introduce silent fallback behavior for missing voice settings or unsupported provider capabilities.
- Make the smallest correct change for the subbundle.

Required proof:
- Capture command transcripts under the subbundle proof folder.
- For critical subbundles, create `proof/SBxx/manifest.md` and `proof/SBxx/semantic-invariants.md`.
- Update `reviews/01-execution-report.md` gate rows and raw-note closure as each phase closes.

Stop conditions:
- Stop and reopen SB01 if the disabled-state cause differs from the current inventory.
- Stop and document a blocker if Playwright cannot open the app route or microphone permission prevents real recording proof.
