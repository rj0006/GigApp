#!/usr/bin/env bash
set -euo pipefail

DOMAIN="${1:-}"
APP_USER=gigapp
WWW=/var/www/gigapp
DATA=/var/lib/gigapp
ENV_FILE="$DATA/gigapp.env"
CONF_FILE="$DATA/deploy.conf"
SITE_FILE=/etc/caddy/sites/gigapp.caddy
DEPLOY_BIN=/usr/local/bin/gigapp-deploy
PG_VERSION=18
EF_VERSION=8.0.10

if [ "$(id -u)" -ne 0 ]; then
  echo "Run this as root: sudo bash $0 $DOMAIN" >&2
  exit 1
fi

if [ -z "$DOMAIN" ] && [ -f "$SITE_FILE" ]; then
  DOMAIN="$(awk 'NR == 1 { print $1 }' "$SITE_FILE")"
fi

if [ -z "$DOMAIN" ]; then
  echo "Usage: sudo bash $0 <domain>" >&2
  echo "The domain's DNS A record must already point at this server, for example gigapp.example.com." >&2
  exit 1
fi

saved_repo=""
saved_branch=""
if [ -f "$CONF_FILE" ]; then
  saved_repo="$(sed -n 's/^GIGAPP_REPO_URL=//p' "$CONF_FILE")"
  saved_branch="$(sed -n 's/^GIGAPP_BRANCH=//p' "$CONF_FILE")"
fi
REPO_URL="${GIGAPP_REPO_URL:-${saved_repo:-https://github.com/rj0006/GigApp.git}}"
BRANCH="${GIGAPP_BRANCH:-${saved_branch:-main}}"

. /etc/os-release
if [ "${VERSION_ID:-}" != "24.04" ]; then
  echo "Warning: written for Ubuntu 24.04; this is ${PRETTY_NAME:-unknown}." >&2
fi

export DEBIAN_FRONTEND=noninteractive

echo "==> Installing the .NET 8 SDK, git, PostgreSQL $PG_VERSION with PostGIS, and Caddy"
apt-get update -q
apt-get install -y -q curl ca-certificates gnupg openssl git ufw dotnet-sdk-8.0

if [ ! -f /etc/apt/sources.list.d/pgdg.list ]; then
  install -d /usr/share/postgresql-common/pgdg
  curl -fsSL -o /usr/share/postgresql-common/pgdg/apt.postgresql.org.asc \
    https://www.postgresql.org/media/keys/ACCC4CF8.asc
  echo "deb [signed-by=/usr/share/postgresql-common/pgdg/apt.postgresql.org.asc] https://apt.postgresql.org/pub/repos/apt ${VERSION_CODENAME}-pgdg main" \
    > /etc/apt/sources.list.d/pgdg.list
  apt-get update -q
fi
apt-get install -y -q "postgresql-$PG_VERSION" "postgresql-$PG_VERSION-postgis-3"

if ! command -v caddy >/dev/null 2>&1; then
  curl -1sLf https://dl.cloudsmith.io/public/caddy/stable/gpg.key \
    | gpg --batch --yes --dearmor -o /usr/share/keyrings/caddy-stable-archive-keyring.gpg
  curl -1sLf https://dl.cloudsmith.io/public/caddy/stable/debian.deb.txt \
    > /etc/apt/sources.list.d/caddy-stable.list
  apt-get update -q
  apt-get install -y -q caddy
fi

echo "==> Swap file for builds"
add_swap_file() {
  [ -z "$(swapon --show --noheadings)" ] || return 0
  fallocate -l 2G /swapfile && chmod 600 /swapfile && mkswap /swapfile >/dev/null && swapon /swapfile || return 1
  grep -q '^/swapfile ' /etc/fstab || echo '/swapfile none swap sw 0 0' >> /etc/fstab
}
add_swap_file || echo "Warning: could not add a swap file, continuing without it." >&2

