param(
    [Parameter(Mandatory = $true)]
    [string]$DatabasePath,

    [string]$CreatedPlanPath = "C:\repositories\CanDoItAll\artifacts\canvas-regression-bundle-v1-fresh-validation\created-plan.json",

    [string]$OutputPath = "C:\repositories\CanDoItAll\artifacts\canvas-regression-bundle-v1-fresh-validation\canonical-ai-agent-repair.json"
)

$ErrorActionPreference = "Stop"

$sqliteExe = "C:\ProgramData\Anaconda3\Library\bin\sqlite3.exe"

if (-not (Test-Path $sqliteExe)) {
    throw "sqlite3.exe was not found at '$sqliteExe'."
}

if (-not (Test-Path $DatabasePath)) {
    throw "The SQLite database '$DatabasePath' does not exist."
}

if (-not (Test-Path $CreatedPlanPath)) {
    throw "The created-plan artifact '$CreatedPlanPath' does not exist."
}

function Escape-SqlLiteral {
    param(
        [AllowNull()]
        [string]$Value
    )

    if ($null -eq $Value) {
        return ""
    }

    return $Value.Replace("'", "''")
}

function New-GuidText {
    return ([guid]::NewGuid().ToString("D")).ToUpperInvariant()
}

function Normalize-GuidText {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Value
    )

    return ([guid]$Value).ToString("D").ToUpperInvariant()
}

function Invoke-SqliteJson {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Sql
    )

    $output = & $sqliteExe $DatabasePath ".mode json" $Sql 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "SQLite query failed.`nSQL:`n$Sql`nError:`n$($output -join "`n")"
    }

    if ([string]::IsNullOrWhiteSpace(($output -join ""))) {
        return @()
    }

    return @($output | ConvertFrom-Json)
}

function Invoke-SqliteNonQuery {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Sql
    )

    $output = $Sql | & $sqliteExe $DatabasePath 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "SQLite command failed.`nSQL:`n$Sql`nError:`n$($output -join "`n")"
    }
}

function Get-SingleRow {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Sql
    )

    $rows = Invoke-SqliteJson -Sql $Sql
    if ($rows.Count -eq 0) {
        return $null
    }

    return $rows[0]
}

function Parse-JsonMap {
    param(
        [AllowNull()]
        [string]$Json
    )

    if ([string]::IsNullOrWhiteSpace($Json)) {
        return @{}
    }

    return ConvertTo-MutableMap ($Json | ConvertFrom-Json)
}

function ConvertTo-MutableMap {
    param(
        [AllowNull()]
        [object]$Value
    )

    if ($null -eq $Value) {
        return $null
    }

    if ($Value -is [System.Collections.IDictionary]) {
        $map = @{}
        foreach ($key in $Value.Keys) {
            $map[[string]$key] = ConvertTo-MutableMap $Value[$key]
        }

        return $map
    }

    if ($Value -is [System.Collections.IEnumerable] -and -not ($Value -is [string])) {
        return @(
            foreach ($item in $Value) {
                ConvertTo-MutableMap $item
            }
        )
    }

    if ($Value -is [psobject] -and $Value.PSObject.Properties.Count -gt 0) {
        $map = @{}
        foreach ($property in $Value.PSObject.Properties) {
            $map[$property.Name] = ConvertTo-MutableMap $property.Value
        }

        return $map
    }

    return $Value
}

function ConvertTo-CompactJson {
    param(
        [Parameter(Mandatory = $true)]
        [object]$Value
    )

    return $Value | ConvertTo-Json -Depth 100 -Compress
}

function Ensure-Directory {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    $directory = Split-Path -Parent $Path
    if (-not [string]::IsNullOrWhiteSpace($directory) -and -not (Test-Path $directory)) {
        New-Item -ItemType Directory -Force -Path $directory | Out-Null
    }
}

