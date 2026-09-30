# Test server deploy

Every push to `main` that changes `GigApp.Api/`, `deploy/` or the workflow builds the API on GitHub
and puts it live on the test server. A pull request runs the same build as a check, without deploying.

## One-time setup

1. **DNS.** Point an A record for the test domain, for example `gigapp.factualtechnologies.tech`, at
   the server's IP address.
2. **Server.** On Ubuntu 24.04, as root:

   ```bash
   curl -fsSL https://raw.githubusercontent.com/rj0006/GigApp/main/deploy/server-setup.sh -o gigapp-setup.sh
   sudo bash gigapp-setup.sh gigapp.factualtechnologies.tech
   ```

   It installs .NET 8, PostgreSQL 18 with PostGIS, and Caddy if it is missing. It then creates the
   `gigapp` user, database, service and site, and prints three secrets. Another site already on the
   server keeps working: the script only adds its own file under `/etc/caddy/sites/`.
3. **GitHub.** Under Settings > Secrets and variables > Actions, add `GIGAPP_TEST_HOST`,
   `GIGAPP_TEST_KNOWN_HOSTS` and `GIGAPP_TEST_SSH_KEY` exactly as printed.
4. **First deploy.** Actions > "Deploy to test server" > Run workflow, or push to `main`.

Running the script again is safe. The database password, JWT key, deploy key and domain are all
kept. `NEW_DEPLOY_KEY=1` issues a new deploy key, after which `GIGAPP_TEST_SSH_KEY` must be updated.

## What a deploy does

1. GitHub builds the app with `dotnet publish` for linux-x64, plus an EF Core migrations bundle.
2. The build is copied to `/var/www/gigapp/releases/<commit>`.
3. `deploy/activate.sh` runs on the server as `gigapp`. It links the shared uploads and KYC folders,
   runs the migrations and switches `/var/www/gigapp/current`. It then restarts the service and waits
   for `http://127.0.0.1:5000/` to answer.
4. If the new build does not answer within a minute, the previous build is put back and the run
   fails. Migrations are not undone. The last five builds are kept.

## Where things live

| Path | What |
|---|---|
| `/var/www/gigapp/current` | The live build |
| `/var/lib/gigapp/gigapp.env` | Settings and secrets: connection string, JWT key |
| `/var/lib/gigapp/uploads` | Public uploads, linked as `wwwroot/uploads` |
| `/var/lib/gigapp/App_Data` | KYC documents, linked as `App_Data` |
| `/home/gigapp/.aspnet/DataProtection-Keys` | Keys behind the session and form tokens; losing them signs everyone out |
| `/etc/caddy/sites/gigapp.caddy` | The site, proxied to port 5000 |

Logs: `journalctl -u gigapp -f`

## Test server settings

- **Development mode.** The seeder creates the dev accounts from `CLAUDE.md`, Swagger is on at
  `/swagger`, and OTP codes come back in the response. Anyone can therefore sign in to any customer
  or partner account by its number, so keep only test data here. Change the admin password after the
  first deploy, because the seeded one is public in this repository.
- **Real visitor IPs.** `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true` makes the app read the visitor's
  IP from Caddy. Without it every request comes from `127.0.0.1`, and the login and OTP rate limits
  would block all users together.
- **Memory.** `DOTNET_gcServer=0` and `MemoryMax=768M` keep the app small on a shared server.
