#!/bin/sh
set -eu

SCRIPT_DIR=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
cd "$SCRIPT_DIR"

if [ "$#" -ne 1 ]; then
    echo "Usage: ./server/start-lan.sh <LAN IPv4 address>" >&2
    exit 2
fi

LAN_HOST=$1
if ! node -e 'const net = require("node:net"); const ip = process.argv[1].split(".").map(Number); process.exit(net.isIPv4(process.argv[1]) && (ip[0] === 10 || (ip[0] === 172 && ip[1] >= 16 && ip[1] <= 31) || (ip[0] === 192 && ip[1] === 168)) ? 0 : 1)' "$LAN_HOST"; then
    echo "The LAN address must be a private IPv4 address, for example 192.168.1.20." >&2
    exit 2
fi

if [ ! -f "$SCRIPT_DIR/game-server/AbramsServer.x86_64" ]; then
    echo "Build the Linux dedicated server in Unity using Abrams > Build > Linux Dedicated Game Server first." >&2
    exit 1
fi

if ! docker info >/dev/null 2>&1; then
    if [ "$(uname -s)" = "Darwin" ] &&
        [ -d "/Applications/Docker.app" ]; then
        echo "Starting Docker Desktop..."
        open -a Docker
        docker_ready=false
        for attempt in $(seq 1 36); do
            if docker info >/dev/null 2>&1; then
                docker_ready=true
                break
            fi
            sleep 5
        done
        if [ "$docker_ready" != true ]; then
            echo "Docker Desktop did not become ready within three minutes." >&2
            exit 1
        fi
    else
        echo "Docker is not running. Start Docker or the Docker service first." >&2
        exit 1
    fi
fi

if [ ! -f .env.compose ]; then
    ACCESS_TOKEN_SECRET=$(openssl rand -hex 32)
    SERVER_API_TOKEN=$(openssl rand -hex 32)
    POSTGRES_PASSWORD=$(openssl rand -hex 32)
    umask 077
    {
        printf 'ALLOWED_ORIGINS=http://%s:8080\n' "$LAN_HOST"
        printf 'LAN_HOST=%s\n' "$LAN_HOST"
        printf 'POSTGRES_PASSWORD=%s\n' "$POSTGRES_PASSWORD"
        printf 'ACCESS_TOKEN_SECRET=%s\n' "$ACCESS_TOKEN_SECRET"
        printf 'SERVER_API_TOKEN=%s\n' "$SERVER_API_TOKEN"
    } > .env.compose
    echo "Created server/.env.compose with private random secrets."
else
    if ! grep -Fxq "ALLOWED_ORIGINS=http://$LAN_HOST:8080" .env.compose; then
        echo "server/.env.compose is configured for a different LAN address." >&2
        echo "Update ALLOWED_ORIGINS in that file, then retry." >&2
        exit 1
    fi
    if grep -q '^LAN_HOST=' .env.compose; then
        if ! grep -Fxq "LAN_HOST=$LAN_HOST" .env.compose; then
            echo "server/.env.compose is configured for a different game-server address." >&2
            echo "Update LAN_HOST in that file, then retry." >&2
            exit 1
        fi
    else
        printf 'LAN_HOST=%s\n' "$LAN_HOST" >> .env.compose
    fi
    echo "Using existing server/.env.compose."
fi

docker compose --env-file .env.compose up -d --build postgres api web

api_ready=false
for attempt in $(seq 1 30); do
    if node -e 'fetch("http://127.0.0.1:3001/health").then(response => process.exit(response.ok ? 0 : 1)).catch(() => process.exit(1))'; then
        api_ready=true
        break
    fi
    sleep 2
done

if [ "$api_ready" != true ]; then
    echo "The LAN lobby API did not become healthy. Recent API logs:" >&2
    docker compose --env-file .env.compose logs --tail=50 api >&2
    exit 1
fi

./start-game-server.sh

printf '\nLAN lobby is healthy and ready.\n'
printf 'WebGL game: http://%s:8080\n' "$LAN_HOST"
printf 'LAN lobby API: http://%s:3001\n' "$LAN_HOST"
printf 'LAN dedicated game server: %s:7777\n' "$LAN_HOST"
