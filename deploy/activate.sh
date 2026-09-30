#!/usr/bin/env bash
set -euo pipefail

sha="${1:?usage: activate.sh <commit>}"
base=/var/www/gigapp
shared=/var/lib/gigapp
release="$base/releases/$sha"
previous="$(readlink -e "$base/current" || true)"

if [ ! -d "$release" ]; then
  echo "No build uploaded at $release" >&2
  exit 1
fi

switch_to() {
  ln -sfn "$1" "$base/current.next"
  mv -Tf "$base/current.next" "$base/current"
  sudo -n /usr/bin/systemctl restart gigapp
}

is_healthy() {
  for _ in $(seq 1 30); do
    if curl -fsS -o /dev/null --max-time 5 -H 'X-Forwarded-Proto: https' http://127.0.0.1:5000/; then
      return 0
    fi
    sleep 2
  done
  return 1
}

echo "==> Linking uploads and private files"
mkdir -p "$release/wwwroot"
rm -rf "$release/wwwroot/uploads" "$release/App_Data"
ln -s "$shared/uploads" "$release/wwwroot/uploads"
ln -s "$shared/App_Data" "$release/App_Data"

echo "==> Migrating the database"
set -a
. "$shared/gigapp.env"
set +a
DOTNET_ROOT="$(dirname "$(readlink -f "$(command -v dotnet)")")"
export DOTNET_ROOT
cd "$release"
chmod +x efbundle
./efbundle --connection "$ConnectionStrings__DefaultConnection"

echo "==> Switching to $sha"
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

echo "==> Live: $sha"

live="$(readlink -e "$base/current")"
ls -1dt "$base"/releases/*/ | tail -n +6 | while read -r dir; do
  if [ "$(readlink -e "$dir")" != "$live" ]; then
    rm -rf "$dir"
  fi
done
