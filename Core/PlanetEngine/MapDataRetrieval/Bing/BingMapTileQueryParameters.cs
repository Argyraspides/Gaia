using System;
using System.Collections.Generic;
using Gaia.PlanetEngine.MapTiles;
using Daedalus.Enums;
using Daedalus.Networking.HTTP;

namespace Gaia.PlanetEngine.MapDataRetrieval.Bing;

public class BingMapTileQueryParameters : IQueryParameters
{
  public const int MinimumServerInstance = 0;
  public const int MaximumServerInstance = 3;

  public BingMapTileQueryParameters(
    int serverInstance,
    MapType mapType,
    string quadKey,
    ImageType mapImgType,
    string apiVersion,
    HumanLanguage language
  )
  {
    if (serverInstance < 0 || serverInstance >= 4)
    {
      throw new ArgumentOutOfRangeException(
        nameof(serverInstance),
        "Server instance must be between 0 and 3"
      );
    }

    if (string.IsNullOrEmpty(quadKey))
    {
      throw new ArgumentException(
        "QuadKey cannot be null or empty",
        nameof(quadKey)
      );
    }

    if (string.IsNullOrEmpty(apiVersion))
    {
      throw new ArgumentException(
        "API version cannot be null or empty",
        nameof(apiVersion)
      );
    }

    ServerInstance = serverInstance;
    MapType = mapType;
    QuadKey = quadKey;
    MapImageType = mapImgType;
    ApiVersion = apiVersion;
    Language = language;
  }

  /*

  The Bing maps API query URL looks like this:
  "https://ecn.t{serverInstance}.tiles.virtualearth.net/tiles/{mapType}{quadKey}.{mapImgType}?g={apiVersion}&mkt={lang}";

  */

  public int ServerInstance { get; }
  public MapType MapType { get; }
  public string QuadKey { get; }
  public ImageType MapImageType { get; }
  public string ApiVersion { get; }
  public HumanLanguage Language { get; }

  public IDictionary<string, string> ToQueryDictionary()
  {
    var queryParams = new Dictionary<string, string>();

    queryParams[BingMapTileURLBuilder.ServerInstanceStr] = ServerInstance.ToString();
    queryParams[BingMapTileURLBuilder.MapTypeStr] = MapTypeToQueryParam(MapType);
    queryParams[BingMapTileURLBuilder.QuadKeyStr] = QuadKey;
    queryParams[BingMapTileURLBuilder.MapImgTypeStr] = MapImageType.ToString();
    queryParams[BingMapTileURLBuilder.ApiVersionStr] = ApiVersion;
    queryParams[BingMapTileURLBuilder.LangStr] = Language.ToString().ToLower();

    return queryParams;
  }

  private static string MapTypeToQueryParam(MapType mapType) => mapType switch
  {
    MapType.Satellite => "a",
    MapType.Street => "r",
    MapType.Hybrid => "h",
    _ => "a"
  };
}
