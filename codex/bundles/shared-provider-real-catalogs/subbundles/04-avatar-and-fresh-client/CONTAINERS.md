# Local manual-setup client

This task-specific Compose app joins the existing development test pair's external
networks. Those networks and PostgreSQL container are independently managed; normal
`docker compose down` here must not remove them or any volumes. Port 5214 is loopback-only.
The inspected local ingress is 172.31.0.1; revalidate explicit UI trust if networks change.

The external `candoitall-spui-fresh-credentials` volume holds only a new database-role
password and this client's independent API signing key. It contains no source credentials,
upstream provider keys or imported-source token. Database `candoitall_e2e_fresh_client`
has its own owner with no access to source/client databases. `app-data` is new and owned
by this Compose project. DataProtectionFile/UnprotectedDevelopment is an existing local
test convention, not a production secret-protection recommendation. Restart unless-stopped
is deliberate for this user testing instance so it returns after a computer restart.

From this directory, `docker compose up -d --wait` starts/reuses the already provisioned
client; `docker compose stop` preserves all data. No volume-removal/reset command is part
of the handoff. Provisioning is one-time; no source or existing client data was copied.

App health checks readiness. Database is external, so startup uses the existing bounded
database-readiness retry instead of a same-Compose depends_on relationship.
