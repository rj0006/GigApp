# Deployment

Live site: https://gigapp.factualtechnologies.tech/ on a Linux VPS behind a reverse proxy.

The live site is updated by the owner's own publish routine, not by anything in this repository. Two
GitHub Actions workflows (`deploy-backend.yml` and `build-apps.yml`) were added and then removed on
2026-10-10, because the GitHub account was locked for billing and no run could start. They are still in
git history at commit `a3e837d` if they are ever wanted back. Sections below marked "workflow route"
only matter in that case.

## Local and live use different settings

Nothing in the code branches on the environment name. The same build reads its settings in this order,
and the later source wins: `appsettings.json` → `appsettings.{Environment}.json` → environment variables.

| Setting | Local | Live |
|---|---|---|
| Environment | `Development` | `Production` (`ASPNETCORE_ENVIRONMENT`) |
| Database | `appsettings.json` (local Postgres) | `ConnectionStrings__DefaultConnection` in the server env file |
| JWT signing key | `appsettings.Development.json` | `Jwt__Key` in the server env file |
| Run migrations at startup | off | `Database__MigrateOnStartup=true` |
| `DbSeeder`, Swagger | on | off (Development only) |
| Flutter API URL | `http://localhost:5245` (default in `lib/core/config/env.dart`) | `--dart-define=API_BASE_URL=...` in `build-apps.yml` |

Production secrets live only in the server env file and in GitHub secrets. Never put them in a file
that is committed. `appsettings.Production.json` is git-ignored and is excluded from the rsync, so a
deploy can neither overwrite nor delete it.

## Server setup

Done once, by hand.

1. A service user and a deploy directory, for example `/var/www/gigapp`, owned by the deploy user.
2. A root-only env file, for example `/etc/gigapp/gigapp.env` (`chmod 600`):

   ```
   ASPNETCORE_ENVIRONMENT=Production
   ConnectionStrings__DefaultConnection=Host=localhost;Port=5432;Database=gigapp;Username=gigapp;Password=<server password>
   Jwt__Key=<long random string, at least 32 characters>
   Database__MigrateOnStartup=true
   ```

3. A systemd unit that runs `dotnet GigApp.Api.dll` from the deploy directory with
   `EnvironmentFile=/etc/gigapp/gigapp.env` and `Restart=always`.
4. A sudoers rule so the deploy user may restart only that service, without a password:
   `deploy ALL=(root) NOPASSWD: /bin/systemctl restart gigapp`.
5. nginx in front of Kestrel, passing `X-Forwarded-For` and `X-Forwarded-Proto`, with WebSocket upgrade
   headers for `/hubs/` (SignalR). The app already calls `UseForwardedHeaders` for the loopback proxy.
6. `rsync` installed on the server.
7. A dedicated SSH key pair for GitHub Actions. The public key goes in the deploy user's
   `authorized_keys`. The private key goes in the `DEPLOY_SSH_KEY` secret.

Rotate the database password if it has ever been pasted into a chat, an email or a ticket:

```sql
ALTER USER gigapp PASSWORD '<new password>';
```

Then update the env file and restart the service.

## GitHub secrets (workflow route)

Repository → Settings → Secrets and variables → Actions.

| Secret | Value |
|---|---|
| `DEPLOY_HOST` | Server hostname or IP |
| `DEPLOY_USER` | Deploy user |
| `DEPLOY_PATH` | Deploy directory, for example `/var/www/gigapp` |
| `DEPLOY_PORT` | SSH port. Optional, defaults to 22 |
| `DEPLOY_SERVICE` | systemd service name, for example `gigapp` |
| `DEPLOY_SSH_KEY` | Private key of the Actions key pair |
| `DEPLOY_KNOWN_HOSTS` | Output of `ssh-keyscan -p <port> <host>` |
| `ANDROID_KEYSTORE_BASE64` | The release keystore, base64 encoded |
| `ANDROID_KEYSTORE_PASSWORD` | Keystore password |
| `ANDROID_KEY_PASSWORD` | Key password |
| `ANDROID_KEY_ALIAS` | Key alias |

