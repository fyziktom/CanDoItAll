#!/usr/bin/env python3
"""Check source markers for the Simple Chat streaming/SSE contract."""

from __future__ import annotations

import argparse
import sys
from pathlib import Path


def read_all(root: Path, patterns: tuple[str, ...]) -> str:
    parts: list[str] = []
    for pattern in patterns:
        for path in root.rglob(pattern):
            try:
                parts.append(path.read_text(encoding="utf-8", errors="replace"))
            except OSError:
                continue
    return "\n".join(parts)


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--repo-root", type=Path, required=True)
    args = parser.parse_args()
    root = args.repo_root.resolve()
    errors: list[str] = []

    llm_abstractions = root / (
        "src/MAF/Common/CanDoItAll.AgentFramework.Llm.Abstractions"
    )
    providers = root / "src/MAF/Common/CanDoItAll.AgentFramework.Providers"
    provider_runtime = root / (
        "src/MAF/Common/CanDoItAll.AgentFramework.Llm.ProviderRuntime"
    )
    web_api = root / "src/App/CanDoItAll.Web/Api"
    simple_product = root / "src/Modules/CanDoItAll.Modules.LlmChats"
    simple_persistence = root / (
        "src/Modules/CanDoItAll.Modules.LlmChats.Persistence"
    )

    abstraction_text = read_all(llm_abstractions, ("*.cs",))
    provider_text = read_all(providers, ("*.cs",))
    runtime_text = read_all(provider_runtime, ("*.cs",))
    api_text = read_all(web_api, ("*.cs",))
    product_text = read_all(simple_product, ("*.cs",))
    persistence_text = read_all(simple_persistence, ("*.cs",))

    required_markers = (
        (
            "ILlmStreamingInvocationPort",
            abstraction_text,
            "provider-neutral streaming port",
        ),
        (
            "IProviderStreamingChatCompletionDriver",
            provider_text,
            "provider streaming capability",
        ),
        (
            "ProviderBackedLlmStreamingInvocationAdapter",
            runtime_text,
            "provider runtime streaming adapter",
        ),
        (
            "ServerSentEventResponseWriter",
            api_text,
            "reuse of existing SSE writer",
        ),
        (
            "ProfileBoundedReplayEventStream",
            api_text,
            "reuse of profile-bounded stream",
        ),
        (
            "Last-Event-ID",
            api_text,
            "SSE resume cursor documentation/source marker",
        ),
        (
            "llm.response.delta",
            api_text + product_text + persistence_text,
            "versioned delta event",
        ),
        (
            "llm.operation.succeeded",
            api_text + product_text + persistence_text,
            "terminal success event",
        ),
        (
            "LeaseExpiresAt",
            product_text + persistence_text,
            "durable execution lease",
        ),
    )
    for marker, text, description in required_markers:
        if marker not in text:
            errors.append(f"Missing {description}: marker {marker}")

    forbidden_product_markers = (
        "ServerSentEventResponseWriter",
        "HttpContext",
        "text/event-stream",
    )
    for marker in forbidden_product_markers:
        if marker in product_text or marker in persistence_text:
            errors.append(
                f"Product/persistence source owns Web SSE concern: {marker}"
            )

    # Require concrete drivers to opt into streaming rather than only a fake
    # product-level chunker.
    for driver_name in ("OpenAiProviderDriver", "OllamaProviderDriver"):
        matching = [
            path for path in providers.rglob(f"*{driver_name}*.cs")
            if path.is_file()
        ]
        if not matching:
            errors.append(f"Provider driver not found: {driver_name}")
            continue
        text = "\n".join(
            path.read_text(encoding="utf-8", errors="replace")
            for path in matching
        )
        streaming_markers = ("Stream", "stream")
        if not any(marker in text for marker in streaming_markers):
            errors.append(f"{driver_name} has no streaming implementation marker")

    if errors:
        print("\n".join(f"ERROR: {error}" for error in errors), file=sys.stderr)
        return 1

    print("Streaming/SSE source contract check passed.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
