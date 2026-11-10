# Abrams online service

This Node.js service provides account registration/login, room discovery,
room allocation, and one-time player join tickets. It uses PostgreSQL for
persistent account and room data. It does not simulate or host tank gameplay:
internet matches require separate Unity dedicated-server builds running the
project's multiplayer scene and registering with this service.

## Run locally

Requirements: Node.js 20+ and PostgreSQL 14+.

1. Create a PostgreSQL database and user.
2. Copy `.env.example` to `.env`; set `DATABASE_URL`, `ACCESS_TOKEN_SECRET`,
   and `SERVER_API_TOKEN` to unique random values of at least 32 characters.
   Local development allows HTTP origins on `localhost` and `127.0.0.1`.
   In production, set `ALLOWED_ORIGINS` to a comma-separated list of exact
   HTTPS game origins, for example `https://game.example.com`.
3. Install and start the service:

   ```sh
   npm install
   npm test
   npm start
   ```

The service creates its tables on startup. `/health` checks both the HTTP
process and PostgreSQL connection.

Signed-in players can retrieve their persistent profile from
`GET /api/profile`. The response includes the account ID, username, wallet,
experience, battles played, victories, defeats, and tanks destroyed. Levels
are calculated as `floor(experience / 1000) + 1`; a completed battle awards
100 experience plus 25 per tank destroyed.

### Load development sample data

Start the service once so it can create the database tables, then, from this
directory, import the sample rows:

```sh
set -a
. ./.env
set +a
psql "$DATABASE_URL" -f sample-data.sql
```

The sample accounts are `samplepilot` and `redcommander`; both use the
development-only password `TankDemo2026!`. The sample match is marked complete
and its sample game server is offline, so it demonstrates stored data without
appearing as a joinable online room. Never use these credentials outside a
local development database.

## Dedicated game servers

Each running Unity dedicated server registers its endpoint with
`POST /api/server/register`, then sends periodic heartbeats. The service
assigns an available server to a newly created room and returns its endpoint
and one-time join ticket to the player. The server validates each
ticket against `/api/server/tickets/validate` before approving a Netcode
connection. Online gameplay uses Unity Transport 2.x WebSockets so WebGL
players can connect to dedicated servers. Keep `SERVER_API_TOKEN` private and
configure it only on trusted dedicated-server processes.

### LAN lobby

LAN games use anonymous guest sessions, a separate Node/PostgreSQL-based room
directory, and a Unity Linux dedicated game server. WebGL and desktop clients
can both create and join rooms. The room creator connects as a normal player
after the room is allocated, so its tank appears immediately while other
players join. The **LAN GAME** menu does not ask players to register or sign
in; **ONLINE BATTLE** remains a separate account and dedicated-server flow.

#### Start the LAN stack

Requirements: Docker Desktop (or Docker Engine with Compose) and Node.js 20+
for the startup script, plus Unity 2022.3.62f1 with Linux Dedicated Server
Build Support.

1. In Unity, open the playable demo scene and build
   **Abrams → Build → Linux Dedicated Game Server**. The build is saved to
   `server/game-server/`. Rebuild it after changing the game code or player
   prefab.
2. Find the LAN computer's private IPv4 address (for example `192.168.1.20`).
   From the project root run:

   ```sh
   ./server/start-lan.sh 192.168.1.20
   ```

   On macOS, the script starts Docker Desktop if it is installed but stopped;
   on other platforms, start Docker first. It creates a private
   `server/.env.compose` with random secrets, starts PostgreSQL, the lobby API,
   WebGL hosting, and the dedicated game server, then waits for both services
   to become ready. The LAN URL is `http://192.168.1.20:8080`; the API listens
   on port 3001 on the LAN host (mapped to port 3000 inside Docker). PostgreSQL
   is not exposed outside Docker.
3. If needed, rebuild the WebGL player in Unity using
   **Abrams → Build → WebGL LAN Player**. The build is saved to
   `tankbattle(WebGL)/` and uses plain HTTP for the private-LAN lobby API.
   Install WebGL Build Support from Unity Hub if Unity reports that the module
   is missing.
4. Allow inbound TCP ports 8080, 3001, and 7777 through the LAN computer's
   firewall.

Other LAN players open `http://192.168.1.20:8080` (or launch a desktop build),
choose **LAN GAME**, enter a display name, choose a team, then create or join
the published room. The WebGL page automatically addresses the lobby at the
same host on port 3001. For desktop clients, set **LAN Lobby Service Url** on
the scene's `GameDirector` to `http://192.168.1.20:3001` before building or
running the game. No account or internet access is required for LAN play.
Clients connect directly to the dedicated game server over WebSockets; the
Node/PostgreSQL service handles room discovery, membership, and connection
approval.

Check services and logs with:

