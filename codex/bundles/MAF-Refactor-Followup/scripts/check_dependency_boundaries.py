#!/usr/bin/env python3
"""Verify key project-reference boundaries after the corrective work."""

from __future__ import annotations

import sys
import xml.etree.ElementTree as ET
from pathlib import Path

repo = Path(sys.argv[1]).resolve() if len(sys.argv) > 1 else Path.cwd().resolve()

rules = {
    'src/MAF/Common/CanDoItAll.AgentFramework.Maf/CanDoItAll.AgentFramework.Maf.csproj': [
        'src/Modules/',
        'CanDoItAll.AgentFramework.Workflows.MafAdapter',
    ],
    'src/MAF/Common/CanDoItAll.AgentFramework.Runtime.Abstractions/CanDoItAll.AgentFramework.Runtime.Abstractions.csproj': [
        'CanDoItAll.AgentFramework.Maf', 'src/Modules/', 'Microsoft.Agents.AI',
    ],
    'src/MAF/Common/CanDoItAll.AgentFramework.Llm.Abstractions/CanDoItAll.AgentFramework.Llm.Abstractions.csproj': [
        'CanDoItAll.AgentFramework.Maf', 'CanDoItAll.AgentFramework.Core', 'src/Modules/', 'Microsoft.Agents.AI',
    ],
    'src/MAF/Common/CanDoItAll.AgentFramework.Llm.ProviderRuntime/CanDoItAll.AgentFramework.Llm.ProviderRuntime.csproj': [
        'CanDoItAll.AgentFramework.Maf', 'CanDoItAll.AgentFramework.Core', 'src/Modules/', 'src/UI/',
    ],
}

errors: list[str] = []
for rel, forbidden in rules.items():
    path = repo / rel
    if not path.is_file():
        errors.append(f'Missing project: {rel}')
        continue
    text = path.read_text(encoding='utf-8', errors='replace').replace('\\', '/')
    for token in forbidden:
        if token.replace('\\', '/') in text:
            errors.append(f'Forbidden dependency token {token!r} in {rel}')
    try:
        ET.parse(path)
    except ET.ParseError as exc:
        errors.append(f'Invalid project XML {rel}: {exc}')

if errors:
    print('\n'.join(errors))
    sys.exit(1)
print('Key dependency boundaries passed.')
