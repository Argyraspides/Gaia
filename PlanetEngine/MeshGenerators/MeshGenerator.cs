using System;
using Gaia.PlanetEngine.MapTiles;
using Godot;

namespace Gaia.PlanetEngine.MeshGenerators;

public static partial class MeshGenerator
{
    public static MeshInstance3D GenerateMesh(MapTileType tileType)
    {
        switch (tileType)
        {
            case MapTileType.WEB_MERCATOR_EARTH:
                // TODO:: how to handle dynamic params?
                return GenerateWebMercatorMesh();
            default:
                throw new NotImplementedException();
        }
    }

    // Generates a plane mesh. Each vertex corresponds to a pixel
    // so that you can use height maps to deform the mesh to create
    // terrain
    // width and height are for how big the map tile is going to be.
    // Common size is 256x256 as that is the size given back by most
    // map provider apis
    public static MeshInstance3D GenerateWebMercatorMesh(int width = 1, int height = 1)
    {
        PlaneMesh planeMesh = new PlaneMesh();

        planeMesh.Size = new Vector2(1.0f, 1.0f);
        planeMesh.SubdivideWidth = width;
        planeMesh.SubdivideDepth = height;

        return new MeshInstance3D() { Mesh = planeMesh };
    }
}