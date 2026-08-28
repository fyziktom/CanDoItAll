# Model thinking settings handoff

Current image: candoitall-shared-providers-ui:model-thinking-20260828-2 on all apps.
Existing data, secrets, agent assignments, histories and volumes are retained.

1. Source: http://localhost:5210/agents?tab=providers. Select a provider, open Thinking,
   search a model and choose Edit. Automatic uses discovered or built-in capabilities.
   Uncheck Automatic to set support, allowed efforts and an optional per-model default.
   Apply changes to the draft, then Save provider. Re-enable Automatic to reset.
2. Clients: http://localhost:5212/agents?tab=agents and
   http://localhost:5214/agents?tab=agents. Open an agent's Runtime settings, choose
   the shared provider/model and use Refresh shared provider after source changes.
   Provider Thinking tables are read-only mirrors. Refresh preserves unsaved agent choices.
3. Choose thinking per agent. Provider default defers to the current source model
   default; a valid explicit effort overrides it. Unsupported models disable override.

For the verified OpenAI reasoning-with-tools path on 5212, use Thinking Proof OpenAI
Responses. The original UI Shared OpenAI Chat uses Chat Completions and retains its
upstream compatibility limitations. Manual capability settings cannot remove those.

The internal shared connection URL remains http://candoitall-spui-shared:8080/.
Existing source JWT secrets are already configured; no new token was created or
printed. Renew scoped credentials through Settings/API when they expire.

No manual capability override was left behind by testing. Source Proof Responses
defaults are Medium; UI Shared Ollama is automatic Low/Medium/High. The dedicated
source-default agent is back on its original Mini/provider-default setting. All
user-owned agent dialogs and the new Simple Chat draft were closed without saving.

Proof: ../../proof/SB09/manifest.md and ../../proof/SB10/manifest.md. Broad failures
are separate from the 229 passing focused cases and eight successful live requests.
