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
connection. Keep `SERVER_API_TOKEN` private and configure it only on trusted
dedicated-server processes.

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

Allow inbound UDP traffic on each game server's configured port. Set the
Unity `GameDirector` **Online Service Url** field to the Node service's public
HTTPS origin (for example, `https://api.example.com`) in the client build.
The game's online button supports registration, login, room creation/listing,
and joining; room creation reports an error while no dedicated server is
registered and healthy.

Do not expose this service or send passwords over plain HTTP on the public
internet. Terminate HTTPS at a trusted reverse proxy and restrict the service
and database with firewall rules. The Node service is the account/lobby
backend; Unity dedicated-server builds still run the real-time game.