echo "==> Creating the $APP_USER user and folders"
id "$APP_USER" >/dev/null 2>&1 || useradd --create-home --shell /bin/bash "$APP_USER"
install -d -o "$APP_USER" -g "$APP_USER" -m 755 "$WWW" "$WWW/releases"
install -d -o "$APP_USER" -g "$APP_USER" -m 750 "$DATA" "$DATA/uploads" "$DATA/App_Data" "$DATA/tools"

if [ ! -f "$ENV_FILE" ]; then
  echo "==> Writing $ENV_FILE"
  install -o "$APP_USER" -g "$APP_USER" -m 600 /dev/null "$ENV_FILE"
  cat > "$ENV_FILE" <<EOF
ASPNETCORE_ENVIRONMENT=Development
ASPNETCORE_URLS=http://127.0.0.1:5000
ASPNETCORE_FORWARDEDHEADERS_ENABLED=true
DOTNET_gcServer=0
ConnectionStrings__DefaultConnection="Host=localhost;Port=5432;Database=gigapp;Username=gigapp;Password=$(openssl rand -hex 16)"
Jwt__Key=$(openssl rand -hex 32)
EOF
else
  echo "==> Keeping the existing $ENV_FILE"
fi

DB_PASSWORD="$(sed -nE 's/^ConnectionStrings__DefaultConnection=.*Password=([^;"]*).*/\1/p' "$ENV_FILE")"
if [ -z "$DB_PASSWORD" ]; then
  echo "No database password found in $ENV_FILE" >&2
  exit 1
fi

echo "==> Creating the database"
psql_admin() { (cd / && sudo -u postgres psql -v ON_ERROR_STOP=1 -qtA "$@"); }
if [ -z "$(psql_admin -c "SELECT 1 FROM pg_roles WHERE rolname = 'gigapp'")" ]; then
  psql_admin -c "CREATE ROLE gigapp LOGIN PASSWORD '$DB_PASSWORD'"
else
  psql_admin -c "ALTER ROLE gigapp WITH LOGIN PASSWORD '$DB_PASSWORD'"
fi
if [ -z "$(psql_admin -c "SELECT 1 FROM pg_database WHERE datname = 'gigapp'")" ]; then
  psql_admin -c "CREATE DATABASE gigapp OWNER gigapp"
fi
psql_admin -d gigapp -c "CREATE EXTENSION IF NOT EXISTS postgis"

echo "==> gigapp service"
cat > /etc/systemd/system/gigapp.service <<EOF
[Unit]
Description=GigApp
After=network.target postgresql.service
Wants=postgresql.service
ConditionPathExists=$WWW/current/GigApp.Api.dll

[Service]
User=$APP_USER
Group=$APP_USER
WorkingDirectory=$WWW/current
EnvironmentFile=$ENV_FILE
ExecStart=/usr/bin/dotnet $WWW/current/GigApp.Api.dll
Restart=always
RestartSec=5
KillSignal=SIGINT
TimeoutStopSec=30
SyslogIdentifier=gigapp
MemoryMax=768M

[Install]
WantedBy=multi-user.target
EOF
systemctl daemon-reload
systemctl enable gigapp >/dev/null 2>&1

echo "==> Caddy site for $DOMAIN"
install -d /etc/caddy/sites
if ! grep -qs '^import /etc/caddy/sites/\*\.caddy' /etc/caddy/Caddyfile; then
  cat > /etc/caddy/Caddyfile <<'EOF'
