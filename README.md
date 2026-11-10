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
Assets/Scripts/
  AI/               AI tank and drone controllers
  Core/             GameDirector and its feature partials
  Drones/           Drone controls and camera
  Effects/          Tracks, exhaust, and visual effects
  Networking/       LAN and online multiplayer networking
  Tank/             Tank movement, health, camera, and controls
  UI/               HUD and minimap markers
  Weapons/          Launchers, shells, and missiles
Assets/prefabs/     Player tank, AI tank, drone, shells, missiles
Assets/Tiny Teacup Studio/.../Demo/DemoScene.unity   Playable scene
Packages/           Unity package manifest
ProjectSettings/    Editor and player settings
```

Gameplay is a set of `MonoBehaviour` components. Online accounts and room
coordination are provided by the Node.js/PostgreSQL service in [server](./server/README.md).
Internet matches use separately deployed Unity dedicated-server builds and
the online service and Unity Transport. The online menu also provides player
profiles and an Ethereum Sepolia TCOIN/equipment panel.

LAN matches use anonymous guest sessions, the room directory, and a Unity
Linux dedicated game server. Both WebGL and desktop clients can create or join
rooms; the creator's tank spawns immediately and can play while others join.
Build the server from **Abrams → Build → Linux Dedicated Game Server**, then
run `./server/start-lan.sh <private LAN IPv4>` to start the LAN services,
including the dedicated game server. **Online Service Url** and the Online
Battle dedicated-server flow remain separate. See
[the LAN setup](./server/README.md#lan-lobby).

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
has no bytecode there. TCOIN and equipment transactions, and backend battle
rewards, remain disabled until TankCoin and TankGame are deployed with TankGame
pointing to the deployed TankCoin address, and the addresses are updated in
Unity `GameDirector` and `server/.env`. Grant the `TankGame` contract
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
| Game flow, pause, win/lose | `Assets/Scripts/Core/GameDirector.cs` |
| Player drive / aim / fire | `Assets/Scripts/Tank/TankMovement.cs`, `Assets/Scripts/Tank/TankTurretMouse.cs` (`TankTurretMove`), `Assets/Scripts/Tank/TankFire.cs` |
| Damage | `Assets/Scripts/Tank/TankHealth.cs`, `Assets/Scripts/Weapons/TankShell.cs` |
| AI tanks | `Assets/Scripts/AI/AITankRandomspawner.cs` (`AIRandomSpawner`), `Assets/Scripts/AI/AITankController.cs` |
| Drones | `Assets/Scripts/AI/AIDroneSpawner.cs`, `Assets/Scripts/AI/AIDroneController.cs`, `Assets/Scripts/Drones/TankDronecontroll.cs`, `Assets/Scripts/Weapons/AntiDroneLauncher.cs`, `Assets/Scripts/Weapons/MissileScript.cs` (`HomingMissile`) |
| LAN and online networking | `Assets/Scripts/Networking/` |
| Online accounts and rooms | [server/src/index.js](./server/src/index.js), [server/src/security.js](./server/src/security.js) |

Third-party art and VFX live under `Assets/Samples/` and `Assets/Tiny Teacup Studio/`.

## Git notes

Ignored by design: `Library/`, `Temp/`, `Logs/`, `obj/`, `UserSettings/`, generated `.csproj` / `.sln` files, and OS junk.

After cloning, Unity regenerates those folders. Commit `.meta` files with every asset change so references stay valid for other machines.
