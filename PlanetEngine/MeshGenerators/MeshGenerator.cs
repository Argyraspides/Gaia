using Godot;

namespace Gaia.PlanetEngine.MeshGenerators;

public static partial class MeshGenerator
{
    
    // Generates a plane mesh. Each vertex corresponds to a pixel
    // so that you can use height maps to deform the mesh to create
    // terrain
    // width and height are for how big the map tile is going to be.
    // Common size is 256x256 as that is the size given back by most
    // map provider apis
    public static ArrayMesh GenerateWebMercatorMesh(int width, int height)
    {
        var arrayMesh = new ArrayMesh();
        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);

        for (int i = 0; i < width; i++)
        {
            for (int j = 0; j < height; j++)
            {
                
            }
        }
        
    }
}