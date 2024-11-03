# Hollow;TerrainSystem (Prototype)
This project is a proof-of-concept for GPU driven 
terrain system made for Unity engine. This is **NOT** a production ready package, just a demo project.

Supports Unity 2022.3, **built-in render pipeline**.

## Features
* Adaptive Virtual Texturing
    * Uses job system for multithreaded feedback analysis & page update
    * Cache textures are BC1 compressed
* GPU driven LOD selection
  * LOD is controlled by pixel-error metric similar to built-in Unity terrain
  * Stitches vertices between terrain patches _(stitching between terrain tiles coming later)_
* GPU driven LOD quad-tree generation as well
  * 6ms to recompute 2049x2049 terrain LOD tree
* GPU driven cross-terrain tile painting, doesn't rely on CPU readback
  * Painting remains responsive no matter the brush size
* Simple Undo/Redo system for render textures integrated with Unity editor
* Splat mapping using texture arrays
  * 1 draw call instead of multi-pass
  * Supports blending based on heightmap 
  * Includes tools that combines multiple textures into Tex2DArray on import with limited auto-reformat functionality
  * Triplanar mapping
* Terrain works with scene picking (no outline though)

Anyway if you want to poke at it, go to `TestScene.unity`.
For render loop implementation check `PhotoTerrainBuiltInCamera` code. 

## Known Issues
* Shadows suck!
* Terrain isn't visible when viewing scene in wireframe or looking at camera preview in scene window
* AMD GPUs might act funny with virtual texture. I think I fixed it, but it may still be there
* When using brush, first stroke might randomly produce incorrect result

## Notes
* When using brushes, use alt+lmb and move mouse up/down to change opacity, left/right to change size. Alt + scroll will rotate the brush.
* If you modify material palette and want to make a build, use context menu -> "Create Texture Array" on palette asset. Texture arrays can't be created at runtime

## 3rd party
* ['Betsy' GPU compressor](https://github.com/darksylinc/betsy)
* [meshoptimizer](https://github.com/zeux/meshoptimizer)
* [LightProbeUtility by keijiro](https://github.com/keijiro/LightProbeUtility)
* [Unity's Terrain Tools (Icons)](https://docs.unity3d.com/Packages/com.unity.terrain-tools@5.0/manual/index.html)
* [Terrain Sample Asset Pack (Terrain textures)](https://assetstore.unity.com/packages/3d/environments/landscapes/terrain-sample-asset-pack-145808)

## References
* [GPUDrivenTerrainLearn](https://github.com/wlgys8/GPUDrivenTerrainLearn)
* [Terrain Rendering in Far Cry 5](https://ubm-twvideo01.s3.amazonaws.com/o1/vault/gdc2018/presentations/TerrainRenderingFarCry5.pdf)
* [Landscape creation and rendering in REDengine 3](https://ubm-twvideo01.s3.amazonaws.com/o1/vault/GDC2014/Presentations/Gollent_Marcin_Landscape_Creation_and.pdf)
* [Boots on the Ground: The Terrain of Call of Duty](https://research.activision.com/publications/2021/09/boots-on-the-ground--the-terrain-of-call-of-duty)
* [Large Scale Terrain Rendering in Call of Duty](https://advances.realtimerendering.com/s2023/Etienne(ATVI)-Large%20Scale%20Terrain%20Rendering%20with%20notes%20(Advances%202023).pdf)
* [J.M.P. van Waveren - Software Virtual Textures](https://mrelusive.com/publications/papers/Software-Virtual-Textures.pdf)
* [Adaptive Virtual Texture Rendering in Far Cry 4](https://www.youtube.com/watch?v=SVPMhGteeuE)
* [Virtual Texturing - Andreas Neu](https://www.graphics.rwth-aachen.de/media/papers/neu_virtual_texturing_low_031.pdf)
* [Normal Mapping for a Triplanar Shader - Ben Golus](https://bgolus.medium.com/normal-mapping-for-a-triplanar-shader-10bf39dca05a#ddd9)