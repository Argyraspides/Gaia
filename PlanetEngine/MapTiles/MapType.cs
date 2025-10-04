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

namespace Gaia.PlanetEngine.MapTiles;

public enum MapType
{
  Satellite,
  Street,
  Hybrid,

  Unknown
}

public enum MapTileType
{
  WebMercatorEarth, // Standard, FLAT web-mercator projection
  WebMercatorWgs84, // Web mercator projection projected back onto a WGS84 ellipsoid using custom shader
  Mercury,
  Venus,
  Mars,
  Jupiter,
  Saturn,
  Uranus,
  Neptune,

  Unknown
}
