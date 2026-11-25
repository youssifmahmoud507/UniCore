#!/usr/bin/env bash
# Smoke test of a RUNNING stack (docker compose up -d). It checks what the Testcontainers tests cannot: the real image,
# the migrations bundle, the seeded admin and the real configuration. It only reads, except for the login itself.
#
#   ./scripts/smoke.sh                 # uses .env and API_PORT (default 8080)
#   BASE_URL=http://host:port ./scripts/smoke.sh
set -u

if [ -f .env ]; then
  set -a
  # shellcheck disable=SC1091
  . ./.env
  set +a
fi

BASE_URL="${BASE_URL:-http://localhost:${API_PORT:-8080}}"
LOGIN="${SEED_ADMIN_USERNAME:-superadmin}"
PASSWORD="${SEED_ADMIN_PASSWORD:-}"
failed=0

check() { # name expected actual
  if [ "$2" = "$3" ]; then
    echo "  ok   $1"
  else
    echo "  FAIL $1 (expected $2, got $3)"
    failed=$((failed + 1))
  fi
}

code() { curl -s -o /dev/null -w '%{http_code}' "$@"; }

echo "Smoke test against $BASE_URL"

echo "Health"
check "live"  200 "$(code "$BASE_URL/health/live")"
check "ready" 200 "$(code "$BASE_URL/health/ready")"

echo "Authentication"
check "no token is refused"       401 "$(code "$BASE_URL/api/v1/me")"
check "a garbage token is refused" 401 "$(code -H 'Authorization: Bearer not-a-token' "$BASE_URL/api/v1/me")"
check "a wrong password is refused" 401 "$(code -X POST -H 'Content-Type: application/json' -d "{\"login\":\"$LOGIN\",\"password\":\"definitely-wrong\"}" "$BASE_URL/api/v1/auth/login")"

if [ -z "$PASSWORD" ]; then
  echo "  SKIP the rest: SEED_ADMIN_PASSWORD is not set (put it in .env)"
  exit 1
fi

body="$(curl -s -X POST -H 'Content-Type: application/json' -d "{\"login\":\"$LOGIN\",\"password\":\"$PASSWORD\"}" "$BASE_URL/api/v1/auth/login")"
token="$(printf '%s' "$body" | sed -n 's/.*"accessToken":"\([^"]*\)".*/\1/p')"
if [ -z "$token" ]; then
  echo "  FAIL the seeded admin could not log in (was the migrate service run and the seed configured?)"
  echo "$body"
  exit 1
fi
echo "  ok   the seeded admin logs in"

auth=(-H "Authorization: Bearer $token")

echo "Signed-in reads (these touch the database and the migrated schema)"
check "me"             200 "$(code "${auth[@]}" "$BASE_URL/api/v1/me")"
check "dashboard"      200 "$(code "${auth[@]}" "$BASE_URL/api/v1/dashboard")"
check "report list"    200 "$(code "${auth[@]}" "$BASE_URL/api/v1/reports")"
check "audit log"      200 "$(code "${auth[@]}" "$BASE_URL/api/v1/audit-logs?pageSize=1")"
check "students list"  200 "$(code "${auth[@]}" "$BASE_URL/api/v1/students?pageSize=1")"
check "roles"          200 "$(code "${auth[@]}" "$BASE_URL/api/v1/roles")"
check "notifications"  200 "$(code "${auth[@]}" "$BASE_URL/api/v1/notifications")"
check "workflow inbox" 200 "$(code "${auth[@]}" "$BASE_URL/api/v1/workflows/inbox")"

echo "Behaviour that must hold in the real container"
check "an unknown route is 404" 404 "$(code "${auth[@]}" "$BASE_URL/api/v1/no-such-thing")"
headers="$(curl -s -D - -o /dev/null "$BASE_URL/api/v1/me")"
case "$headers" in
  *[Nn]osniff*) echo "  ok   X-Content-Type-Options: nosniff" ;;
  *) echo "  FAIL X-Content-Type-Options header is missing"; failed=$((failed + 1)) ;;
esac

echo
if [ "$failed" -eq 0 ]; then
  echo "Smoke test passed."
else
  echo "Smoke test FAILED: $failed check(s)."
  exit 1
fi
