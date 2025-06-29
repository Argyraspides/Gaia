using System;
using System.Net.Http;
using System.Threading.Tasks;
using Gaia.PlanetEngine.MapTiles;

namespace Gaia.PlanetEngine.MapDataRetrieval.Bing;

public class BingMapProvider : IMapProvider<BingMapTileQueryParameters>
{
  private readonly BingMapTileCacher _bingMapTileCacher;

  public BingMapProvider()
  {
    _bingMapTileCacher = new BingMapTileCacher();
  }

  public async Task<byte[]> RequestRawMapTile(BingMapTileQueryParameters queryParameters) =>
    throw new NotImplementedException();

  public async Task<MapTile> RequestMapTile(BingMapTileQueryParameters queryParameters)
  {
    // Check if resource already exists and return the cached map tile if it does
    // This partialTile contains enough information to uniquely identify a map tile in the cache
    var partialTile = new BingMercatorMapTile(
      queryParameters.QuadKey,
      queryParameters.MapType,
      queryParameters.Language,
      queryParameters.MapImageType
    );

    if (_bingMapTileCacher.ResourceExists(partialTile))
    {
      BingMercatorMapTile mapTileResource = _bingMapTileCacher.RetrieveResourceFromCache(partialTile);
      return mapTileResource;
    }

    // Otherwise, query Bing (Microsoft is the GOAT right? I make fun of them yet here
    // I am using C# and the .NET framework)
    var bingMapTileUrlBuilder = new BingMapTileURLBuilder();
    string url = bingMapTileUrlBuilder.BuildUrl(queryParameters);

    byte[] rawMapData = await new HttpClient().GetByteArrayAsync(url);

    // TODO:: Do some error handling here to make sure you don't cache map tiles that are
    // fucked
    var bingMercatorMapTile = new BingMercatorMapTile(
      queryParameters.QuadKey,
      queryParameters.MapType,
      queryParameters.Language,
      queryParameters.MapImageType,
      rawMapData
    );

    _bingMapTileCacher.CacheResource(bingMercatorMapTile);

    return bingMercatorMapTile;
  }
}
