## Changelog

### 0.8.3
* Bug fixes

### 0.8.2
* Added optional uv generation to geometry generation.

### 0.8.1
* Fix for Studio Visual Effect Graph

### 0.8.0
* Updated to Unity 2020.3
* Improved accuracy of texture blending between perspectives.
* Allow for fading out texture at the boundaries of perspectives using Invalid Edge Width slider.
* Add ability to specify how untextured geometry is handled.
* Improved edge masking usability and performance.

### 0.7.1
* Exposed Disable Main Directional Shadows toggle in BiRP Photo Look shader.

### 0.7.0
* Fixed per perspective radial bias depth compensation. 

### 0.6.3
* removed misc files

### 0.6.2
* added define for photolook to ignore main light shadows

### 0.6.1
* Allow BiRP photolook to use shadows from all light types.

### 0.6.0
* Sync look collider and triangle mesh to volume bounds
* Allow BiRP procedural looks to sample light probes
* Once volume bounds are set, only allow reset on pressing the reset button in the inspector

### 0.5.1
* Bug fixes

### 0.5.0
* Added material property block support
* Bug fixes

### 0.3.0
* Improved performance of triangle extraction kernel
* Added depthkit icon to all depthkit components
* Volume Density now is a slider that controls voxels per meter
* Manual Volume bounds are always on in the scene view and control the number of voxels used to reconstruct the clip, no longer does it control the size of the voxels.
* Added Edge Mask feature to **Studio Mesh Source** Component for color blending
	* Global edge mask control
	* Individual perspective edge mask control
* Changed the color blending occlusion test to be a smoothstep interpolation rather than a hard coded if/else.
* Updated normal generation kernel to output WS Depth to a texture used in the Edge Masking feature.  This is disabled entirely if the edge mask is disabled. 
* Updated SDF generation to filter out invalid voxels that lie outside of the frustum of all perspectives.
