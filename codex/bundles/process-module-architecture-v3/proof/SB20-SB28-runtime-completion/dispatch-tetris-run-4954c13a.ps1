$ErrorActionPreference = 'Stop'
try {
    $body = @{ requestedBy = 'codex-runtime-handoff-validation' } | ConvertTo-Json -Depth 5
    $response = Invoke-RestMethod -Uri 'http://localhost:5032/api/processes/runs/4954c13a-8baa-4c06-a8a2-58300105acb9/dispatch' -Method Post -ContentType 'application/json' -Body $body -TimeoutSec 1800
    $response | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath 'C:\repositories\CanDoItAll\codex\bundles\process-module-architecture-v3\proof\SB20-SB28-runtime-completion\dispatch-tetris-run-4954c13a-response.json'
}
catch {
    $_.Exception.ToString() | Set-Content -LiteralPath 'C:\repositories\CanDoItAll\codex\bundles\process-module-architecture-v3\proof\SB20-SB28-runtime-completion\dispatch-tetris-run-4954c13a-error.txt'
    throw
}
