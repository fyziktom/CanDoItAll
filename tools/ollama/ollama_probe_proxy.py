#!/usr/bin/env python3
import argparse
import hashlib
import http.server
import json
import sys
import time
import urllib.error
import urllib.request
from datetime import datetime, timezone
from pathlib import Path


HOP_BY_HOP_HEADERS = {
    "connection",
    "content-encoding",
    "content-length",
    "keep-alive",
    "proxy-authenticate",
    "proxy-authorization",
    "te",
    "trailer",
    "transfer-encoding",
    "upgrade",
}


def utc_now():
    return datetime.now(timezone.utc).isoformat()


def sha256_hex(value):
    return hashlib.sha256(value).hexdigest()


def text_length(value):
    return len(value) if isinstance(value, str) else 0


def summarize_messages(messages):
    summary = {
        "count": 0,
        "textChars": 0,
        "roles": {},
    }

    if not isinstance(messages, list):
        return summary

    summary["count"] = len(messages)
    for message in messages:
        if not isinstance(message, dict):
            continue

        role = str(message.get("role") or "unknown")
        content = message.get("content")
        chars = 0
        if isinstance(content, str):
            chars = len(content)
        elif isinstance(content, list):
            chars = sum(text_length(part.get("text")) for part in content if isinstance(part, dict))

        role_summary = summary["roles"].setdefault(role, {"count": 0, "textChars": 0})
        role_summary["count"] += 1
        role_summary["textChars"] += chars
        summary["textChars"] += chars

    return summary


def summarize_tools(tools):
    if not isinstance(tools, list):
        return {
            "count": 0,
            "schemaChars": 0,
        }

    return {
        "count": len(tools),
        "schemaChars": len(json.dumps(tools, sort_keys=True, separators=(",", ":"))),
    }


def summarize_request_body(body):
    summary = {
        "bodyBytes": len(body),
        "bodySha256": sha256_hex(body),
    }

    try:
        payload = json.loads(body.decode("utf-8")) if body else {}
    except (UnicodeDecodeError, json.JSONDecodeError):
        summary["json"] = False
        return summary

    if not isinstance(payload, dict):
        summary["json"] = False
        return summary

    summary.update({
        "json": True,
        "model": payload.get("model"),
        "stream": payload.get("stream"),
        "options": payload.get("options") if isinstance(payload.get("options"), dict) else {},
        "promptChars": text_length(payload.get("prompt")),
        "messages": summarize_messages(payload.get("messages")),
        "tools": summarize_tools(payload.get("tools")),
    })

    return summary


def parse_json_lines(body):
    chunks = []
    for line in body.splitlines():
        if not line.strip():
            continue

        try:
            chunks.append(json.loads(line.decode("utf-8")))
        except (UnicodeDecodeError, json.JSONDecodeError):
            return []

    return chunks


def summarize_response_body(body):
    summary = {
        "bodyBytes": len(body),
        "bodySha256": sha256_hex(body),
    }

    json_chunks = parse_json_lines(body)
    if not json_chunks:
        try:
            payload = json.loads(body.decode("utf-8")) if body else {}
            json_chunks = [payload] if isinstance(payload, dict) else []
        except (UnicodeDecodeError, json.JSONDecodeError):
            json_chunks = []

    summary["jsonChunkCount"] = len(json_chunks)
    if not json_chunks:
        return summary

    final_chunk = next((chunk for chunk in reversed(json_chunks) if chunk.get("done") is True), json_chunks[-1])
    for key in (
        "done",
        "prompt_eval_count",
        "eval_count",
        "total_duration",
        "load_duration",
        "prompt_eval_duration",
        "eval_duration",
    ):
        if key in final_chunk:
            summary[key] = final_chunk[key]

    return summary


