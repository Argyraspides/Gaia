/*




     ,ad8888ba,         db         88         db
    d8"'    `"8b       d88b        88        d88b
   d8'                d8'`8b       88       d8'`8b
   88                d8'  `8b      88      d8'  `8b
   88      88888    d8YaaaaY8b     88     d8YaaaaY8b
   Y8,        88   d8""""""""8b    88    d8""""""""8b
    Y8a.    .a88  d8'        `8b   88   d8'        `8b
     `"Y88888P"  d8'          `8b  88  d8'          `8b

                     WEAVER OF WORLDS

*/

using System;
using System.Threading.Tasks;
using Gaia.Common.Utils.Godot;
using Gaia.Common.Utils.Logging;
using Gaia.PlanetEngine.MapDataRetrieval;
using Gaia.PlanetEngine.MapTiles;
using Gaia.PlanetEngine.Utils;
using Godot;

namespace Gaia.PlanetEngine.LoDSystem;

/// <summary>
/// Represents a single chunk of planetary terrain in a quadtree structure.
/// Handles loading and display of map tiles from a Web Mercator projection, reprojecting them
/// onto an ellipsoidal surface. Each chunk knows its position (lat/lon in radians) and coverage area.
///
/// The reference frame of the TerrainChunk is determined by the underlying map tile
/// </summary>
public partial class TerrainChunk : Node3D
{
    public MapTile MapTile { get; private set; }

    /// <summary>
    /// Gets or sets the mesh that will define the geometry of the chunk.
    /// In general, if the mesh includes the poles of the planet,
    /// the mesh will be triangular. Otherwise, it will be a quadrilateral.
    /// </summary>
    public MeshInstance3D TerrainChunkMesh { get; private set; }


    /// <summary>
    /// Gets or sets the shader material used for map reprojection.
    /// E.g., warping a Web-Mercator projection map tile
    /// such that it can be fit to an ellipsoid.
    /// </summary>
    public ShaderMaterial ShaderMaterial { get; private set; }


    public MeshInstance3D MeshInstance
    {
        get => TerrainChunkMesh;
        set => TerrainChunkMesh = value;
    }
    
    public TerrainChunk(MapTile mapTile)
    {
        if (mapTile == null)
        {
            throw new ArgumentNullException("Cannot create a TerrainChunk with a null map tile");
        }

        MapTile = mapTile;

    }

    public async void Load()
    {
            if (GodotUtils.IsValid(TerrainChunkMesh))
            {
                AddChild(TerrainChunkMesh);
                await InitializeTerrainChunkAsync();
            }
            else
            {
                Logger.LogError("TerrainChunk::Load() - TerrainChunkMesh is not a valid MeshInstance3D");
            }
    }

    /// <summary>
    /// Applies a texture to the terrain chunk's mesh and configures shader parameters
    /// for Web Mercator to WGS84 reprojection. The shader transforms the flat map
    /// projection into the correct spherical coordinates for the planet's surface.
    /// Also applies a scale correction to handle east-west texture inversion.
    /// </summary>
    /// <param name="texture2D">The texture to apply to the terrain.</param>
    private void ApplyTexture(Texture2D texture2D)
    {
        var standardShader = new StandardMaterial3D();
        standardShader.AlbedoTexture = texture2D;
        if (!GodotUtils.IsValid(TerrainChunkMesh))
        {
            Logger.LogError(
                "TerrainChunk::ApplyTexture: TerrainChunk texture could not be loaded! The TerrainChunkMesh is invalid!");
            return;
        }

        TerrainChunkMesh.MaterialOverride = standardShader;
    }

    private async Task InitializeTerrainChunkAsync()
    {
        MapTile mapTile = await MapAPI.RequestMapTileAsync(
            (float)MapTile.Latitude,
            (float)MapTile.Longitude,
            MapTile.ZoomLevel,
            MapTile.MapType,
            MapTile.MapImageType,
            MapTile.MapTileType
        );

        if (mapTile == null)
        {
            throw new Exception("Failed to initialize map tile");
        }

        ApplyTexture(mapTile.Texture2D);
    }

}