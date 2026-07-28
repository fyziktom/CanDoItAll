# Ollama Context Probe

This folder contains optional local-only tooling for measuring the prompt shape sent to Ollama-compatible providers. It requires a local Ollama installation and Python 3; it is not part of the web application runtime or routine validation gate.

Run the commands from the repository root.

Create the long-context model:

```powershell
ollama create gemma4-12b-256k -f .\tools\ollama\Modelfile.gemma4-12b-256k
```

Start the logging proxy:

```powershell
python .\tools\ollama\ollama_probe_proxy.py --listen 127.0.0.1:11534 --target http://127.0.0.1:11434 --log-dir .\tools\ollama\.probe
```

Run a smoke request through the proxy:

```powershell
python .\tools\ollama\ollama_probe_smoke.py --base-url http://127.0.0.1:11534 --model gemma4-12b-256k
```

The proxy log records request body byte counts, message counts, message text character counts, tool schema character counts, body hashes, and Ollama response counters such as `prompt_eval_count` and `eval_count`. It does not persist prompt text by default.
