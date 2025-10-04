using Gaia.PlanetEngine.MapTiles;
using Gaia.PlanetEngine.Utils;
using Godot;
using Daedalus.Enums;
using Daedalus.Images;

namespace Gaia.PlanetEngine.MapDataRetrieval.Bing;

public class BingMercatorMapTile : MapTile
{
  /// <summary>
  ///   This constructor can be used if one wants to provide the minimum amount of information necessary to uniquely identify
  ///   a tile
  /// </summary>
  /// <param name="quadKey"></param>
  /// <param name="imageData"></param>
  /// <param name="mapType"></param>
  /// <param name="imageType"></param>
  /// <param name="language"></param>
  public BingMercatorMapTile(
    string quadKey,
    MapType mapType = MapType.Unknown,
    HumanLanguage language = HumanLanguage.UNKNOWN,
    ImageType imageType = ImageType.Unknown,
    byte[] imageData = null
  )
  {
    MapType = mapType;
    Language = language;
    MapImageType = imageType == ImageType.Unknown ? ImageUtils.GetImageFormat(imageData) : imageType;

    QuadKey = quadKey;

    // Extract zoom level from quadkey length
    ZoomLevel = quadKey.Length;

    // Convert tile coordinates to lat/lon
    (Latitude, Longitude, ZoomLevel) = PlanetUtils.QuadKeyToLatLonAndZoom(quadKey);

    LatitudeTileCoo = PlanetUtils.LatToTileCooWebMercator(Latitude, ZoomLevel);
    LongitudeTileCoo = PlanetUtils.LonToTileCooWebMercator(Longitude, ZoomLevel);

    LatitudeRange = PlanetUtils.TileToLatRangeWebMercator(LatitudeTileCoo, ZoomLevel);
    LongitudeRange = PlanetUtils.TileToLonRangeWebMercator(LongitudeTileCoo);

    if (imageData != null)
    {
      (Width, Height) = ImageUtils.GetImageDimensions(imageData);
      Texture2D = ImageUtils.ByteArrayToImageTexture(imageData);
    }
    else
    {
      var placeHolderTexture = new PlaceholderTexture2D();
      placeHolderTexture.Size = new Vector2I(256, 256);
      Texture2D = placeHolderTexture;
    }

    ResourceData = imageData;
    ResourcePath = Hash;
  }

  public BingMercatorMapTile()
  {
    ResourcePath = Hash;
  }

  public string QuadKey { get; private set; }

  public override string GenerateHashCore()
  {
    string format = "Bing/{0}/{1}/{2}/{3}/tile_{4}_{5}.{6}";
    return string.Format(format,
      MapType,
      MapImageType.ToString().ToLower(),
      Language,
      ZoomLevel,
      LongitudeTileCoo,
      LatitudeTileCoo,
      MapImageType.ToString().ToLower());
  }

  public override bool IsHashable()
  {
    return MapType != MapType.Unknown
           && MapImageType != ImageType.Unknown
           && Language != HumanLanguage.UNKNOWN
           && ZoomLevel > 0
           && LongitudeTileCoo >= 0
           && LongitudeTileCoo < int.MaxValue
           && LatitudeTileCoo >= 0
           && LatitudeTileCoo < int.MaxValue;
  }
}
