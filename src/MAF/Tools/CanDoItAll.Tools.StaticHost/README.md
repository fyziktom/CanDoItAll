# Static validation host

This executable serves generated static output over an HTTP loopback IP endpoint.
The registered `workspace_static_serve` tool launches it through the workspace's
owned-process lifecycle; agents stop it with `workspace_dotnet_stop`, and execution
termination recovers any remaining lease.

The host accepts a directory, URL and explicit SPA-fallback flag. It allows GET and
HEAD, rejects path traversal and symbolic-link traversal, serves known MIME types
(including WebAssembly), and applies `no-cache, max-age=0, must-revalidate` plus
`X-Content-Type-Options: nosniff`. SPA fallback is limited to extensionless paths.
It has no directory listing, upload, external binding or product-specific behavior.
These headers describe disposable validation infrastructure, not a production
deployment target or an application's service-worker cache implementation.

The executable has only the ASP.NET shared-framework dependency. MAF deploys its
assembly and runtime configuration. Core receives the trusted executable location
through the typed command boundary and has no dependency on this HTTP host.

The process starts in the workspace root and receives the authorized content directory
as an absolute argument. A deeply nested published directory is not used as the process
working directory, which avoids the Windows process-creation directory length limit.

See [the architecture decision](../../../../docs/architecture/workspace-published-output-validation.md)
and `WorkspacePublishedOutputTests` for actual publish, response and cleanup proof.
