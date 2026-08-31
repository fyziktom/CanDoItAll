param([Parameter(Mandatory)][string]$ProofRoot)
$ErrorActionPreference = 'Stop'
$specs = @(
    @{ Name = 'native'; AgentId = '300a1315-d133-1159-a6b1-b02433ce78c0'; ReadableModel = 'gpt-5.4-mini'; Scope = 'C:\Users\lucys\AppData\Local\CanDoItAll\workspace\runtime-overrides\ff24611dad478ec960349d9ad11d1017\data\scopes\organization\e5df9ad633dbc6974a0678a74976013c' },
    @{ Name = 'client'; AgentId = '952b041a-aba0-385b-8e4e-494c4b21d831'; ReadableModel = 'gpt-5.6-luna'; Scope = 'C:\repositories\CanDoItAll\.artifacts\shared-providers-e2e\client-a\data\workspace\data\scopes\organization\3dfd771ef0fef5ef9ff8845e3efa2580' }
)
$observations = foreach ($spec in $specs) {
    $catalogPath = Join-Path $spec.Scope 'workspace.json'
    $catalog = Get-Content -LiteralPath $catalogPath -Raw | ConvertFrom-Json -DateKind String
    $agents = @($catalog.agents | Where-Object id -eq $spec.AgentId)
    if ($agents.Count -ne 1) {
        throw 'Baseline agent identity is not unique in workspace catalog.'
    }
    $agent = $agents[0]
    $configuration = $agent.configurationJson | ConvertFrom-Json -DateKind String
    $timings = Get-Content -LiteralPath (Join-Path $ProofRoot ($spec.Name + '-persisted-dispatch-timings.json')) -Raw | ConvertFrom-Json -DateKind String
    $runRows = foreach ($timing in $timings.runs) {
        $runId = [Guid]$timing.runId
        $runPath = Join-Path $spec.Scope ('execution\runs\' + $runId.ToString('N') + '\run.json')
        $run = Get-Content -LiteralPath $runPath -Raw | ConvertFrom-Json -DateKind String
        $metadata = $run.metadataJson | ConvertFrom-Json -DateKind String
        [pscustomobject]@{
            RunId = $run.id
            SourceKind = $run.sourceKind
            SourceId = $run.sourceId
            ChatSessionId = $run.chatSessionId
            AutoApprovePendingToolCalls = $run.autoApprovePendingToolCalls
            Compatibility = $run.entryAgentRequestCompatibilityEvidence
            ContextContributors = $metadata.contributors
            ContextDigest = $metadata.contextDigest
            WorkspaceScope = $metadata.agentContextWorkspaceScope
            AuthorityPolicyFingerprint = $metadata.agentExecutionAuthority.policyFingerprint
            AuthorityPolicyVersion = $metadata.agentExecutionAuthority.policyVersion
            RunFileBytes = (Get-Item -LiteralPath $runPath).Length
        }
    }
    $fileMetadata = foreach ($relative in @('workspace.json', 'workspace.index.json', 'execution\index.json', 'execution\usage-index.json', 'execution\chat-index.json')) {
        $file = Get-Item -LiteralPath (Join-Path $spec.Scope $relative)
        [pscustomobject]@{ Path = $relative; Bytes = $file.Length; ModifiedAtUtc = $file.LastWriteTimeUtc.ToString('O') }
    }
    [pscustomobject]@{
        Instance = $spec.Name
        Agent = [ordered]@{
            Id = $agent.id
            Name = $agent.name
            UpdatedAtUtc = $agent.updatedAtUtc
            ProviderProfileId = $agent.providerProfileId
            Model = $agent.model
            ReadableModelFromUi = $spec.ReadableModel
            ThinkingEffort = $configuration.modelParameters.reasoningEffort
            ChatHistoryMode = $agent.chatHistoryMode
            ChatHistoryModeName = (@{ 0 = 'ProviderDefault'; 1 = 'FrameworkManaged'; 2 = 'ProviderManaged' })[[int]$agent.chatHistoryMode]
            Temperature = $agent.temperature
            RequirePerServiceCallChatHistoryPersistence = $agent.requirePerServiceCallChatHistoryPersistence
            EnableBackgroundResponses = $agent.enableBackgroundResponses
            ConfigurationSha256 = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($agent.configurationJson)))
            CapabilityCount = @($agent.capabilities).Count
            Capabilities = @($agent.capabilities | Select-Object capabilityId,capabilityKey,kind | Sort-Object capabilityId)
            PermissionFlags = ($agent.permissions | Select-Object canUseTools,canAskOtherAgents,canEscalateToHuman,canObserveOtherAgents,canScheduleWork,requiresApprovalForExternalCalls,autoApproveExternalCallsByDefault)
        }
        RunHistoryDirectoryCountAfterBaseline = @(Get-ChildItem -LiteralPath (Join-Path $spec.Scope 'execution\runs') -Directory).Count
        HistoryFileMetadataAfterBaseline = $fileMetadata
        Runs = $runRows
    }
}
[ordered]@{
    SchemaVersion = 1
    ObservedAtUtc = [DateTimeOffset]::UtcNow.ToString('O')
    ObservationBoundary = 'Read after controlled sampling and before app deployment; agent/catalog configuration was not edited during sampling'
    Instances = $observations
} | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath (Join-Path $ProofRoot 'baseline-configuration.json') -Encoding utf8NoBOM
$observations | ForEach-Object {
    [ordered]@{ Instance = $_.Instance; Agent = $_.Agent.Name; ThinkingEffort = $_.Agent.ThinkingEffort; CapabilityCount = $_.Agent.CapabilityCount; RunHistoryCount = $_.RunHistoryDirectoryCountAfterBaseline } | ConvertTo-Json -Compress
}
