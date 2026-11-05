# Tank Valley — Unity combat environment

A self-contained, stylized 768 × 768 meter environment inspired by the terrain concept. Includes a saved `.unity` scene, a supplied heightmap, pre-shaped terrain geometry, procedural buildings and ruins, rock formations, pine forests, river bridges, cover, colliders, team bases, and gameplay markers. No external art packs are required.

## Import into an existing project

1. Open a **Unity 6** project. The Built-in Render Pipeline is the primary target.
2. Choose **Assets → Import Package → Custom Package…** and select `TankValley.unitypackage`.
3. Import **all** the package contents, including the Editor folder. Wait for script compilation.
4. Open **Assets/TankValley/Scenes/TankValley.unity**, or choose **Tools → Tank Valley → Open Map**.
5. First opening automatically creates a native Unity Terrain asset from the included heightmap and disables the pre-shaped mesh fallback. It saves the supplied scene when there are no pre-existing unsaved edits. If the scene shows an unsaved-change marker, save it yourself.
6. Press **Play** to inspect the map. Click the Game view: **W/A/S/D** to move, **Q/E** to descend/ascend, **Shift** to move faster, and **right-drag** to look.

There is no manual heightmap import, sculpting, or object placement required. Keep the `Assets/TankValley` folder at its original location for the automatic setup.

## Open the standalone project

Extract `TankValley-Unity6-Project.zip`, then in Unity Hub choose **Add → Add project from disk** and select the extracted `TankValleyProject` folder. Open it in Unity 6 and follow steps 4–6 above. A valid Unity editor license is required as usual.

## Contents

- 768 × 768 meter terrain; 128 meter height scale.
- 1025 × 1025 samples, 16-bit little-endian RAW heightmap, already applied to the saved terrain meshes.
- Automatic native Terrain creation with the same heightmap and ground texture.
- A 2048 × 2048 ground color texture with grass, dirt roads, gravel, and exposed rock.
- 760 pine trees with trunk capsule colliders.
- 225 large boulders plus riverbank rocks.
- Ruined factory halls, warehouses, farmhouses, smokestacks, storage tanks, bunkers, concrete barriers, sandbags, and three river bridges.
- Two bases, ten named spawn transforms, a factory capture-point transform, and three waypoint routes.
- Static tank props for scale. These do not drive or fire.
- An overview camera, daylight, and four invisible boundary colliders.

All building, bridge, boulder, ground, and combat-cover meshes have colliders. Buildings and trees are separate scene objects, grouped in the hierarchy. Shared meshes and materials are in the corresponding asset folders. Water is a visual surface; it has no buoyancy or damage logic.

## Terrain and editing

On the first scene opening, `ValleyTerrainInstaller` creates `Assets/TankValley/Generated/ValleyTerrain.asset`, the Terrain Layer, and a terrain material. The initial scene already contains heightmap-shaped mesh ground so it has visible, collidable terrain before this step.

Select **Valley Terrain - heightmap applied** to use Unity's terrain sculpting tools. The supplied ground color is one full-map Terrain Layer; add your own tileable layers if you want to repaint it with more detail. The disabled mesh group can be used as a fallback, but enable only one ground system at a time to avoid duplicate collisions and flickering surfaces.

If setup did not complete, use **Tools → Tank Valley → Finish Terrain Setup**. Use **Tools → Tank Valley → Validate Open Map** to check native terrain, mesh references, and materials inside Unity.

The setup script operates on the supplied Tank Valley scene and assets. It does not change your other scenes or your project's render pipeline. It includes material adaptation for URP, but URP and HDRP have not been verified here; the standalone project uses the Built-in pipeline.

## Scope and validation

This is an environment scene using stylized procedural models, rather than a photorealistic reconstruction of the concept image. Add your own tank controller, combat logic, AI, objectives, and navigation baking.

The C# scripts were compiled against Unity 6000.5.7f1 reference libraries. Asset references, scene structure, mesh buffers, and heightmap samples were checked outside Unity. The local Unity editor could not launch because its license was not activated, so an actual Unity import, rendering, Play Mode, and automatic native-terrain setup remain unverified. `TankValley-scene-preview.png` is an offline render of the supplied geometry, not a Unity screenshot.

Manual heightmap settings, if needed: **1025 × 1025**, **16-bit**, **little-endian / Windows byte order**, terrain size **768 × 128 × 768**. The `.raw` rows run from world Z = 0 upward; the PNG preview is flipped for normal image viewing. Use the provided RAW data for elevation rather than converting the original concept image to grayscale.

Useful Unity documentation: [Heightmaps](https://docs.unity.cn/Manual/terrain-Heightmaps.html), [TerrainData](https://docs.unity3d.com/cn/6000.0/ScriptReference/TerrainData.html), and [scene-open callbacks](https://docs.unity3d.com/ja/6000.0/ScriptReference/SceneManagement.EditorSceneManager-sceneOpened.html).

The included terrain, geometry, and textures were generated specifically for this deliverable; no third-party models or textures are bundled.
