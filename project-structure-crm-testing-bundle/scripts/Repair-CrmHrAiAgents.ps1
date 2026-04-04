param(
    [Parameter(Mandatory = $true)]
    [string]$DatabasePath,

    [string]$CreatedPlanPath = "C:\repositories\CanDoItAll\artifacts\project-structure-crm-testing\created-plan.json",

    [string]$OutputPath = "C:\repositories\CanDoItAll\artifacts\project-structure-crm-testing\crm-ai-agent-repair.json"
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

    return @($output | ConvertFrom-Json -Depth 100)
}

function Invoke-SqliteNonQuery {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Sql
    )

    $output = & $sqliteExe $DatabasePath $Sql 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "SQLite command failed.`nSQL:`n$Sql`nError:`n$($output -join "`n")"
    }
}

function ConvertTo-CompactJson {
    param(
        [Parameter(Mandatory = $true)]
        [object]$Value
    )

    return $Value | ConvertTo-Json -Depth 100 -Compress
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

    return $Json | ConvertFrom-Json -AsHashtable -Depth 100
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

$plan = Get-Content -Raw $CreatedPlanPath | ConvertFrom-Json -AsHashtable -Depth 100
$b04 = @($plan.subprojects | Where-Object { $_.code -eq "B04" })[0]
if ($null -eq $b04) {
    throw "The created plan does not contain the B04 subproject entry."
}

if ($null -eq $b04.crmAiLane) {
    throw "The created plan does not contain the B04 CRM AI lane."
}

$projectId = [string]$b04.projectId
$projectIdSql = Escape-SqlLiteral $projectId
$timestamp = [DateTimeOffset]::UtcNow.ToString("o")

$agentSpecifications = @(
    @{
        DisplayName = "CRM Domain Steward"
        ExternalCode = "CRM-DOMAIN-STEWARD"
        Email = "crm-domain-steward@local"
        ParticipantRole = "Unified account and contact model steward"
        ParticipantNotes = "Owns canonical projection checks for account and contact surfaces. Skills: canonical model review, projection drift detection, and route consistency auditing."
        TaskTitle = "Validate unified account and contact projections"
        TaskDescription = "Model-level review of CRM account and contact projections, with explicit drift findings and route-impact notes."
        TaskNotes = "Verify that CRM routes reflect the unified party model without duplicate or split ownership behavior. Agent: CRM Domain Steward. Expected output: canonical model drift findings with route-impact notes."
        Summary = "Canonical CRM model steward for account and contact surfaces. Skills: canonical model review, route consistency auditing, and projection drift detection."
        PartyNotes = "Created by the project-structure CRM testing bundle repair so the B04 planning lane is backed by a real CRM AI-agent directory record instead of a local-only participant node."
        DefaultModel = "gpt-5.4"
        ExecutionMode = "Remote"
        ValidationStatus = "ReviewRequired"
        Capabilities = @(
            @{
                Name = "Canonical model review"
                Scope = "Account, contact, and party projection seams across CRM routes."
                ToolAccess = "CRM routes, project-structure assignments, targeted test evidence."
                Limitations = "Does not approve schema or ownership changes without human review."
                Notes = "Escalates split source-of-truth risks and projection drift."
            },
            @{
                Name = "Route consistency audit"
                Scope = "Account and contact list/detail surfaces plus project-workbench handoff points."
                ToolAccess = "Blazor route verification, notes, and regression evidence."
                Limitations = "No autonomous deployment or data migration authority."
                Notes = "Focuses on user-visible behavioral consistency."
            }
        )
    },
    @{
        DisplayName = "Relationship Mapper"
        ExternalCode = "RELATIONSHIP-MAPPER"
        Email = "relationship-mapper@local"
        ParticipantRole = "Stakeholder and relationship mapping reviewer"
        ParticipantNotes = "Checks relationship roles, participant carry-through, and shared party semantics. Skills: stakeholder graph review, interaction participant tracing, and ambiguity detection."
        TaskTitle = "Probe stakeholder role and interaction participant mapping"
        TaskDescription = "Surface relationship ambiguities before they become user-facing data drift, including stakeholder roles and interaction participant carry-through."
        TaskNotes = "Review stakeholder-role integrity, interaction participant persistence, and party linkage clarity. Agent: Relationship Mapper. Expected output: stakeholder role matrix and interaction-participant ambiguity findings."
        Summary = "Stakeholder and interaction mapping reviewer for CRM relationship fidelity. Skills: role-link auditing, participant carry-through review, and ambiguity detection."
        PartyNotes = "Created by the project-structure CRM testing bundle repair to bind the B04 relationship reviewer to a reusable CRM AI-agent identity."
        DefaultModel = "gpt-5.4"
        ExecutionMode = "Remote"
        ValidationStatus = "ReviewRequired"
        Capabilities = @(
            @{
                Name = "Relationship graph audit"
                Scope = "Stakeholders, related contacts, and party-role mappings across CRM and Workbench."
                ToolAccess = "CRM relationship records, project-party assignments, validation notes."
                Limitations = "Does not invent missing business relationships."
                Notes = "Flags ambiguous or duplicate role ownership before workflow drift spreads."
            },
            @{
                Name = "Interaction participant tracing"
                Scope = "Meeting, call, email, and follow-up participant carry-through."
                ToolAccess = "Interaction journals, participant nodes, work-item ownership metadata."
                Limitations = "Requires human confirmation for remediation priorities."
                Notes = "Optimized for cross-surface consistency review."
            }
        )
    },
    @{
        DisplayName = "Follow-up Guardian"
        ExternalCode = "FOLLOW-UP-GUARDIAN"
        Email = "follow-up-guardian@local"
        ParticipantRole = "Interaction and overdue follow-up regression pilot"
        ParticipantNotes = "Exercises interaction logging, next-action ownership, and visibility of overdue work. Skills: overdue-action regression review, owner-flow verification, and operational readiness checks."
        TaskTitle = "Exercise overdue follow-up and next-action workflow regressions"
        TaskDescription = "Catch route or workflow gaps around next actions, overdue surfacing, and ownership carry-through before the CRM follow-up surface drifts."
        TaskNotes = "Run regression thinking against follow-up ownership, overdue surfacing, and operational route readiness. Agent: Follow-up Guardian. Expected output: overdue next-action regression risks and route readiness notes."
        Summary = "Interaction and follow-up workflow watchdog. Skills: overdue-action regression review, ownership verification, and operational readiness checks."
        PartyNotes = "Created by the project-structure CRM testing bundle repair to make the B04 follow-up watchdog visible in the CRM module and reusable across task ownership."
        DefaultModel = "gpt-5.4"
        ExecutionMode = "Remote"
        ValidationStatus = "ReviewRequired"
        Capabilities = @(
            @{
                Name = "Overdue workflow regression review"
                Scope = "Next-action ownership, overdue surfacing, and operator-facing follow-up flows."
                ToolAccess = "Interaction journals, overdue dashboards, work-item assignments."
                Limitations = "Does not close or reassign follow-ups without human approval."
                Notes = "Focuses on operational readiness before rollout."
            },
            @{
                Name = "Ownership carry-through validation"
                Scope = "Assignee propagation from project structure to CRM follow-up behavior."
                ToolAccess = "Project-party assignments, work-item metadata, CRM route checks."
                Limitations = "Human steward owns final workflow policy."
                Notes = "Catches assignee drift before it becomes execution noise."
            }
        )
    }
)

$bindings = New-Object System.Collections.Generic.List[object]

foreach ($spec in $agentSpecifications) {
    $participantEntry = @($b04.crmAiLane.Participants | Where-Object { $_.title -eq $spec.DisplayName })[0]
    if ($null -eq $participantEntry) {
        throw "Could not find the participant node '$($spec.DisplayName)' in the B04 AI lane."
    }

    $taskEntry = @($b04.crmAiLane.Tasks | Where-Object { $_.title -eq $spec.TaskTitle })[0]
    if ($null -eq $taskEntry) {
        throw "Could not find the task node '$($spec.TaskTitle)' in the B04 AI lane."
    }

    $participantNodeKey = [string]$participantEntry.id
    $taskNodeKey = [string]$taskEntry.id

    $tagsJson = ConvertTo-CompactJson @(
        "crm",
        "ai-agent",
        "bundle-backfill",
        "b04"
    )

    $capabilityNames = @($spec.Capabilities | ForEach-Object { $_.Name })

    $partyExtendedDataJson = ConvertTo-CompactJson @{
        sourceBundle = "project-structure-crm-testing-bundle"
        sourceArtifact = "CanDoItAll_CrmHr_CodexBundle_Final"
        projectId = $projectId
        participantNodeKey = $participantNodeKey
        taskNodeKey = $taskNodeKey
        skills = $capabilityNames
    }

    $profileExtendedDataJson = ConvertTo-CompactJson @{
        source = "project-structure-crm-testing-bundle/repair"
        linkedParticipantNodeKey = $participantNodeKey
        linkedTaskNodeKey = $taskNodeKey
        summary = $spec.Summary
    }

    $capabilityJson = ConvertTo-CompactJson $spec.Capabilities

    $existingParty = Get-SingleRow -Sql @"
select Id, CreatedAtUtc
from CrmHr_Parties
where PartyType = 'AiAgent'
  and (ExternalCode = '$(Escape-SqlLiteral $spec.ExternalCode)' or DisplayName = '$(Escape-SqlLiteral $spec.DisplayName)')
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
    $normalizedEmail = $spec.Email.Trim().ToLowerInvariant()

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
    $participantMetadata.participant.role = $spec.ParticipantRole
    $participantMetadata.participant.organization = "CanDoItAll CRM backfill"
    $participantMetadata.participant.email = $spec.Email
    $participantMetadata.participant.phone = ""
    $participantMetadata.participant.linkedPartyName = $spec.DisplayName
    $participantMetadataJson = ConvertTo-CompactJson $participantMetadata

    $taskMetadata = Parse-JsonMap $taskEntry.metadataJson
    if (-not $taskMetadata.ContainsKey("workItem") -or $null -eq $taskMetadata.workItem) {
        $taskMetadata.workItem = @{}
    }

    $taskMetadata.workItem.assigneePartyName = $spec.DisplayName
    $taskMetadata.workItem.description = $spec.TaskDescription
    $taskMetadataJson = ConvertTo-CompactJson $taskMetadata

    $sql = @"
BEGIN IMMEDIATE;

$(if ($null -ne $existingParty -and $originalPartyId -cne $partyId) {
@"
UPDATE CrmHr_AiAgentProfiles
SET PartyId = '$partyIdSql'
WHERE lower(PartyId) = lower('$(Escape-SqlLiteral $originalPartyId)');

UPDATE CrmHr_PartyContactPoints
SET PartyId = '$partyIdSql'
WHERE lower(PartyId) = lower('$(Escape-SqlLiteral $originalPartyId)');

UPDATE CrmHr_ProjectPartyAssignments
SET PartyId = '$partyIdSql'
WHERE lower(PartyId) = lower('$(Escape-SqlLiteral $originalPartyId)');

UPDATE CrmHr_Parties
SET Id = '$partyIdSql'
WHERE lower(Id) = lower('$(Escape-SqlLiteral $originalPartyId)');
"@
} else {
    ""
})

$(if ($null -ne $existingContactPoint -and $originalContactPointId -cne $contactPointId) {
@"
UPDATE CrmHr_PartyContactPoints
SET Id = '$contactPointIdSql'
WHERE lower(Id) = lower('$(Escape-SqlLiteral $originalContactPointId)');
"@
} else {
    ""
})

$(if ($null -ne $existingProfile -and $originalProfileId -cne $profileId) {
@"
UPDATE CrmHr_AiAgentProfiles
SET Id = '$profileIdSql'
WHERE lower(Id) = lower('$(Escape-SqlLiteral $originalProfileId)');
"@
} else {
    ""
})

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
    DisplayName = '$(Escape-SqlLiteral $spec.DisplayName)',
    LegalName = '$(Escape-SqlLiteral $spec.DisplayName)',
    PreferredName = '$(Escape-SqlLiteral $spec.DisplayName)',
    ExternalCode = '$(Escape-SqlLiteral $spec.ExternalCode)',
    Summary = '$(Escape-SqlLiteral $spec.Summary)',
    Notes = '$(Escape-SqlLiteral $spec.PartyNotes)',
    TagsJson = '$(Escape-SqlLiteral $tagsJson)',
    Region = 'Global',
    CountryCode = 'BO',
    TimeZone = 'America/La_Paz',
    IsSensitive = 0,
    ExtendedDataJson = '$(Escape-SqlLiteral $partyExtendedDataJson)',
    LastChangedBy = 'project-structure-crm-testing-bundle/repair',
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
    '$(Escape-SqlLiteral $spec.DisplayName)',
    '$(Escape-SqlLiteral $spec.DisplayName)',
    '$(Escape-SqlLiteral $spec.DisplayName)',
    '$(Escape-SqlLiteral $spec.ExternalCode)',
    '$(Escape-SqlLiteral $spec.Summary)',
    '$(Escape-SqlLiteral $spec.PartyNotes)',
    '$(Escape-SqlLiteral $tagsJson)',
    'Global',
    'BO',
    'America/La_Paz',
    0,
    '$(Escape-SqlLiteral $partyExtendedDataJson)',
    'project-structure-crm-testing-bundle/repair',
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
    Value = '$(Escape-SqlLiteral $spec.Email)',
    NormalizedValue = '$(Escape-SqlLiteral $normalizedEmail)',
    IsPrimary = 1,
    IsPublic = 0,
    Notes = 'Local CRM test contact for the project-structure AI-agent repair.'
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
    '$(Escape-SqlLiteral $spec.Email)',
    '$(Escape-SqlLiteral $normalizedEmail)',
    1,
    0,
    'Local CRM test contact for the project-structure AI-agent repair.'
);
"@
})

$(if ($null -ne $existingProfile) {
@"
UPDATE CrmHr_AiAgentProfiles
SET ProviderProfileId = NULL,
    DefaultModel = '$(Escape-SqlLiteral $spec.DefaultModel)',
    ExecutionMode = '$(Escape-SqlLiteral $spec.ExecutionMode)',
    OwnerPartyId = NULL,
    CapabilityJson = '$(Escape-SqlLiteral $capabilityJson)',
    ValidationStatus = '$(Escape-SqlLiteral $spec.ValidationStatus)',
    LastReviewedAtUtc = '$timestamp',
    Notes = '$(Escape-SqlLiteral $spec.PartyNotes)',
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
    '$(Escape-SqlLiteral $spec.DefaultModel)',
    '$(Escape-SqlLiteral $spec.ExecutionMode)',
    NULL,
    '$(Escape-SqlLiteral $capabilityJson)',
    '$(Escape-SqlLiteral $spec.ValidationStatus)',
    '$timestamp',
    '$(Escape-SqlLiteral $spec.PartyNotes)',
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
    'project-structure-crm-testing-bundle/repair',
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
    NULL,
    NULL,
    1,
    'project-structure-crm-testing-bundle/repair',
    'Assigned the CRM AI agent to the B04 work item through the canonical project-party assignment seam.'
);

UPDATE Workbench_ProjectObjects
SET Notes = '$(Escape-SqlLiteral $spec.ParticipantNotes)',
    MetadataJson = '$(Escape-SqlLiteral $participantMetadataJson)',
    UpdatedAtUtc = '$timestamp'
WHERE lower(ProjectId) = lower('$projectIdSql')
  AND NodeKey = '$(Escape-SqlLiteral $participantNodeKey)';

UPDATE Workbench_ProjectObjects
SET Notes = '$(Escape-SqlLiteral $spec.TaskNotes)',
    MetadataJson = '$(Escape-SqlLiteral $taskMetadataJson)',
    UpdatedAtUtc = '$timestamp'
WHERE lower(ProjectId) = lower('$projectIdSql')
  AND NodeKey = '$(Escape-SqlLiteral $taskNodeKey)';

COMMIT;
"@

    Invoke-SqliteNonQuery -Sql $sql

    $binding = [ordered]@{
        displayName = $spec.DisplayName
        externalCode = $spec.ExternalCode
        projectId = $projectId
        partyId = $partyId
        profileId = $profileId
        participantNodeKey = $participantNodeKey
        taskNodeKey = $taskNodeKey
        defaultModel = $spec.DefaultModel
        executionMode = $spec.ExecutionMode
        validationStatus = $spec.ValidationStatus
        capabilityNames = $capabilityNames
        summary = $spec.Summary
    }

    $bindings.Add([pscustomobject]$binding)

    $participantEntry.notes = $spec.ParticipantNotes
    $participantEntry.metadataJson = $participantMetadataJson
    $taskEntry.notes = $spec.TaskNotes
    $taskEntry.metadataJson = $taskMetadataJson
}

$bindingArray = $bindings.ToArray()

$plan["crmDirectoryBindings"] = $bindingArray
$plan["crmDirectoryRepairGeneratedAtUtc"] = $timestamp

Ensure-Directory -Path $CreatedPlanPath
$plan | ConvertTo-Json -Depth 100 | Set-Content -Encoding utf8 -Path $CreatedPlanPath

$repairResult = [ordered]@{
    generatedAtUtc = $timestamp
    databasePath = $DatabasePath
    createdPlanPath = $CreatedPlanPath
    projectId = $projectId
    bindingCount = $bindings.Count
    bindings = $bindingArray
}

Ensure-Directory -Path $OutputPath
$repairResult | ConvertTo-Json -Depth 100 | Set-Content -Encoding utf8 -Path $OutputPath
$repairResult | ConvertTo-Json -Depth 100
