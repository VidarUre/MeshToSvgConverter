# MeshToSvgConverter

Abstract:

When computer programs generate and render graphics, the result is typically raster graphics. Raster graphics is described by a 2D grid of a pre-defined size, e.g. 1000x1000, where each element is a color, called a pixel. This static nature of raster graphics can be a disadvantage in graphic design where artists may have rendered images of 3D scenes as their working material. Furthermore, such raster images lack information about geometry that is occluded from the given viewpoint, making it cumbersome to edit them. Vector graphics is an alternative approach that uses geometry like curves and paths to draw shapes, and the image can contain more than one layer of geometry. Because the size of such geometry is dynamic, vector graphics can be scaled without sacrificing visual quality. This thesis presents an implemented solution for converting the 3D meshes in a scene into layered vector graphics illustrations, including geometry that is occluded from the given viewpoint. All the triangles in a mesh are grouped together to increase their editability when they have been converted into vector graphics.

How to use it:

- Import all the needed meshes in the form of .obj files, along with their .mtl files. For the best editability of the SVG file, make sure that triangles are grouped into meshes with the <g>-tag in the .obj files prior to import.
- Adjust the camera perspective and objects in the scene as wished.
- Click Main Camera and in the script variables, specify storage path for the SVG file, number of depth layers, the two variables for merging silhouettes, and check the box for sorting layers by depth.
- Run the project by clicking the Play button. The scene will then be converted into an SVG file stored at the given location.
