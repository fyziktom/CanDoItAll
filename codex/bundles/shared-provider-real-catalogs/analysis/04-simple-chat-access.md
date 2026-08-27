# Normal local browser access

N005 was reproduced in Playwright MCP without a browser token. The prior runtime test
explicitly issued/injected a scoped JWT, so it missed the normal desktop path.

The local operator identity required both `ResolvedRuntimeHostProfile.IsInteractive`
and original/effective loopback transport. Docker uses a headless secret-vault/runtime
profile; a host browser reaches its loopback-published port through gateway 172.31.0.1
(observed as IPv4-mapped IPv6). Both conditions denied the UI's exact chat scopes.

Repair: the Web circuit identity no longer depends on OS desktop integration capabilities.
An empty-by-default, startup-validated exact ingress list permits this verified local NAT
path. The original and effective addresses must both be trusted. Host/forwarded headers
alone cannot grant access. The original framework principal and HttpContext.User remain
unchanged; existing authenticated users keep their actual scopes. Scoped file access uses
the circuit identity after HttpContext is gone, as before.

The explicit gateway option is safe only with an exclusively trusted local ingress.
Deployment checks loopback bindings before setting it. No public gateway/private-subnet
auto-trust, API bypass, OS-profile deception or new authorization layer was introduced.

Framework lifecycle and forwarding guidance were checked against Microsoft's primary docs:
[Blazor HttpContext](https://learn.microsoft.com/en-us/aspnet/core/blazor/components/httpcontext?view=aspnetcore-10.0)
and [proxy headers](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/proxy-load-balancer?view=aspnetcore-10.0).
The implementation captures transport trust when the host initializes the circuit; later
access uses scoped state, not retained-request identity. This is a project-specific trust
policy, not a claim that Microsoft recommends anonymous remote browser access.
