# Prompt: detached remote job runner

Implementuj detached remote job runner.

Požadavky:
- dlouhá operace běží na remote hostu nezávisle na jednom SSH session,
- výstupy ukládá do `/opt/candoitall/.mcp-state/jobs/<operationId>/`,
- ukládá `status.json`, `stdout.log`, `stderr.log`, `pid`, `exit-code.txt`,
- podporuje `operation_status`, `operation_wait`, `operation_logs`,
- polling musí být deterministický,
- logy musí být redigované.

Důraz:
- reconnect klienta nesmí ztratit stav,
- krátké operace mohou běžet inline,
- mutující tools rozhodují sync vs detached podle očekávané délky.
