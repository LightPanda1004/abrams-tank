# Tank Arena Terrain — Unity Import Package

## Install
1. Open an existing Unity 2021.3+ project (Built-in or URP recommended).
2. Copy the included `Assets/TankArena` folder into your project's `Assets` folder.
3. Wait for Unity to finish importing.
4. From Unity's top menu select **Tools > Tank Arena > Create Arena Scene**.
5. Open `Assets/TankArena/Scenes/TankArenaDemo.unity` (the builder also opens it).
6. Place your own tank prefabs at the two Tank Spawn Points and configure gameplay.

## Included
- 513 x 513 16-bit RAW heightmap, 500 x 500 x 95 terrain
- 2048 x 2048 albedo (extracted from supplied image)
- 1024 x 1024 normal and roughness references
- Height preview PNG
- Unity Editor scene generator, TerrainData, TerrainLayer, collider, lighting, camera, two empty spawn transforms

## Notes / limitations
- This is an image-derived terrain, not a reconstruction of a 3D source mesh. Cliff silhouettes and texture registration are approximate. Some slopes are steep and not tank-drivable; edit terrain or adjust spawn points as needed.
- The albedo is a projected overview image and may have baked shadows, perspective, and minor distortions. It is not a seamless tileable texture.
- Normal and roughness images are approximate crops from the provided reference composite, not independently authored calibrated PBR maps. Roughness is included as a reference texture but not automatically wired to Unity's terrain shader.
- No tank models, gameplay controller, vegetation prefabs, or separate cliff meshes are included.
- Heightmap uses little-endian unsigned 16-bit samples, 513x513, with vertical rows flipped to Unity terrain coordinates.
- Unity's normal texture importer may need Texture Type set to Normal Map for correct results. Select `Arena_Normal.png`, set **Texture Type: Normal map**, and click Apply. Unity may ask to fix normal maps automatically.
- The generator overwrites its own `TankArenaTerrain.asset`, `ArenaTerrainLayer.terrainlayer`, and `Scenes/TankArenaDemo.unity` on each run. Back up edits before regenerating.