## Contact details and social links

The storefront footer reads the `Company` section. Add the keys you need to the server env file, or put
the values in `appsettings.json`, and restart the service. Unset keys simply do not appear.

```
Company__Address=...
Company__HelplineNumber=...
Company__HelplineHours=...
Company__FacebookUrl=https://...
Company__InstagramUrl=https://...
Company__TwitterUrl=https://...
Company__LinkedInUrl=https://...
Company__YouTubeUrl=https://...
```

## Testing before the real launch

While the apps are only being tested, none of the launch items are needed: the package names
(`com.example.*`), the app labels, a company name or logo, an SMS provider, Firebase and a signing key
can all wait.

- **Test builds are signed with a debug key.** A build made on another machine has a different
  signature, so it must be installed after uninstalling the old one.
- **Install from the website:** open the footer link on the phone, download the APK and allow
  "install unknown apps" when Android asks. Play Protect may show a warning for a test app.
- **Test APKs travel with the code:** `gigapp-customer.apk` and `gigapp-partner.apk` are committed in
  `wwwroot/downloads/`, so an ordinary publish of the API carries them to the server and the footer links
  appear on their own. Each replacement adds about 110 MB to the git history, so replace them rarely.
- **OTP has no SMS provider yet.** On the live site the code is not shown on screen. It is written to the
  service log, so read it with `journalctl -u <service> -f | grep OTP`. The other route is password mode:
  open `/admin/settings/auth`, switch the customer and partner login to password, and register accounts
  with their own passwords.
- **The seeder does not run on the live server**, so there are no demo accounts there. An administrator
  has to exist in the live database before `/admin` can be used.

## Android signing key

Create it once and **back it up outside this machine and outside git**. An app signed with a different
key cannot be installed over the old one, so losing the keystore means every user has to uninstall first.

```bash
keytool -genkeypair -v -keystore gigapp-release.keystore -alias gigapp -keyalg RSA -keysize 2048 -validity 10000
base64 -w0 gigapp-release.keystore
```

Paste the base64 output into `ANDROID_KEYSTORE_BASE64`. Both apps use the same keystore.

For a local release build, create `android/key.properties` in the app (git-ignored) with `storePassword`,
`keyPassword`, `keyAlias` and `storeFile`. Without it the release build falls back to the debug key,
which is fine for testing and wrong for sharing.

```bash
flutter build apk --release --dart-define=API_BASE_URL=https://gigapp.factualtechnologies.tech
```

`--build-number` must go up with every release or Android refuses the update. CI uses the run number.

## First deploy checklist

1. Set `Database__MigrateOnStartup=true` in the server env file **before** the first push, so the
   `AddServiceZones` migration and any later ones are applied on restart.
2. Publish the API the way you normally do.
3. Sign in to `/admin`, open `/admin/masters/servicezones`, create at least one zone and tick the
   categories it serves. The storefront fails closed, so until this is done customers see nothing.
4. Add a "Service zones" link under Masters → Menu. The menu seeder runs in Development only.
5. Open the site footer and confirm both download links appear. They render only when the APK file exists.

## Before the apps are shared publicly

- The apps are named "Gig Customer" and "Gig Partner" for now. When the company name and logo are decided,
  change the labels, the images in `assets/icon/` and `applicationId` from `com.example.customer_app` and
  `com.example.provider_app`. The id cannot change after users have installed the app.
- Users must allow installs from unknown sources, and Play Protect may warn about an app that is not
  from the Play Store.

## Rollback

Publish the previous version of the code again. Database migrations are not reverted by this. Write a new migration to undo schema changes.

## Never overwritten by a deploy

`appsettings.Production.json`, `App_Data/` (KYC documents), `wwwroot/uploads/` and
`wwwroot/downloads/` are excluded from the rsync `--delete`.
