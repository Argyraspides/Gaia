/*




88        88  88888888888  88888888ba   88b           d88  88888888888  ad88888ba
88        88  88           88      "8b  888b         d888  88          d8"     "8b
88        88  88           88      ,8P  88`8b       d8'88  88          Y8,
88aaaaaaaa88  88aaaaa      88aaaaaa8P'  88 `8b     d8' 88  88aaaaa     `Y8aaaaa,
88""""""""88  88"""""      88""""88'    88  `8b   d8'  88  88"""""       `"""""8b,
88        88  88           88    `8b    88   `8b d8'   88  88                  `8b
88        88  88           88     `8b   88    `888'    88  88          Y8a     a8P
88        88  88888888888  88      `8b  88     `8'     88  88888888888  "Y88888P"


                            MESSENGER OF THE MACHINES

*/


using System;
using System.Threading.Tasks;
using Gaia.Common;
using Gaia.Common.Enums;
using Gaia.PlanetEngine.MapDataRetrieval.Bing;
using Gaia.PlanetEngine.MapTiles;
using Gaia.PlanetEngine.Utils;

namespace Gaia.PlanetEngine.MapDataRetrieval;

public static class MapAPI
{

    private const string DEFAULT_API_VERSION_BING = "523";
    private static BingMapProvider m_bingMapProvider = new BingMapProvider();


    // Requests a map tile at a particular latitude/longitude at a specified zoom level (degrees), with a map type
    // (e.g., satellite, street, hybrid, etc.), and an image type (PNG, JPG, etc.).
    // To understand map tiling, see: https://www.microimages.com/documentation/TechGuides/78BingStructure.pdf
    public static async Task<MapTile> RequestMapTileAsync(
        float latitude,
        float longitude,
        int zoom,
        MapType mapType,
        ImageType mapImageType,
        MapTileType mapTileType
    )
    {
        switch (mapTileType)
        {
            case MapTileType.WEB_MERCATOR_WGS84:
            case MapTileType.WEB_MERCATOR_EARTH:
                return await RequestBingWebMercatorMapTile(
                    latitude,
                    longitude,
                    zoom,
                    mapType,
                    mapImageType
                );
            default:
                return null;
        }
    }


    public static async Task<MapTile> RequestBingWebMercatorMapTile(
        float latitude,
        float longitude,
        int zoom,
        MapType mapType,
        ImageType mapImageType)
    {

        // Bings API has a load balancer so we choose a random server here to prevent overloading any
        // individual one
        int serverInstance =
            new Random().Next(
                BingMapTileQueryParameters.MINIMUM_SERVER_INSTANCE,
                BingMapTileQueryParameters.MAXIMUM_SERVER_INSTANCE
            );

        BingMapTileQueryParameters bingQueryParameters = new BingMapTileQueryParameters(
            serverInstance,
            mapType,
            PlanetUtils.LatLonAndZoomToQuadKey(latitude, longitude, zoom),
            mapImageType,
            DEFAULT_API_VERSION_BING,
            HumanLanguage.en
        );

        return await m_bingMapProvider.RequestMapTileAsync(bingQueryParameters);
    }

}
