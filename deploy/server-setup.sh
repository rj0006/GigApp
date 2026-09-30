#!/usr/bin/env bash
set -euo pipefail

DOMAIN="${1:-}"
APP_USER=gigapp
WWW=/var/www/gigapp
DATA=/var/lib/gigapp
ENV_FILE="$DATA/gigapp.env"
SITE_FILE=/etc/caddy/sites/gigapp.caddy
PG_VERSION=18

if [ -z "$DOMAIN" ] && [ -f "$SITE_FILE" ]; then
  DOMAIN="$(awk 'NR == 1 { print $1 }' "$SITE_FILE")"
fi

if [ "$(id -u)" -ne 0 ]; then
  echo "Run this as root: sudo bash $0 $DOMAIN" >&2
  exit 1
fi

if [ -z "$DOMAIN" ]; then
  echo "Usage: sudo bash $0 <domain>" >&2
  echo "The domain's DNS A record must already point at this server, for example gigapp.example.com." >&2
  exit 1
fi

. /etc/os-release
if [ "${VERSION_ID:-}" != "24.04" ]; then
  echo "Warning: written for Ubuntu 24.04; this is ${PRETTY_NAME:-unknown}." >&2
fi

export DEBIAN_FRONTEND=noninteractive

echo "==> Installing .NET 8, PostgreSQL $PG_VERSION with PostGIS, and Caddy"
apt-get update -q
apt-get install -y -q curl ca-certificates gnupg openssl rsync ufw aspnetcore-runtime-8.0

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

echo "==> Creating the $APP_USER user and folders"
id "$APP_USER" >/dev/null 2>&1 || useradd --create-home --shell /bin/bash "$APP_USER"
install -d -o "$APP_USER" -g "$APP_USER" -m 755 "$WWW" "$WWW/releases"
install -d -o "$APP_USER" -g "$APP_USER" -m 750 "$DATA" "$DATA/uploads" "$DATA/App_Data"

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

echo "==> Deploy key for GitHub Actions"
KEY_DIR="$(mktemp -d)"
trap 'rm -rf "$KEY_DIR"' EXIT
install -d -o "$APP_USER" -g "$APP_USER" -m 700 "/home/$APP_USER/.ssh"
AUTHORIZED_KEYS="/home/$APP_USER/.ssh/authorized_keys"
NEW_KEY=no
if [ "${NEW_DEPLOY_KEY:-0}" = "1" ] || ! grep -qs 'github-actions-gigapp$' "$AUTHORIZED_KEYS"; then
  NEW_KEY=yes
  ssh-keygen -q -t ed25519 -N "" -C "github-actions-gigapp" -f "$KEY_DIR/key"
  {
    grep -vs 'github-actions-gigapp$' "$AUTHORIZED_KEYS" || true
    echo "no-pty,no-port-forwarding,no-agent-forwarding,no-X11-forwarding $(cat "$KEY_DIR/key.pub")"
  } > "$KEY_DIR/authorized_keys"
  install -o "$APP_USER" -g "$APP_USER" -m 600 "$KEY_DIR/authorized_keys" "$AUTHORIZED_KEYS"
fi

if [ -f "$WWW/current/GigApp.Api.dll" ]; then
  echo "==> Restarting the live build with the current settings"
  systemctl restart gigapp
fi

echo "==> Firewall"
# sshd -T fails until the first SSH login creates /run/sshd, so fall back to 22.
SSH_PORT="$(sshd -T 2>/dev/null | awk '$1 == "port" { print $2 }' || true)"
SSH_PORT="${SSH_PORT%%$'\n'*}"
SSH_PORT="${SSH_PORT:-22}"
ufw allow "$SSH_PORT/tcp" >/dev/null
ufw allow 80/tcp >/dev/null
ufw allow 443/tcp >/dev/null
ufw --force enable >/dev/null

SERVER_IP="$(ip -4 route get 1.1.1.1 | awk '{for (i = 1; i <= NF; i++) if ($i == "src") { print $(i + 1); exit }}')"

if [ "$NEW_KEY" = no ]; then
  cat <<EOF

================================================================================
 GigApp test server updated.

 Site:            https://$DOMAIN
 GitHub secrets:  unchanged - nothing to update in GitHub.
================================================================================
EOF
  exit 0
fi

cat <<EOF

================================================================================
 GigApp test server is ready for its first deploy.

 Site:  https://$DOMAIN
        The first deploy creates the test accounts listed under "Dev accounts"
        in CLAUDE.md. Their password is public, so change the admin password
        after signing in.

 Add these in GitHub: GigApp > Settings > Secrets and variables > Actions >
 New repository secret.

 GIGAPP_TEST_HOST
$SERVER_IP

 GIGAPP_TEST_KNOWN_HOSTS
$SERVER_IP $(cut -d' ' -f1,2 /etc/ssh/ssh_host_ed25519_key.pub)

 GIGAPP_TEST_SSH_KEY  (copy all of it, including the BEGIN and END lines)
$(cat "$KEY_DIR/key")

 Then: GitHub > Actions > "Deploy to test server" > Run workflow.
================================================================================
EOF

if [ "$SSH_PORT" != "22" ]; then
  echo "Note: SSH listens on port $SSH_PORT, but the deploy workflow connects on 22." >&2
fi
