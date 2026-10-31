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

Gameplay is a set of `MonoBehaviour` components. There is no extra framework or backend.

| Area | Main files |
| --- | --- |
| Game flow, pause, win/lose | `Assets/Scripts/GameDirector.cs` |
| Player drive / aim / fire | `TankMovement.cs`, `TankTurretMouse.cs` (`TankTurretMove`), `TankFire.cs` |
| Damage | `TankHealth.cs`, `TankShell.cs` |
| AI tanks | `AITankRandomspawner.cs` (`AIRandomSpawner`), `AITankController.cs` |
| Drones | `AIDroneSpawner.cs`, `AIDroneController.cs`, `AntiDroneLauncher.cs`, `MissileScript.cs` (`HomingMissile`) |

Third-party art and VFX live under `Assets/Samples/` and `Assets/Tiny Teacup Studio/`.

## Git notes

Ignored by design: `Library/`, `Temp/`, `Logs/`, `obj/`, `UserSettings/`, generated `.csproj` / `.sln` files, and OS junk.

After cloning, Unity regenerates those folders. Commit `.meta` files with every asset change so references stay valid for other machines.
