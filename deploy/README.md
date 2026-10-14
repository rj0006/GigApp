# Test server deploy

The test server builds GigApp by itself. Every minute it checks one branch of the source repository. When there is a new commit that changes `GigApp.Api/`, it builds the API, updates the database, switches to the new build and checks that it answers. There are no GitHub Actions and no secrets.

## One-time setup

1. **DNS.** Point an A record for the test domain, for example `gigapp.factualtechnologies.tech`, at the server's IP address.
2. **Server.** On a fresh Ubuntu 24.04 server, as root:

   ```bash
   curl -fsSL https://raw.githubusercontent.com/rj0006/GigApp/main/deploy/server-setup.sh -o gigapp-setup.sh
   bash gigapp-setup.sh gigapp.factualtechnologies.tech
   ```

3. **First build.** It starts within a minute and takes several minutes, because the packages are downloaded once. Watch it with `journalctl -u gigapp-deploy -f`.

The setup installs the .NET 8 SDK, git, PostgreSQL 18 with PostGIS, and Caddy if it is missing. It adds a 2 GB swap file if the server has none, so a build cannot starve other sites of memory. It then creates the `gigapp` user, database, service, Caddy site and the deploy timer. Other sites on the server keep working, because the script only adds its own file under `/etc/caddy/sites/`.

Running it again is safe. The database password, JWT key, domain, repository and branch are all kept. To change the source, set them on the command line:

```bash
GIGAPP_REPO_URL=https://github.com/rj0006/GigApp.git GIGAPP_BRANCH=main bash gigapp-setup.sh
```

## First admin

A new database has no accounts. The seeder fills in categories, services, plans, taxes, the admin menu and settings, but it creates no user, and the app has no screen to create an admin. Create the first one on the server, once.

First choose a password of letters and numbers and make its hash. The space at the start keeps the password out of the shell history:

```bash
 PW='replace-with-your-password'
HASH="$(python3 -W ignore -c 'import crypt,sys; print(crypt.crypt(sys.argv[1], crypt.mksalt(crypt.METHOD_BLOWFISH)))' "$PW")"
```

Then add the admin. Use a 10-digit phone number that you will sign in with:

```bash
cd / && sudo -u postgres psql -d gigapp -v name='Admin' -v phone='9XXXXXXXXX' -v hash="$HASH" <<'SQL'
INSERT INTO "Users" ("Name", "Phone", "PasswordHash", "Role", "IsActive", "IsPhoneVerified", "PhoneVerifiedAt", "RatingCount", "CreatedAt")
VALUES (:'name', :'phone', :'hash', 'superadmin', true, true, now(), 0, now());
SQL
```

Sign in at `/admin/login` with that phone number and password. Customers and partners register themselves from their portals, and in Development mode the OTP code appears on the screen. Approve new partners under Approvals.

## What a deploy does

1. Reads the latest commit of the branch. If it is already deployed, or failed earlier, nothing happens.
2. If nothing changed under `GigApp.Api/`, the commit is recorded and no build runs.
3. Builds with `dotnet publish` into `/var/www/gigapp/releases/<time>-<commit>`.
4. Applies the database migrations with `dotnet ef database update`. A build that cannot migrate never goes live.
5. Links the shared uploads and KYC folders, switches `/var/www/gigapp/current`, restarts the service and waits for it to answer.
6. If it does not answer, the previous build is put back. Migrations are not undone.

A failed commit is not retried until a newer commit arrives. The last five builds are kept.

## For developers

The server follows the `main` branch of this repository. A commit that changes `GigApp.Api/` is built and deployed within a few minutes. A commit that changes only other folders, such as the Flutter apps or the docs, is skipped.

A broken commit does not take the test server down. If the build or the migration fails, the old build keeps running. If the new build does not answer, the old one is put back.

To keep auto-deploy working:

1. Commit every database change as an EF migration. The server applies migrations, but the app does not migrate itself.
2. Keep the project at `GigApp.Api/GigApp.Api/GigApp.Api.csproj`.
3. If the .NET or EF version changes, tell whoever runs the server. It has the .NET 8 SDK and EF tools 8.0.10 today.
4. If a new setting or key becomes required, add it to `/var/lib/gigapp/gigapp.env` on the server first. The app stops at start when `Jwt:Key` is missing.
5. Keep the repository public, or give the server a read token.

## Everyday commands

Run these on the server.

| What | Command |
|---|---|
| Watch the deploys | `journalctl -u gigapp-deploy -f` |
| Watch the app | `journalctl -u gigapp -f` |
| See what is deployed or failed | `cat /var/lib/gigapp/deploy-state` |
| Check now instead of waiting | `systemctl start gigapp-deploy` |
| Retry the latest commit | `sudo -u gigapp gigapp-deploy --force` |
| Pause auto-deploy | `systemctl stop gigapp-deploy.timer` |

## Where things live

| Path | What |
|---|---|
| `/var/www/gigapp/current` | The live build |
| `/var/lib/gigapp/gigapp.env` | Settings and secrets: connection string, JWT key |
| `/var/lib/gigapp/deploy.conf` | Source repository and branch |
| `/var/lib/gigapp/src` | Checkout used for building |
| `/var/lib/gigapp/uploads` | Public uploads, linked as `wwwroot/uploads` |
| `/var/lib/gigapp/App_Data` | KYC documents, linked as `App_Data` |
| `/home/gigapp/.aspnet/DataProtection-Keys` | Keys behind the session and form tokens; losing them signs everyone out |
| `/usr/local/bin/gigapp-deploy` | The deploy script, written by the setup script |
| `/etc/caddy/sites/gigapp.caddy` | The site, proxied to port 5000 |

To change the deploy script, edit `deploy/server-setup.sh` and run it again on the server.

## Test server settings

- **Development mode.** The seeder fills categories, services, plans, taxes and settings on every start. It does not create accounts, so follow "First admin" above. Swagger is on at `/swagger`, and OTP codes come back in the response, so anyone who knows a customer or partner phone number can sign in to that account. Keep only test data here.
- **Real visitor IPs.** `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true` makes the app read the visitor's IP from Caddy. Without it every request comes from `127.0.0.1`, and the login and OTP rate limits would block all users together.
- **Memory.** The app runs with workstation GC and a 768 MB limit.

## Limits

- A build uses up to about 1 GB of memory and the single CPU for a few minutes. It runs at low priority and is capped at 2 GB, so the app and other sites stay responsive.
- The source repository must be public, because the server fetches it without credentials.