import /etc/caddy/sites/*.caddy
EOF
fi
cat > "$SITE_FILE" <<EOF
$DOMAIN {
	encode zstd gzip
	reverse_proxy 127.0.0.1:5000
}
EOF
caddy validate --config /etc/caddy/Caddyfile --adapter caddyfile >/dev/null
systemctl enable caddy >/dev/null 2>&1
systemctl reload caddy || systemctl restart caddy

echo "==> Letting deploys restart the app and read its log"
cat > /etc/sudoers.d/gigapp <<EOF
$APP_USER ALL=(root) NOPASSWD: /usr/bin/systemctl restart gigapp, /usr/bin/journalctl -u gigapp -n 60 --no-pager
EOF
chmod 440 /etc/sudoers.d/gigapp
visudo -cf /etc/sudoers.d/gigapp >/dev/null

echo "==> Installing the EF Core migration tool"
if [ ! -x "$DATA/tools/dotnet-ef" ]; then
  (cd / && sudo -u "$APP_USER" -H env DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1 DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 \
    dotnet tool install dotnet-ef --version "$EF_VERSION" --tool-path "$DATA/tools")
fi

echo "==> Deploy script and timer"
install -o "$APP_USER" -g "$APP_USER" -m 640 /dev/null "$CONF_FILE"
cat > "$CONF_FILE" <<EOF
GIGAPP_REPO_URL=$REPO_URL
GIGAPP_BRANCH=$BRANCH
EOF

cat > "$DEPLOY_BIN" <<'DEPLOY_SCRIPT'
#!/usr/bin/env bash
set -euo pipefail

export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1 DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 MSBUILDDISABLENODEREUSE=1

base=@WWW@
shared=@DATA@
src="$shared/src"
state="$shared/deploy-state"
project=GigApp.Api/GigApp.Api/GigApp.Api.csproj

. "$shared/deploy.conf"
repo="$GIGAPP_REPO_URL"
branch="$GIGAPP_BRANCH"

force=0
if [ "${1:-}" = "--force" ]; then
  force=1
fi

touch "$state"
exec 9>"$shared/deploy.lock"
if ! flock -n 9; then
  echo "Another deploy is running."
  exit 0
fi

state_get() { grep "^$1=" "$state" | cut -d= -f2- || true; }
state_set() {
  { grep -v "^$1=" "$state" || true; echo "$1=$2"; } > "$state.new"
  mv "$state.new" "$state"
}

sha=""
release=""
deploying=0

on_exit() {
  if [ "$deploying" != 1 ]; then
    return 0
  fi
  dotnet build-server shutdown >/dev/null 2>&1 || true
  if [ -n "$sha" ]; then
    state_set failed "$sha"
  fi
  if [ -n "$release" ] && [ "$(readlink -e "$release" || true)" != "$(readlink -e "$base/current" || true)" ]; then
    rm -rf "$release"
  fi
}
trap on_exit EXIT

[ -d "$src/.git" ] || git init -q -b main "$src"
cd "$src"
git remote remove origin 2>/dev/null || true
git remote add origin "$repo"

remote_sha="$(git ls-remote origin "refs/heads/$branch" | cut -f1)"
if [ -z "$remote_sha" ]; then
  echo "Branch $branch was not found at $repo" >&2
  exit 1
fi

deployed="$(state_get deployed)"
failed="$(state_get failed)"
if [ "$force" = 0 ] && { [ "$remote_sha" = "$deployed" ] || [ "$remote_sha" = "$failed" ]; }; then
  exit 0
fi

git fetch -q --depth 1 origin "refs/heads/$branch"
sha="$(git rev-parse FETCH_HEAD)"

if [ "$force" = 0 ] && [ -n "$deployed" ] && git cat-file -e "$deployed^{commit}" 2>/dev/null \
  && git diff --quiet "$deployed" "$sha" -- GigApp.Api; then
  echo "Nothing changed under GigApp.Api in $sha, so there is nothing to build."
  state_set deployed "$sha"
  exit 0
fi

deploying=1
git checkout -q --force --detach "$sha"
git clean -fdq
release="$base/releases/$(date -u +%Y%m%d%H%M%S)-${sha:0:8}"

echo "==> Building $sha"
dotnet publish "$project" --configuration Release --output "$release" --disable-build-servers --nologo -v minimal

DOTNET_ROOT="$(dirname "$(readlink -f "$(command -v dotnet)")")"
export DOTNET_ROOT
set -a
. "$shared/gigapp.env"
set +a

echo "==> Migrating the database"
"$shared/tools/dotnet-ef" database update --project "$project" --configuration Release \
  --connection "$ConnectionStrings__DefaultConnection" --no-color

echo "==> Linking uploads and private files"
mkdir -p "$release/wwwroot"
rm -rf "$release/wwwroot/uploads" "$release/App_Data"
ln -s "$shared/uploads" "$release/wwwroot/uploads"
ln -s "$shared/App_Data" "$release/App_Data"

previous="$(readlink -e "$base/current" || true)"

switch_to() {
  ln -sfn "$1" "$base/current.next"
  mv -Tf "$base/current.next" "$base/current"
  sudo -n /usr/bin/systemctl restart gigapp
}

health_url="${ASPNETCORE_URLS%%;*}/"
is_healthy() {
  for _ in $(seq 1 30); do
    if curl -fsS -o /dev/null --max-time 5 -H 'X-Forwarded-Proto: https' "$health_url"; then
      return 0
    fi
    sleep 2
  done
  return 1
}

echo "==> Switching to $(basename "$release")"
switch_to "$release"

if ! is_healthy; then
  echo "The new build failed its health check. Latest log lines:" >&2
  sudo -n /usr/bin/journalctl -u gigapp -n 60 --no-pager >&2 || true
  if [ -n "$previous" ] && [ "$previous" != "$release" ]; then
    echo "Putting $(basename "$previous") back." >&2
    switch_to "$previous"
  fi
  exit 1
fi

deploying=0
state_set deployed "$sha"
git update-ref refs/gigapp/deployed "$sha"
dotnet build-server shutdown >/dev/null 2>&1 || true
echo "==> Live: $sha"

live="$(readlink -e "$base/current")"
ls -1dt "$base"/releases/*/ | tail -n +6 | while read -r dir; do
  if [ "$(readlink -e "$dir")" != "$live" ]; then
    rm -rf "$dir"
  fi
