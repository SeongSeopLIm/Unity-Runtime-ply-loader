# RuntimePLY Load for unity


![enter image description here](https://user-images.githubusercontent.com/15900198/138194221-ffd106f5-1b11-4421-864d-8e2f721c0936.PNG)

## Requirements

 - Unity 2020.3.15
 - UniRX (https://github.com/neuecc/UniRx)
 - Compute shaders support platforms
	 - https://docs.unity3d.com/Manual/class-ComputeShader.html
	 -  DX11, DX12, Vulkan,  etc. But i checked only one windows DX11
	 - This Point cloud rendering shader is using the geometry shader. if you want to use it with Metal, should change code. See PSIZE in Metal Shaders.



## How to

 1. Clone or download
 2. Open project
 3. Enter DemoScene (Assets/RuntimPLYLoader/DemoScene.unity)
 4. Play

You can check PointCloudLoadr.cs

## Acknowledgements

Point picking (octree). Please see the following page for details.
https://github.com/Nition/UnityOctree

Ply load was referanced by pcx. Please see the following page for details.
https://github.com/keijiro/Pcx


The point cloud files used in the examples is created by Thomas Flynn and shared under the Creative Commons Attribution license. Please see the following page for details.

https://sketchfab.com/3d-models/hintze-hall-nhm-london-point-cloud-be909aa8afa545118be6d36397529e2f