$plan = ConvertTo-MutableMap (Get-Content -Path $CreatedPlanPath -Raw -Encoding UTF8 | ConvertFrom-Json)
$timestamp = [DateTimeOffset]::UtcNow.ToString("o")

$agentCatalog = @{}
foreach ($agent in $plan.agentCatalog) {
    $agentCatalog[[string]$agent.DisplayName] = $agent
}

$bindings = New-Object System.Collections.Generic.List[object]

foreach ($subproject in @($plan.subprojects)) {
    $projectId = [string]$subproject.projectId
    $projectIdSql = Escape-SqlLiteral $projectId

    foreach ($assignment in @($subproject.aiLane.assignments)) {
        $displayName = [string]$assignment.agentDisplayName
        $agent = $agentCatalog[$displayName]
        if ($null -eq $agent) {
            throw "The created plan does not define agent '$displayName'."
        }

        $participantEntry = @($subproject.aiLane.participants | Where-Object { [string]$_.id -eq [string]$assignment.participantNodeKey })[0]
        $taskEntry = @($subproject.aiLane.tasks | Where-Object { [string]$_.id -eq [string]$assignment.taskNodeKey })[0]
        if ($null -eq $participantEntry -or $null -eq $taskEntry) {
            throw "The created plan is missing the participant or task node for agent '$displayName' in project '$projectId'."
        }

        $participantNodeKey = [string]$participantEntry.id
        $taskNodeKey = [string]$taskEntry.id
        $externalCode = [string]$agent.ExternalCode
        $email = ("{0}@local" -f $externalCode.ToLowerInvariant())
        $summary = [string]$agent.Summary
        $role = [string]$agent.Role
        $skills = @($agent.Skills)
        $capabilityNames = @($agent.CapabilityNames)

        $partyExtendedDataJson = ConvertTo-CompactJson @{
            sourceBundle = "candoitall-project-structure-canvas-regression-bundle-v1"
            sourceArtifact = "candoitall-canonical-architecture-review-bundle-v2"
            projectId = $projectId
            participantNodeKey = $participantNodeKey
            taskNodeKey = $taskNodeKey
            skills = $skills
        }

        $profileExtendedDataJson = ConvertTo-CompactJson @{
            source = "canvas-regression-bundle-v1/fresh-validation"
            linkedParticipantNodeKey = $participantNodeKey
            linkedTaskNodeKey = $taskNodeKey
            summary = $summary
        }

        $capabilities = @(
            foreach ($capabilityName in $capabilityNames) {
                @{
                    Name = $capabilityName
                    Scope = $summary
                    ToolAccess = "Project structure, CRM AI directory, and validation artifacts."
                    Limitations = "No silent ownership changes without explicit human review."
                    Notes = ($skills -join ", ")
                }
            }
        )

        $existingParty = Get-SingleRow -Sql @"
select Id, CreatedAtUtc
from CrmHr_Parties
where PartyType = 'AiAgent'
  and (ExternalCode = '$(Escape-SqlLiteral $externalCode)' or DisplayName = '$(Escape-SqlLiteral $displayName)')
limit 1;
"@

        $originalPartyId = if ($null -ne $existingParty) { [string]$existingParty.Id } else { New-GuidText }
        $partyId = Normalize-GuidText $originalPartyId
        $partyIdSql = Escape-SqlLiteral $partyId
        $createdAtUtc = if ($null -ne $existingParty) { [string]$existingParty.CreatedAtUtc } else { $timestamp }

        $existingContactPoint = Get-SingleRow -Sql @"
select Id
from CrmHr_PartyContactPoints
where lower(PartyId) = lower('$partyIdSql')
  and ContactType = 'Email'
order by IsPrimary desc, rowid asc
limit 1;
"@

        $originalContactPointId = if ($null -ne $existingContactPoint) { [string]$existingContactPoint.Id } else { New-GuidText }
        $contactPointId = Normalize-GuidText $originalContactPointId
        $contactPointIdSql = Escape-SqlLiteral $contactPointId

        $existingProfile = Get-SingleRow -Sql @"
select Id
from CrmHr_AiAgentProfiles
where lower(PartyId) = lower('$partyIdSql')
limit 1;
"@

        $originalProfileId = if ($null -ne $existingProfile) { [string]$existingProfile.Id } else { New-GuidText }
        $profileId = Normalize-GuidText $originalProfileId
        $profileIdSql = Escape-SqlLiteral $profileId

        $participantMetadata = Parse-JsonMap $participantEntry.metadataJson
        if (-not $participantMetadata.ContainsKey("participant") -or $null -eq $participantMetadata.participant) {
            $participantMetadata.participant = @{}
        }

        $participantMetadata.participant.participantKind = "aiAgent"
        $participantMetadata.participant.role = $role
        $participantMetadata.participant.organization = "CanDoItAll canonical bundle backfill"
        $participantMetadata.participant.email = $email
        $participantMetadata.participant.linkedPartyName = $displayName
        $participantMetadata.participant.skills = $skills
        $participantMetadata.participant.externalCode = $externalCode
        $participantMetadataJson = ConvertTo-CompactJson $participantMetadata

        $taskMetadata = Parse-JsonMap $taskEntry.metadataJson
        if (-not $taskMetadata.ContainsKey("workItem") -or $null -eq $taskMetadata.workItem) {
            $taskMetadata.workItem = @{}
        }

        $taskMetadata.workItem.assigneePartyName = $displayName
        $taskMetadata.workItem.agentSkills = $skills
        $taskMetadata.workItem.agentExternalCode = $externalCode
        $taskMetadataJson = ConvertTo-CompactJson $taskMetadata

        $tagsJson = ConvertTo-CompactJson @("ai-agent", "canonical-bundle-backfill", "project-structure", "codex")
        $normalizedEmail = $email.ToLowerInvariant()
        $capabilityJson = ConvertTo-CompactJson $capabilities
        $partyNotes = "Backfilled CRM AI agent for canonical architecture review planning. Role: $role. Skills: $($skills -join ', ')."
        $participantNotes = "Canonical CRM AI agent participant. Role: $role. Skills: $($skills -join ', ')."
        $taskNotes = [string]$taskEntry.notes

        $sql = @"
BEGIN IMMEDIATE;

DELETE FROM CrmHr_ProjectPartyAssignments
WHERE lower(ProjectId) = lower('$projectIdSql')
  AND NodeKey = '$(Escape-SqlLiteral $participantNodeKey)'
  AND AssignmentKind = 'AiAgent';

DELETE FROM CrmHr_ProjectPartyAssignments
WHERE lower(ProjectId) = lower('$projectIdSql')
  AND NodeKey = '$(Escape-SqlLiteral $taskNodeKey)'
  AND AssignmentKind = 'WorkItemAssignee';

DELETE FROM CrmHr_PartyContactPoints
WHERE PartyId = '$partyIdSql'
  AND ContactType = 'Email'
  AND Id <> '$contactPointIdSql';

$(if ($null -ne $existingParty) {
@"
UPDATE CrmHr_Parties
SET LifecycleStatus = 'Active',
    DisplayName = '$(Escape-SqlLiteral $displayName)',
    LegalName = '$(Escape-SqlLiteral $displayName)',
    PreferredName = '$(Escape-SqlLiteral $displayName)',
    ExternalCode = '$(Escape-SqlLiteral $externalCode)',
    Summary = '$(Escape-SqlLiteral $summary)',
    Notes = '$(Escape-SqlLiteral $partyNotes)',
    TagsJson = '$(Escape-SqlLiteral $tagsJson)',
    Region = 'Global',
    CountryCode = 'BO',
    TimeZone = 'America/La_Paz',
    IsSensitive = 0,
    ExtendedDataJson = '$(Escape-SqlLiteral $partyExtendedDataJson)',
    LastChangedBy = 'canvas-regression-bundle-v1/fresh-validation',
    UpdatedAtUtc = '$timestamp'
WHERE Id = '$partyIdSql';
"@
} else {
@"
INSERT INTO CrmHr_Parties (
    Id,
    PartyType,
    LifecycleStatus,
    DisplayName,
    LegalName,
    PreferredName,
    ExternalCode,
    Summary,
    Notes,
    TagsJson,
    Region,
    CountryCode,
    TimeZone,
    IsSensitive,
    ExtendedDataJson,
    LastChangedBy,
    CreatedAtUtc,
    UpdatedAtUtc
) VALUES (
    '$partyIdSql',
    'AiAgent',
    'Active',
    '$(Escape-SqlLiteral $displayName)',
    '$(Escape-SqlLiteral $displayName)',
    '$(Escape-SqlLiteral $displayName)',
    '$(Escape-SqlLiteral $externalCode)',
    '$(Escape-SqlLiteral $summary)',
    '$(Escape-SqlLiteral $partyNotes)',
    '$(Escape-SqlLiteral $tagsJson)',
    'Global',
    'BO',
    'America/La_Paz',
    0,
    '$(Escape-SqlLiteral $partyExtendedDataJson)',
    'canvas-regression-bundle-v1/fresh-validation',
    '$createdAtUtc',
    '$timestamp'
);
"@
})

$(if ($null -ne $existingContactPoint) {
@"
UPDATE CrmHr_PartyContactPoints
SET ContactType = 'Email',
    Label = 'Primary',
    Value = '$(Escape-SqlLiteral $email)',
    NormalizedValue = '$(Escape-SqlLiteral $normalizedEmail)',
    IsPrimary = 1,
    IsPublic = 0,
    Notes = 'Fresh-validation CRM contact for the canonical bundle AI-agent repair.'
WHERE Id = '$contactPointIdSql';
"@
} else {
@"
INSERT INTO CrmHr_PartyContactPoints (
    Id,
    PartyId,
    ContactType,
    Label,
    Value,
    NormalizedValue,
    IsPrimary,
    IsPublic,
    Notes
) VALUES (
    '$contactPointIdSql',
    '$partyIdSql',
    'Email',
    'Primary',
    '$(Escape-SqlLiteral $email)',
    '$(Escape-SqlLiteral $normalizedEmail)',
    1,
    0,
    'Fresh-validation CRM contact for the canonical bundle AI-agent repair.'
);
"@
})

$(if ($null -ne $existingProfile) {
@"
UPDATE CrmHr_AiAgentProfiles
SET ProviderProfileId = NULL,
    DefaultModel = '$(Escape-SqlLiteral ([string]$agent.DefaultModel))',
    ExecutionMode = 'Remote',
    OwnerPartyId = NULL,
    CapabilityJson = '$(Escape-SqlLiteral $capabilityJson)',
    ValidationStatus = 'ReviewRequired',
    LastReviewedAtUtc = '$timestamp',
    Notes = '$(Escape-SqlLiteral $partyNotes)',
    ExtendedDataJson = '$(Escape-SqlLiteral $profileExtendedDataJson)'
WHERE Id = '$profileIdSql';
"@
} else {
@"
INSERT INTO CrmHr_AiAgentProfiles (
    Id,
    PartyId,
    ProviderProfileId,
    DefaultModel,
    ExecutionMode,
    OwnerPartyId,
    CapabilityJson,
    ValidationStatus,
    LastReviewedAtUtc,
    Notes,
    ExtendedDataJson
) VALUES (
    '$profileIdSql',
    '$partyIdSql',
    NULL,
    '$(Escape-SqlLiteral ([string]$agent.DefaultModel))',
    'Remote',
    NULL,
    '$(Escape-SqlLiteral $capabilityJson)',
    'ReviewRequired',
    '$timestamp',
    '$(Escape-SqlLiteral $partyNotes)',
    '$(Escape-SqlLiteral $profileExtendedDataJson)'
);
"@
})

INSERT INTO CrmHr_ProjectPartyAssignments (
    Id,
    ProjectId,
    PartyId,
    AssignmentKind,
    NodeKey,
    PhaseName,
    OpportunityId,
    AllocationPercent,
    StartsAtUtc,
    EndsAtUtc,
    IsPrimary,
    Source,
    Notes
) VALUES (
    '$(Escape-SqlLiteral (New-GuidText))',
    '$projectIdSql',
    '$partyIdSql',
    'AiAgent',
    '$(Escape-SqlLiteral $participantNodeKey)',
    '',
    NULL,
    NULL,
    NULL,
    NULL,
    1,
    'canvas-regression-bundle-v1/fresh-validation',
    'Linked participant node to the canonical CRM AI-agent party.'
);

INSERT INTO CrmHr_ProjectPartyAssignments (
    Id,
    ProjectId,
    PartyId,
    AssignmentKind,
    NodeKey,
    PhaseName,
    OpportunityId,
    AllocationPercent,
    StartsAtUtc,
    EndsAtUtc,
    IsPrimary,
    Source,
    Notes
) VALUES (
    '$(Escape-SqlLiteral (New-GuidText))',
    '$projectIdSql',
    '$partyIdSql',
    'WorkItemAssignee',
    '$(Escape-SqlLiteral $taskNodeKey)',
    '',
    NULL,
    NULL,
    '$(Escape-SqlLiteral ([string]$taskEntry.startUtc))',
    '$(Escape-SqlLiteral ([string]$taskEntry.endUtc))',
    1,
    'canvas-regression-bundle-v1/fresh-validation',
    'Assigned the canonical CRM AI agent to the project-structure work item.'
);

UPDATE Workbench_ProjectObjects
SET Notes = '$(Escape-SqlLiteral $participantNotes)',
    MetadataJson = '$(Escape-SqlLiteral $participantMetadataJson)',
    UpdatedAtUtc = '$timestamp'
WHERE lower(ProjectId) = lower('$projectIdSql')
  AND NodeKey = '$(Escape-SqlLiteral $participantNodeKey)';

UPDATE Workbench_ProjectObjects
SET Notes = '$(Escape-SqlLiteral $taskNotes)',
    MetadataJson = '$(Escape-SqlLiteral $taskMetadataJson)',
    UpdatedAtUtc = '$timestamp'
WHERE lower(ProjectId) = lower('$projectIdSql')
  AND NodeKey = '$(Escape-SqlLiteral $taskNodeKey)';

COMMIT;
"@

        Invoke-SqliteNonQuery -Sql $sql

        $bindings.Add([ordered]@{
            projectId = $projectId
            projectCode = $subproject.code
            displayName = $displayName
            externalCode = $externalCode
            partyId = $partyId
            profileId = $profileId
            participantNodeKey = $participantNodeKey
            taskNodeKey = $taskNodeKey
            capabilityNames = $capabilityNames
        })
    }
}

$bindingArray = $bindings.ToArray()
$plan["crmDirectoryBindings"] = $bindingArray
$plan["crmDirectoryRepairGeneratedAtUtc"] = $timestamp

Ensure-Directory -Path $CreatedPlanPath
$plan | ConvertTo-Json -Depth 100 | Set-Content -Encoding utf8 -Path $CreatedPlanPath

$result = [ordered]@{
    generatedAtUtc = $timestamp
    databasePath = $DatabasePath
    createdPlanPath = $CreatedPlanPath
    bindingCount = $bindings.Count
    bindings = $bindingArray
}

Ensure-Directory -Path $OutputPath
$result | ConvertTo-Json -Depth 100 | Set-Content -Encoding utf8 -Path $OutputPath
$result | ConvertTo-Json -Depth 100
