$body = @{ requestedBy = 'codex-scaffold-contract-validation-dispatch' } | ConvertTo-Json
try {
    $response = Invoke-RestMethod -Uri 'http://localhost:5032/api/processes/runs/b1753d52-50de-4825-9501-d2cd501b141d/dispatch' -Method Post -ContentType 'application/json' -Body $body -TimeoutSec 1800
    $response | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath 'C:\repositories\CanDoItAll\codex\bundles\process-module-architecture-v3\proof\SB20-SB28-runtime-completion\dispatch-tetris-run-b1753d52-after-scaffold-contract-fix-response.json' -Encoding UTF8
} catch {
    $message = $_.Exception.ToString()
    if ($_.ErrorDetails -and $_.ErrorDetails.Message) { $message += "
" + $_.ErrorDetails.Message }
    $message | Set-Content -LiteralPath 'C:\repositories\CanDoItAll\codex\bundles\process-module-architecture-v3\proof\SB20-SB28-runtime-completion\dispatch-tetris-run-b1753d52-after-scaffold-contract-fix-error.txt' -Encoding UTF8
    exit 1
}
