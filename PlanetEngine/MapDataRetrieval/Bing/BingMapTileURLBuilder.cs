using System.Collections.Generic;
using Daedalus.Networking.HTTP;

namespace Gaia.PlanetEngine.MapDataRetrieval.Bing;

/// <summary>
///   Builds a URL for querying map tiles from the Bing backend API. This is the single source of truth for
///   URL parameters
///   See: https://learn.microsoft.com/en-us/bingmaps/rest-services/directly-accessing-the-bing-maps-tiles
///   TODO::WARNING(Argyraspides, 06/02/2025) The URL that this class uses will be deprecated in June. Fix it up before
///   then!
/// </summary>
public class BingMapTileURLBuilder : IUrlBuilder<BingMapTileQueryParameters>
{
  public const string ServerInstanceStr = "serverInstance";
  public const string MapTypeStr = "mapType";
  public const string QuadKeyStr = "quadKey";
  public const string MapImgTypeStr = "mapImgType";
  public const string ApiVersionStr = "apiVersion";
  public const string LangStr = "lang";

  public readonly string UrlTemplate =
    $"https://ecn.t{{{ServerInstanceStr}}}.tiles.virtualearth.net/tiles/{{{MapTypeStr}}}{{{QuadKeyStr}}}.{{{MapImgTypeStr}}}?g={{{ApiVersionStr}}}&mkt={{{LangStr}}}";

  public string BuildUrl(BingMapTileQueryParameters parameters)
  {
    IDictionary<string, string> parameterKvp = parameters.ToQueryDictionary();
    string finalURL = UrlTemplate;

    foreach (KeyValuePair<string, string> kvp in parameterKvp)
    {
      finalURL = finalURL.Replace("{" + kvp.Key + "}", kvp.Value);
    }

    return finalURL;
  }
}
