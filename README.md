# Abrams (Leopard Battle)

A Unity tank combat prototype. Drive a player tank, fight randomly spawned AI tanks, and shoot down kamikaze drones with homing missiles.

## Requirements

- [Unity 2022.3.62f1](https://unity.com/releases/editor/whats-new/2022.3.62) (LTS)
- macOS, Windows, or Linux with that editor installed

The project tracks Visible Meta Files and Force Text serialization in `ProjectSettings/EditorSettings.asset`. Keep `.meta` files in Git whenever you add or rename assets.

## Getting started

1. Clone this repository.
2. Open the `Abrams` folder in Unity Hub with editor **2022.3.62f1**.
3. Wait for the first import to finish (`Library/` is generated locally and is not in Git).
4. Open `Assets/Tiny Teacup Studio/Military Base Pack/Demo/DemoScene.unity`.
5. Press Play.

## Controls

| Action | Mouse mode | Keyboard mode |
| --- | --- | --- |
| Drive | WASD / arrow keys | WASD / arrow keys |
| Aim turret / cannon | Mouse | J / L (yaw), I / K (pitch) |
| Fire main gun | Left click | Space |
| Scope zoom | Right click, mouse wheel | Shift, mouse wheel |
| Anti-drone missile | R | R |
| Drone camera | C | C |
| Pause | Esc | Esc |

Turret mode is switched in **Settings → Control Settings** on the in-game menu.

## Project layout

```
Assets/Scripts/     Gameplay scripts (player, AI, weapons, UI)
Assets/prefabs/     Player tank, AI tank, drone, shells, missiles
Assets/Tiny Teacup Studio/.../Demo/DemoScene.unity   Playable scene
Packages/           Unity package manifest
ProjectSettings/    Editor and player settings
```

The project includes local single-player play and browser-based online rooms.
The online flow uses the Node.js/PostgreSQL service for accounts and room
allocation, then connects WebGL clients to an authoritative WebSocket game
gateway. Rooms support up to 32 players and blue/red team selection. See the
[online server setup](./server/README.md) to configure and run both processes.
The current gateway simulates an authoritative tank arena rather than running
Unity physics; it is a separate gameplay model from the local single-player
scene. The online menu also provides an Ethereum Sepolia TCOIN/equipment panel.

## Online blockchain setup

The Reown AppKit package is declared in `Packages/manifest.json`. Desktop
builds use WalletConnect; after Unity resolves packages, run
**Abrams → Blockchain → Assign Reown AppKit Prefab** to assign the package
prefab to the playable demo scene. AppKit uses the Reown Project ID configured
in `GameDirector`.

The client targets Ethereum Sepolia (chain ID `11155111`) and the backend
template is configured with these supplied addresses:

| Contract | Address |
| --- | --- |
| TankCoin | `0x4fc5efd55b1c845e2e88b8431C0d018a6E5331FC` |
| TankGame | `0xed892a064b18F60b823634E4eaD74851FA6F29b6` |

The TankGame address has bytecode on Ethereum Sepolia and its `coin()` getter
returns the configured TankCoin address, but that TankCoin address currently
has no bytecode there. TCOIN and equipment transactions remain disabled until
TankCoin and TankGame are deployed with TankGame pointing to the deployed
TankCoin address, and the addresses are updated in Unity `GameDirector` and
`server/.env`. Battle rewards are not available in this single-player build.
Grant the `TankGame` contract
the required `MINTER_ROLE` and `GAME_ROLE` on TankCoin, and grant the trusted
backend signer `GAME_SERVER_ROLE` on TankGame. Never put the backend signer
key in the Unity project.

The Unity AppKit docs currently require Gamma color space; this project uses
Linear color space, so wallet-modal rendering and wallet callbacks still need
runtime validation before release.

WebGL builds use MetaMask's injected Chrome extension directly, so a phone or
WalletConnect QR scan is not needed. Host the game on HTTPS (or `localhost`)
and open it in a Chrome tab with MetaMask enabled. Click **CONNECT METAMASK**,
approve the account request, then approve switching to Ethereum Sepolia. The
game requests a wallet signature to link the address to the signed-in player;
that signature is not a blockchain transaction and does not spend ETH.
Desktop builds continue to use Reown AppKit and WalletConnect.

| Area | Main files |
| --- | --- |
| Game flow, pause, win/lose | `Assets/Scripts/GameDirector.cs` |
| Player drive / aim / fire | `TankMovement.cs`, `TankTurretMouse.cs` (`TankTurretMove`), `TankFire.cs` |
| Damage | `TankHealth.cs`, `TankShell.cs` |
| AI tanks | `AITankRandomspawner.cs` (`AIRandomSpawner`), `AITankController.cs` |
| Drones | `AIDroneSpawner.cs`, `AIDroneController.cs`, `AntiDroneLauncher.cs`, `MissileScript.cs` (`HomingMissile`) |
| Online client | [GameDirector.OnlineMultiplayer.cs](./Assets/Scripts/GameDirector.OnlineMultiplayer.cs), [AbramsGameSocket.jslib](./Assets/Plugins/WebGL/AbramsGameSocket.jslib) |
| Online API and WebSocket gateway | [server/src/index.js](./server/src/index.js), [server/src/game-ws-gateway.js](./server/src/game-ws-gateway.js), [server/src/game-room.js](./server/src/game-room.js) |

Third-party art and VFX live under `Assets/Samples/` and `Assets/Tiny Teacup Studio/`.

## Git notes

Ignored by design: `Library/`, `Temp/`, `Logs/`, `obj/`, `UserSettings/`, generated `.csproj` / `.sln` files, and OS junk.

After cloning, Unity regenerates those folders. Commit `.meta` files with every asset change so references stay valid for other machines.
