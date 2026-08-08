#!/usr/bin/env python3
"""Check source patterns that must disappear during the follow-up refactor."""

from __future__ import annotations

import re
import sys
from pathlib import Path

repo = Path(sys.argv[1]).resolve() if len(sys.argv) > 1 else Path.cwd().resolve()
checks: list[tuple[str, str, str]] = [
    (
        'src/MAF/Common/CanDoItAll.AgentFramework.Core/Execution/AgentFrameworkWorkspaceExecutionService.ExecutionRuns.cs',
        'ContextPolicyFingerprint = AgentTurnContextMetadata.TryReadTurnContextReference(run.MetadataJson)?.ModelContextDigest',
        'Context policy must not be the model-context digest.',
    ),
    (
        'src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafRuntimeAgentFactory.cs',
        'new DefaultAgentToolInvocationPolicy()',
        'MAF must consume an injected governance policy.',
    ),
    (
        'src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Execution/MafStreamingTurnExecutor.cs',
        'new WorkspaceRecoveryArtifactReader(workspaceRoot, workspaceScope)',
        'Recovery must use the run-owned workspace bundle.',
    ),
    (
        'src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafRuntimeAgentFactory.cs',
        'new MafScriptPolicyInspectionService(this.workspaceRoot, workspaceScope)',
        'Script inspection must use the effective run scope.',
    ),
    (
        'src/Modules/CanDoItAll.Modules.AgentFramework/Services/AgentExecutionAuthorityComposition.cs',
        'hint.Permissions.HasFlag(AgentChatContextPermission.Mutate)',
        'UI hints must not grant mutation authority.',
    ),
    (
        'src/Modules/CanDoItAll.Modules.AgentFramework/Pages/Components/AgentChatPanel.razor.cs',
        'new PendingToolApprovalDecision(item.ApprovalId, approved)',
        'Primary UI must support per-proposal decisions.',
    ),
]

errors: list[str] = []
for rel, token, message in checks:
    path = repo / rel
    if not path.is_file():
        errors.append(f'Missing expected source file: {rel}')
        continue
    if token in path.read_text(encoding='utf-8', errors='replace'):
        errors.append(f'{message} Found in {rel}')

# Broad runtime must remain absent. Scan only tracked source roots; build
# artifact directories can contain generated workspaces with unreadable
# long paths on Windows.
import os

skip_dirs = {'bin', 'obj', '.git', 'codex', '.artifacts', 'node_modules'}
source_roots = [repo / 'src', repo / 'tests']
for root in source_roots:
    if not root.is_dir():
        continue
    for dirpath, dirnames, filenames in os.walk(root):
        dirnames[:] = [d for d in dirnames if d not in skip_dirs]
        for filename in filenames:
            if not filename.endswith('.cs'):
                continue
            path = Path(dirpath) / filename
            try:
                text = path.read_text(encoding='utf-8', errors='replace')
            except OSError:
                continue
            # Match the exact broad interface name only; narrow ports such as
            # IAgentRuntimeToolProvider or IAgentRuntimeStateAdapter are allowed.
            if re.search(r'\binterface\s+IAgentRuntime\b', text):
                errors.append(f'Broad IAgentRuntime interface returned: {path.relative_to(repo)}')

if errors:
    print('\n'.join(errors))
    sys.exit(1)
print('Follow-up architecture source guards passed.')
