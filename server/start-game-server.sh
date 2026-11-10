#!/bin/sh
set -eu

SCRIPT_DIR=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
cd "$SCRIPT_DIR"

if [ ! -f .env.compose ]; then
    echo "Run ./server/start-lan.sh <LAN IPv4> first to configure the LAN services." >&2
    exit 1
fi

if [ ! -f game-server/AbramsServer.x86_64 ]; then
    echo "Build the Linux dedicated server in Unity using Abrams > Build > Linux Dedicated Game Server first." >&2
    exit 1
fi

docker compose --env-file .env.compose up -d --build game-server
container_id=$(docker compose --env-file .env.compose ps -q game-server)
if [ -z "$container_id" ]; then
    echo "Docker Compose did not create the LAN game-server container." >&2
    exit 1
fi
container_started_at=$(docker inspect --format '{{.State.StartedAt}}' "$container_id")

server_ready=false
for attempt in $(seq 1 30); do
    if docker compose --env-file .env.compose logs \
        --since "$container_started_at" --no-color game-server 2>/dev/null |
        grep -q "LAN dedicated server registered and ready"; then
        server_ready=true
        break
    fi
    sleep 2
done

if [ "$server_ready" != true ]; then
    echo "The LAN dedicated game server did not register. Recent logs:" >&2
    docker compose --env-file .env.compose logs --tail=80 game-server >&2
    exit 1
fi

echo "LAN dedicated game server is registered and ready on port 7777."
