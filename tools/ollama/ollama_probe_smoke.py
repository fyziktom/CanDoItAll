#!/usr/bin/env python3
"""Send a small chat request through the Ollama probe proxy and print usage counters."""

from __future__ import annotations

import argparse
import json
import os
import sys
import urllib.error
import urllib.request


def main() -> int:
    parser = argparse.ArgumentParser(description="Run a minimal Ollama context probe smoke request.")
    parser.add_argument("--base-url", default=os.environ.get("OLLAMA_PROBE_BASE_URL", "http://127.0.0.1:11534"))
    parser.add_argument("--model", default=os.environ.get("OLLAMA_PROBE_MODEL", "gemma4-12b-256k"))
    parser.add_argument("--num-ctx", type=int, default=int(os.environ.get("OLLAMA_PROBE_NUM_CTX", "262144")))
    parser.add_argument("--num-predict", type=int, default=int(os.environ.get("OLLAMA_PROBE_NUM_PREDICT", "64")))
    args = parser.parse_args()

    request = {
        "model": args.model,
        "stream": False,
        "messages": [
            {
                "role": "system",
                "content": "You are a concise context probe. Answer with exactly one short sentence."
            },
            {
                "role": "user",
                "content": "Reply with the word ok and no extra explanation."
            }
        ],
        "options": {
            "num_ctx": args.num_ctx,
            "num_predict": args.num_predict
        }
    }
    data = json.dumps(request).encode("utf-8")
    http_request = urllib.request.Request(
        args.base_url.rstrip("/") + "/api/chat",
        data=data,
        headers={"Content-Type": "application/json"},
        method="POST")

    try:
        with urllib.request.urlopen(http_request, timeout=120) as response:
            payload = json.loads(response.read().decode("utf-8"))
    except urllib.error.URLError as error:
        print(f"Ollama probe request failed: {error}", file=sys.stderr)
        return 1

    result = {
        "model": payload.get("model", args.model),
        "promptEvalCount": payload.get("prompt_eval_count"),
        "evalCount": payload.get("eval_count"),
        "totalDuration": payload.get("total_duration"),
        "doneReason": payload.get("done_reason")
    }
    print(json.dumps(result, indent=2, sort_keys=True))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
