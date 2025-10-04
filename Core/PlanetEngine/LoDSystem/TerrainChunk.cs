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
using Gaia.PlanetEngine.MapDataRetrieval;
using Gaia.PlanetEngine.MapTiles;
using Godot;
using Daedalus.Logging;
using Daedalus.GodotUtils;

namespace Gaia.PlanetEngine.LoDSystem;

/// <summary>
///   Represents a single chunk of planetary terrain in a quadtree structure.
///   Handles loading and display of map tiles from a Web Mercator projection, reprojecting them
///   onto an ellipsoidal surface. Each chunk knows its position (lat/lon in radians) and coverage area.
///   The reference frame of the TerrainChunk is determined by the underlying map tile
/// </summary>
public partial class TerrainChunk : Node3D
{
  [Signal]
  public delegate void TerrainChunkLoadedEventHandler();

  public TerrainChunk(MapTile mapTile)
  {
    if (mapTile == null)
    {
      throw new ArgumentNullException("Cannot create a TerrainChunk with a null map tile");
    }

    MapTile = mapTile;
    Logger.RegisterLogging(this, true);
  }

  public MapTile MapTile { get; }

  public MeshInstance3D TerrainChunkMesh { get; private set; }

  public ShaderMaterial ShaderMaterial { get; private set; }


  public MeshInstance3D MeshInstance
  {
    get => TerrainChunkMesh;
    set => TerrainChunkMesh = value;
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
      Logger.LogError(this, "TerrainChunk::Load() - TerrainChunkMesh is not a valid MeshInstance3D");
    }
  }

  private void ApplyTexture(Texture2D texture2D)
  {
    var standardShader = new StandardMaterial3D();
    standardShader.TextureFilter = BaseMaterial3D.TextureFilterEnum.Nearest;

    standardShader.AlbedoTexture = texture2D;
    if (!GodotUtils.IsValid(TerrainChunkMesh))
    {
      Logger.LogError(this,
        "TerrainChunk::ApplyTexture: TerrainChunk texture could not be loaded! The TerrainChunkMesh is invalid!");
      return;
    }

    TerrainChunkMesh.MaterialOverride = standardShader;
    EmitSignal(SignalName.TerrainChunkLoaded);
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
      Logger.LogError(this,
        $"==========================================================================\n" +
        $"TerrainChunk::InitializeTerrainChunkAsync() - MapTile could not be loaded!\n" +
        $"Latitude: {MapTile.Latitude}\n" +
        $"Longitude: {MapTile.Longitude}\n" +
        $"Zoom: {MapTile.ZoomLevel}\n" +
        $"MapType: {MapTile.MapType}"
      );
      return;
    }

    ApplyTexture(mapTile.Texture2D);
  }

  public override void _Process(double delta)
  {
    if (MapTile.LatitudeTileCoo == 0 && MapTile.LongitudeTileCoo == 0 && this.MapTile.ZoomLevel > 19)
    {
      this.LogInfo($"Map Tile 0,0 has position: {this.GlobalPosition.X},{this.GlobalPosition.Y},{this.GlobalPosition.Z}");
    }
  }
}