```sh
docker compose -f server/docker-compose.yml \
  --env-file server/.env.compose ps
docker compose -f server/docker-compose.yml \
  --env-file server/.env.compose logs -f api
docker compose -f server/docker-compose.yml \
  --env-file server/.env.compose logs -f game-server
```

After rebuilding the Unity dedicated server, restart just that service with:

```sh
./server/start-game-server.sh
```

Stop the containers without deleting saved room/account data:

```sh
docker compose -f server/docker-compose.yml \
  --env-file server/.env.compose down
```

The WebGL page, lobby API, and game server must be reachable from every player.
The HTTP setup is for a trusted private LAN; do not expose these ports to the
public internet.

### Blockchain battle rewards

The service can submit verified battle rewards through the `TankGame` contract.
Set `BLOCKCHAIN_RPC_URL`, `BLOCKCHAIN_CHAIN_ID`,
`TANK_COIN_CONTRACT_ADDRESS`, and `TANK_GAME_CONTRACT_ADDRESS` in `.env`, and
set `GAME_SERVER_PRIVATE_KEY` to a dedicated backend signer key. Configure
Ethereum Sepolia with chain ID `11155111` and
`https://ethereum-sepolia-rpc.publicnode.com`. The supplied TankGame address
has bytecode on Ethereum Sepolia and points to the supplied TankCoin address,
but that TankCoin address has no bytecode there. Deploy TankCoin and deploy
TankGame with the deployed TankCoin address, then update `.env` and the Unity
GameDirector contract-address fields before using the economy or battle
rewards. Grant
that signer `GAME_SERVER_ROLE` on
`TankGame`; also grant the `TankGame` contract `MINTER_ROLE` and `GAME_ROLE` on
`TankCoin`. Keep the signer key private and never put it in a Unity client.

After independently verifying a battle result, a trusted dedicated server can
call `POST /api/server/rewards` with `X-Server-Token` and JSON fields
`playerAddress`, `amountWei`, `reason`, and `battleId`. `amountWei` is a
positive decimal integer in TCOIN's smallest units (18 decimals), and
`battleId` is a unique, nonzero `bytes32` hex value. The contract prevents a
battle ID from being rewarded more than once; retries return
`already_rewarded`. This endpoint trusts authenticated dedicated servers to
verify game outcomes and must not be exposed as a player reward claim API.
Players link a reward wallet while signed in: request `POST /api/wallet/challenge`
with an `address`, sign the returned `message` with that wallet, and submit the
address, returned `nonce`, and signature to `POST /api/wallet/verify`. The
challenge expires after five minutes, is single-use, and each wallet can be
linked to only one account. The dedicated server receives the verified wallet
address as part of join-ticket validation; it must never accept a reward
destination directly from a game client. Set
`ABRAMS_ONLINE_KILL_REWARD_WEI` in each dedicated server process to the
positive integer reward per enemy tank destroyed.

Build a Unity Linux dedicated-server player that includes the playable
`DemoScene` and has `GameDirector.multiplayerTankPrefab` assigned. Run one
process per available match server with these environment variables:

```text
ABRAMS_ONLINE_DEDICATED_SERVER=1
ABRAMS_ONLINE_SERVER_ID=region-a-01
ABRAMS_ONLINE_API_URL=https://api.example.com
ABRAMS_ONLINE_SERVER_HOST=game-server-public-hostname
ABRAMS_ONLINE_SERVER_PORT=7777
SERVER_API_TOKEN=<same private value configured on the Node service>
```

For local development with an HTTP API and an HTTP-hosted WebGL build, the
game transport uses unencrypted WebSockets. For production, serve the game
over HTTPS and set `ABRAMS_ONLINE_API_URL` and the Unity `GameDirector`
**Online Service Url** to HTTPS. Install a valid PEM certificate and
unencrypted PEM private key on each dedicated server, with a hostname that
matches `ABRAMS_ONLINE_SERVER_HOST`, and set:

```text
ABRAMS_ONLINE_SERVER_CERTIFICATE_FILE=/run/secrets/game-server.crt
ABRAMS_ONLINE_SERVER_PRIVATE_KEY_FILE=/run/secrets/game-server.key
```

With an HTTPS API URL, the dedicated server automatically enables TLS for its
WebSocket listener; clients validate the certificate against the registered
server hostname. Expose the configured game port over TCP for WebSocket
traffic. Online Battle uses WebSockets. The Online Battle menu supports registration, login, room
creation/listing, and joining; room creation reports an error while no
dedicated server is registered and healthy.

Do not expose this service or send passwords over plain HTTP on the public
internet. Terminate HTTPS at a trusted reverse proxy and restrict the service
and database with firewall rules. The Node service is the account/lobby
backend; Unity dedicated-server builds still run the real-time game.