done
DEPLOY_SCRIPT
sed -i -e "s|@WWW@|$WWW|g" -e "s|@DATA@|$DATA|g" "$DEPLOY_BIN"
chmod 755 "$DEPLOY_BIN"

cat > /etc/systemd/system/gigapp-deploy.service <<EOF
[Unit]
Description=GigApp test server deploy
After=network-online.target postgresql.service
Wants=network-online.target

[Service]
Type=oneshot
User=$APP_USER
Group=$APP_USER
ExecStart=$DEPLOY_BIN
Nice=10
CPUWeight=20
IOSchedulingClass=idle
MemoryMax=2G
TimeoutStartSec=30min
EOF

cat > /etc/systemd/system/gigapp-deploy.timer <<EOF
[Unit]
Description=Check GitHub for a new GigApp build every minute

[Timer]
OnActiveSec=15s
OnUnitInactiveSec=1min
AccuracySec=5s

[Install]
WantedBy=timers.target
EOF
systemctl daemon-reload
systemctl enable --now gigapp-deploy.timer >/dev/null 2>&1

echo "==> Firewall"
# sshd -T fails until the first SSH login creates /run/sshd, so fall back to 22.
SSH_PORT="$(sshd -T 2>/dev/null | awk '$1 == "port" { print $2 }' || true)"
SSH_PORT="${SSH_PORT%%$'\n'*}"
SSH_PORT="${SSH_PORT:-22}"
ufw allow "$SSH_PORT/tcp" >/dev/null
ufw allow 80/tcp >/dev/null
ufw allow 443/tcp >/dev/null
ufw --force enable >/dev/null

cat <<EOF

================================================================================
 GigApp test server is set up.

 Site:    https://$DOMAIN
 Builds:  branch $BRANCH of $REPO_URL, checked every minute.

 The first build starts within a minute and takes several minutes. Watch it with:
   journalctl -u gigapp-deploy -f

 The first deploy creates the test accounts listed under "Dev accounts" in
 CLAUDE.md. Their password is public, so change the admin password after
 signing in.
================================================================================
EOF
