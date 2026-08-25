# CanDoItAll Shared Providers Test Upstream

Deterministic, repository-owned ASP.NET Core fixture for the SB07 shared-provider Docker lane. It has no product-project references and performs no outbound network calls. It is test support only and must not be exposed outside an isolated development or CI network.

## Run

From the repository root:

```powershell
$env:Fixture__Authentication__DataTokenFile = "<path-to-data-token>"
$env:Fixture__Authentication__ControlTokenFile = "<path-to-distinct-control-token>"
dotnet run --project .\tests\Support\CanDoItAll.SharedProviders.TestUpstream\CanDoItAll.SharedProviders.TestUpstream.csproj --urls http://127.0.0.1:5180
```

Build the standalone image with the project directory as its context:

```powershell
docker build --file .\tests\Support\CanDoItAll.SharedProviders.TestUpstream\Dockerfile --tag candoitall-shared-providers-test-upstream:dev .\tests\Support\CanDoItAll.SharedProviders.TestUpstream
```

The runtime image includes `/usr/local/bin/busybox`, so Compose can use `/usr/local/bin/busybox wget --spider --quiet http://127.0.0.1:8080/health` without installing tools at startup.

## API

- `GET /health`
- `GET /v1/models`
- `POST /v1/chat/completions`
- `POST /v1/responses`
- `POST /v1/images/generations`
- `GET /system_stats`
- `POST /prompt`
- `GET /history/{promptId}`
- `GET /view?filename=...&subfolder=...&type=output`

Chat Completions and Responses support buffered and fixed-delay multi-chunk SSE output. A function tool definition produces a deterministic tool call. Structured-output requests produce `{"result":"fixture","value":42}`. Image generation accepts `b64_json` with `png`, `jpeg`, or `webp` output.

## Test control and captures

All `/v1` requests require the data Bearer token. The explicit ComfyUI data routes are anonymous
because the production driver has no credential contract; they remain reachable only on the
isolated data network. Every `/_test` request requires the distinct control token. The data token
cannot read captures or mutate fixture behavior, and the control token cannot invoke OpenAI data
routes. `PUT /_test/control` accepts string-enum JSON such as:

```json
{
  "failure_mode": "rate_limited",
  "surface": "chat_completions"
}
```

Supported failure modes are `none`, `bad_request`, `unauthorized`, `rate_limited`, `internal_server_error`, and `timeout`. Surfaces can target any public fixture route or `all`. `GET /_test/control` reads the active control and `DELETE /_test/control` resets it.

`GET /_test/captures` returns at most 128 in-memory request captures with bodies truncated to 64 KiB. It records header names and a small safe-value allowlist; Authorization and Cookie values are never stored or logged. `DELETE /_test/captures` resets the capture buffer. Cancellation observed during streaming or timeout behavior is recorded on the corresponding capture.