class OllamaProbeProxy(http.server.BaseHTTPRequestHandler):
    protocol_version = "HTTP/1.1"

    def log_message(self, format, *args):
        sys.stderr.write("%s - %s\n" % (self.address_string(), format % args))

    def do_GET(self):
        self.forward()

    def do_POST(self):
        self.forward()

    def forward(self):
        started = time.perf_counter()
        content_length = int(self.headers.get("Content-Length") or 0)
        request_body = self.rfile.read(content_length) if content_length else b""
        target_url = self.server.target_base + self.path

        headers = {
            key: value
            for key, value in self.headers.items()
            if key.lower() not in HOP_BY_HOP_HEADERS and key.lower() != "host"
        }

        outbound = urllib.request.Request(
            target_url,
            data=request_body if self.command != "GET" else None,
            headers=headers,
            method=self.command,
        )

        status = 502
        response_headers = {}
        response_body = b""
        error = None

        try:
            with urllib.request.urlopen(outbound, timeout=self.server.timeout_seconds) as response:
                status = response.status
                response_headers = dict(response.headers.items())
                response_body = response.read()
        except urllib.error.HTTPError as exc:
            status = exc.code
            response_headers = dict(exc.headers.items())
            response_body = exc.read()
        except Exception as exc:
            error = f"{type(exc).__name__}: {exc}"
            response_body = json.dumps({"error": error}).encode("utf-8")
            response_headers = {"Content-Type": "application/json"}

        self.send_response(status)
        for key, value in response_headers.items():
            if key.lower() not in HOP_BY_HOP_HEADERS:
                self.send_header(key, value)
        self.send_header("Content-Length", str(len(response_body)))
        self.end_headers()
        self.wfile.write(response_body)

        elapsed_ms = round((time.perf_counter() - started) * 1000, 2)
        self.server.write_probe_record({
            "timestampUtc": utc_now(),
            "method": self.command,
            "path": self.path,
            "status": status,
            "elapsedMs": elapsed_ms,
            "error": error,
            "request": summarize_request_body(request_body),
            "response": summarize_response_body(response_body),
        })


class ThreadingHTTPServer(http.server.ThreadingHTTPServer):
    daemon_threads = True

    def __init__(self, server_address, handler_class, target_base, log_path, timeout_seconds):
        super().__init__(server_address, handler_class)
        self.target_base = target_base.rstrip("/")
        self.log_path = log_path
        self.timeout_seconds = timeout_seconds

    def write_probe_record(self, record):
        with self.log_path.open("a", encoding="utf-8") as writer:
            writer.write(json.dumps(record, sort_keys=True, separators=(",", ":")))
            writer.write("\n")


def parse_listen(value):
    if ":" not in value:
        raise argparse.ArgumentTypeError("listen value must be HOST:PORT")

    host, port_text = value.rsplit(":", 1)
    return host, int(port_text)


def normalize_target_base(value):
    target = value.strip()
    if not target:
        raise argparse.ArgumentTypeError("target value cannot be empty")

    return target if "://" in target else "http://" + target


def main():
    parser = argparse.ArgumentParser(description="Record Ollama request shape while proxying to the real Ollama API.")
    parser.add_argument("--listen", default="127.0.0.1:11534", type=parse_listen)
    parser.add_argument("--target", default="http://127.0.0.1:11434", type=normalize_target_base)
    parser.add_argument("--log-dir", default=".artifacts/ollama-probe")
    parser.add_argument("--timeout-seconds", type=int, default=600)
    args = parser.parse_args()

    log_dir = Path(args.log_dir)
    log_dir.mkdir(parents=True, exist_ok=True)
    log_path = log_dir / "ollama-probe.jsonl"

    server = ThreadingHTTPServer(
        args.listen,
        OllamaProbeProxy,
        args.target,
        log_path,
        args.timeout_seconds,
    )

    print(f"Listening on http://{args.listen[0]}:{args.listen[1]}", flush=True)
    print(f"Forwarding to {args.target.rstrip('/')}", flush=True)
    print(f"Writing probe log to {log_path.resolve()}", flush=True)
    server.serve_forever()


if __name__ == "__main__":
    main()
