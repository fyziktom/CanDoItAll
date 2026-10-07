# Controlled TestLab production browser host

Test-only executable using WebApplicationFactory with real Kestrel and the actual Web
entry point, routes, shell, DI and PostgreSQL owners. The browser test provides an isolated
profile through the existing fixture environment contract and an owned loopback port.
Standard input commands arm a held postcommit read-back, a one-shot Activity failure, or
release/shut down the host. There are no HTTP control endpoints or production fault flags.

The fixture wraps actual owner persistence with the existing OwnerPostcommitTestProbe.
It releases pending work during shutdown. TestLabBrowserHost owns the process, captures
output, and disposes its databases and workspace. The Playwright test project builds this
helper transitively; it does not belong in the product solution.
