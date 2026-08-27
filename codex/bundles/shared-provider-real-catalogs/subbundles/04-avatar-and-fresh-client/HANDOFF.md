# Three-instance manual testing

All three apps run image `candoitall-shared-providers-ui:avatar-blank-client-20260827-2`.
They are published on loopback only. On first visit, confirm the selected database with
**Continue**. Ordinary local UI access does not require a browser JWT.

| Instance | Browser address | State |
| --- | --- | --- |
| Shared/source | http://localhost:5210/agents?tab=providers | Existing real OpenAI/Ollama publications preserved |
| Existing client | http://localhost:5212/agents?tab=simple-chats | Existing imports and history preserved |
| Fresh manual client | http://localhost:5214/agents?tab=providers | Zero providers, sources, imports and secrets |

## Connect the fresh client through the UI

1. On the source, open http://localhost:5210/settings?tab=api-access.
   Create a token with a descriptive Subject and Display name (for example
   `manual-desktop-client`), an appropriate Lifetime minutes, and exactly these scopes:
   `api.shared-providers.catalog.read api.shared-providers.invoke`.
   Copy the generated JWT privately; do not paste it into logs or bundle evidence.
2. On the fresh client, open http://localhost:5214/settings?tab=secrets.
   Choose **New secret**, name it **Shared instance JWT**, set Kind to **ApiKey**,
   Scope to `workspace`, paste the source JWT into Value, and **Save secret**.
3. Open http://localhost:5214/agents?tab=providers, then **Sharing → Add source**.
   No local provider needs to be saved first. Enter:

   - Source name: `Shared instance` (or another descriptive name).
   - CanDoItAll base URL: `http://candoitall-spui-shared:8080/`.
   - Source credential secret: **Shared instance JWT**.
   - Enabled: checked.
   - **Allow HTTP on a private network**: checked for this isolated Docker test.

4. **Save source → Test → Discover and import**. Select **UI Shared OpenAI Chat**,
   **UI Shared OpenAI Image**, and **UI Shared Ollama**, then apply the selection.
   Use **Sync selected** after changing the published source catalog.
5. Create your own Simple Chat at http://localhost:5214/agents?tab=simple-chats.
   Select an imported provider and a supported real model in Runtime. An OpenAI
   catalog ID is not necessarily a chat model; model capabilities still apply.

The source URL above is Docker DNS, not a browser URL. Do not use `localhost:5210`
inside the client configuration: container-local localhost points to the client itself.
The instance root is required; the app adds `/api/shared-providers/v1/catalog` and
relay paths. Upstream OpenAI credentials remain only on the source.
JWTs expire: renew on the source and replace the client secret value. The existing
5212 test token also has a finite lifetime; current successful connection is not a
permanent credential guarantee. No token or database password is included here.

## Lifecycle and validation

The fresh container is `candoitall-spui-fresh-app-1`, database/role
`candoitall_e2e_fresh_client`, and app volume `candoitall-spui-fresh_app-data`.
It uses its own API signing key and database password. The fresh role cannot connect
to source/client databases. No provider data was copied from another instance.
`AgentFramework__Providers__SeedDefaults=false` keeps it provider-free across restarts;
explicit providers you later save/import are preserved. Standard technical-agent
templates remain available but cannot execute until you configure a provider.

From this directory: `docker compose up -d --wait` starts it;
`docker compose stop` stops it without deleting data. Keep the shared PostgreSQL
container and the existing test networks available. See CONTAINERS.md for ownership.
The app restarts automatically with Docker unless explicitly stopped.

Both existing containers retain rollback copies ending `before-avatar-count-20260827`.
No existing data volume or history was removed. Port 5032 was not rebuilt or stopped.
Avatar regression creates identifiable `Avatar verification ...` draft definitions only
on 5210/5212. No test definition, provider, import or source credential was saved on 5214.

Health: `/health` returns `200 Healthy` on all three. The fresh client's anonymous
Docker-DNS request to the source catalog returns 401 as expected; the existing client's
UI **Test** succeeds with its stored JWT. New source import is deliberately left for you.
