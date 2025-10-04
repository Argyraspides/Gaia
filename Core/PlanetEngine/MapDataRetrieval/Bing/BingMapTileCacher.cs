using System;
using System.IO;
using Godot;
using FileAccess = Godot.FileAccess;
using Daedalus.Networking.Cache;

namespace Gaia.PlanetEngine.MapDataRetrieval.Bing;

using FileAccess = FileAccess;

public class BingMapTileCacher : ICacheCapability<BingMercatorMapTile>
{
  /// <summary>
  ///   Contains the default high-resolution Earth textures
  /// </summary>
  private readonly string _defaultCacheFolderPath =
    "res://SolarSystem/Scenes/Earth/Assets/MapTiles";

  /// <summary>
  ///   Bing map tile cache, located in the user:// directory. This is the path to cache all the map tiles that the
  ///   user has accumulated over the runtime of Hermes
  /// </summary>
  private readonly string _userCacheFolderPath = Path.Combine(OS.GetUserDataDir(), "BingMapProvider", "Cache");

  public BingMapTileCacher()
  {
    if (!Directory.Exists(_userCacheFolderPath))
    {
      Directory.CreateDirectory(_userCacheFolderPath);
      GD.Print("Cache directory created at: " + _userCacheFolderPath);
    }
  }


  public void CacheResource(BingMercatorMapTile resource)
  {
    string filePathOfMapTile = Path.Combine(_userCacheFolderPath, resource.ResourcePath);
    string directoryPathOfMapTile = Path.GetDirectoryName(filePathOfMapTile);
    if (!Directory.Exists(directoryPathOfMapTile))
    {
      try
      {
        // TODO:: what to do if null? DirectoryPathofmpatile is nullable ...
        Directory.CreateDirectory(directoryPathOfMapTile);
      }
      catch (Exception e)
      {
        GD.PrintErr("Error creating directory: " + directoryPathOfMapTile + "\n" + e.Message);
      }
    }

    using var file = FileAccess.Open(filePathOfMapTile, FileAccess.ModeFlags.Write);
    if (file == null)
    {
      GD.PrintErr(
        "Unable to cache bing mercator map tile. " +
        "The file path may be invalid, you may have inappropriate permissions, " +
        "or the file is currently being accessed by another resource");
      return;
    }

    file.StoreBuffer(resource.ResourceData);
  }

  public BingMercatorMapTile RetrieveResourceFromCache(string resourceHash) => throw new NotImplementedException();

  public bool ResourceExists(string resourceHash)
  {
    // Check both the user cache for map tiles cached during runtime, and default cache
    // for pre-bundled textures
    return
      File.Exists(Path.Combine(_userCacheFolderPath, resourceHash)) ||
      File.Exists(Path.Combine(_defaultCacheFolderPath, resourceHash));
  }

  public BingMercatorMapTile RetrieveResourceFromCache(BingMercatorMapTile partialResource)
  {
    if (!ResourceExists(partialResource))
    {
      throw new FileNotFoundException(
        "Unable to retrieve bing mercator map tile from cache. Map tile doesn't exist");
    }

    // Check the pre-bundled high-resolution texture path
    string filePath = Path.Combine(_defaultCacheFolderPath, partialResource.ResourcePath);
    var bingMercatorMapTile = new BingMercatorMapTile();
    if (FileAccess.FileExists(filePath))
    {
      using var file = FileAccess.Open(filePath, FileAccess.ModeFlags.Read);
      bingMercatorMapTile = new BingMercatorMapTile(
        partialResource.QuadKey,
        partialResource.MapType,
        partialResource.Language,
        partialResource.MapImageType,
        file.GetBuffer((long)file.GetLength())
      );
    }

    // Check user cache if the high resolution texture doesn't exist
    filePath = Path.Combine(_userCacheFolderPath, partialResource.Hash);
    if (FileAccess.FileExists(filePath))
    {
      using var file = FileAccess.Open(filePath, FileAccess.ModeFlags.Read);
      bingMercatorMapTile = new BingMercatorMapTile(
        partialResource.QuadKey,
        partialResource.MapType,
        partialResource.Language,
        partialResource.MapImageType,
        file.GetBuffer((long)file.GetLength())
      );
    }

    return bingMercatorMapTile;
  }

  public bool ResourceExists(BingMercatorMapTile partialResource)
  {
    string filePath = Path.Combine(_defaultCacheFolderPath, partialResource.ResourcePath);
    if (FileAccess.FileExists(filePath))
    {
      return true;
    }

    filePath = Path.Combine(_userCacheFolderPath, partialResource.Hash);
    if (FileAccess.FileExists(filePath))
    {
      return true;
    }

    return false;
  }

  public string GenerateResourcePath(BingMercatorMapTile resource) => throw new NotImplementedException();
}
