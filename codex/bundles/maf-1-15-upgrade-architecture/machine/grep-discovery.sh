#!/usr/bin/env bash
set -euo pipefail

ROOT="${1:-$(pwd)}"
ROOT="$(cd "$ROOT" && pwd)"
OUT="$ROOT/.artifacts/maf-1.15-discovery"
mkdir -p "$OUT"

EXCLUDES=(
  --exclude-dir=.git
  --exclude-dir=bin
  --exclude-dir=obj
  --exclude-dir=.artifacts
  --exclude-dir=ExternalPackages
  --exclude-dir=node_modules
)

run_group() {
  local name="$1"
  shift
  local target="$OUT/$name.txt"
  : > "$target"

  for pattern in "$@"; do
    printf '===== PATTERN: %s =====\n' "$pattern" >> "$target"
    if ! grep -RInF "${EXCLUDES[@]}" \
      --include='*.cs' --include='*.csproj' --include='*.props' --include='*.targets' \
      --include='*.json' --include='*.yaml' --include='*.yml' --include='*.md' \
      "$pattern" "$ROOT" >> "$target" 2>/dev/null; then
      printf '<no matches>\n' >> "$target"
    fi
    printf '\n' >> "$target"
  done
}

run_group packages \
  "Microsoft.Agents.AI" "Microsoft.Extensions.AI" "PackageReference" "PackageVersion"

run_group agent-pipeline \
  "ChatClientAgentOptions" "UseProvidedChatClientAsIs" \
  "DisableApprovalResponseBinding" "DisableApprovalNotRequiredFunctionBypassing" \
  "EnableNonApprovalRequiredFunctionBypassing" "FunctionInvokingChatClient" \
  "UseApprovalResponseBinding" "UseApprovalNotRequiredFunctionBypassing" \
  "BuildAIAgent" "AsAIAgent"

run_group approvals \
  "ToolApprovalRequestContent" "ToolApprovalResponseContent" \
  "ApprovalRequiredAIFunction" "ToolApprovalAgent" "ToolAutoApprovalRuleContext" \
  "PendingToolApprovalRecord" "RespondToPendingApprovals" "CreateResponse(" \
  "ApprovalId" "CallId"

run_group sessions \
  "SerializeSessionAsync" "DeserializeSessionAsync" "AgentSessionStateBag" \
  "ChatClientAgentSession" "SerializedSessionStateJson" "conversationId" \
  "RequestScopedSessionContentScrubber" "ShouldReplayTranscriptAfterApproval" \
  "RequirePerServiceCallChatHistoryPersistence"

run_group workflows \
  "AgentWorkflowBuilder" "WorkflowHostAgent" "WorkflowSession" \
  "WorkflowOutputEvent" "AsAIAgent" "ToAgentResponse" "MessageMerger" \
  "HandoffDepthGuard" "EmitAgentResponseEvents" \
  "EmitAgentResponseUpdateEvents" "includeWorkflowOutputsInResponse"

run_group checkpointing \
  "CheckpointManager" "ICheckpointStore" \
  "WorkflowBackedAgentExecutionCheckpointBridge" "ExternalRequest" \
  "RequestPort" "Resume"

run_group file-harness \
  "HarnessAgent" "HarnessAgentOptions" "Microsoft.Agents.AI.Harness" \
  "FileAccessStore" "FileAccessProvider" "FileAccessProviderOptions" \
  "DisableFileAccess" "FileMemoryProvider" "FileSystemAgentFileStore" \
  "LocalCodeAct"

run_group custom-filetools \
  "IWorkspaceFileService" "WorkspaceFileService" \
  "IWorkspacePathResolutionService" "IWorkspaceCommandExecutionService" \
  "IWorkspaceArtifactToolService" "CanDoItAll.FileTools" \
  "WorkspaceScopeDescriptor" "ExternalTarget"

run_group hosting-protocols \
  "Microsoft.Agents.AI.A2A" "Microsoft.Agents.AI.Hosting.A2A" \
  "AddAgentFrameworkA2AHosting" "AGUI" "AddAGUI" "MapAGUI" \
  "OpenAIResponses" "HostedAgentState" "HostedWorkflowState" \
  "HostedWorkflowRunResult" "DeleteSessionAsync" "autoSend"

run_group optional \
  "CompactionProvider" "CompactionStrategy" "FileMemoryProvider" \
  "MessageInjectingChatClient" "EnableMessageInjection" \
  "CosmosChatHistoryProvider" "TodoProvider" "AgentModeProvider"

run_group merge-snapshot \
  "MafAgentResponseSnapshotter" "AgentResponseUpdate" "CreatedAt" \
  "MessageId" "ResponseId" "OrderBy(" "GroupBy(" "Distinct("

{
  printf 'repositoryRoot=%s\n' "$ROOT"
  printf 'capturedAtUtc=%s\n' "$(date -u +%Y-%m-%dT%H:%M:%SZ)"
  printf 'gitHead=%s\n' "$(git -C "$ROOT" rev-parse HEAD)"
  printf 'gitBranch=%s\n' "$(git -C "$ROOT" branch --show-current)"
  printf 'gitStatusBegin\n'
  git -C "$ROOT" status --short
  printf 'gitStatusEnd\n'
} > "$OUT/metadata.txt"

dotnet --info > "$OUT/dotnet-info.txt"

grep -RInF "${EXCLUDES[@]}" --include='*.csproj' \
  "Microsoft.Agents.AI" "$ROOT" > "$OUT/direct-maf-package-references.txt" || true

printf 'MAF 1.15 discovery output: %s\n' "$OUT"
