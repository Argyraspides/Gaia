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

namespace Gaia.PlanetEngine.Utils;

using System;
using System.Text;
using Godot;

/// <summary>
/// This class just holds a bunch of static functions and definitions for anything map related.
/// E.g., converting latitude/longitude to tile coordinates, specifying map types, constructing quadrant keys,
/// image manipulation of certain map tile projections such that they can be fitted again to a 3D sphere, etc.
/// We speak the language of Mathematics, and as such, all angles are in radians unless it is impossible
/// to work with
/// </summary>
public static partial class PlanetUtils
{
    public const double PI = Math.PI;
    public const double TWO_PI = PI * 2.0;

    /// <summary>
    /// These aren't necessarily a universal constant. You can stop the Web Mercator projection at
    /// any latitude you want besides the poles themselves which stretch to infinity. These
    /// are just constants that most map providers use
    /// </summary>
    public const double MIN_LATITUDE_LEVEL_WEB_MERCATOR = -1.484422229745;

    public const double MAX_LATITUDE_LEVEL_WEB_MERCATOR = 1.484422229745;

    public const double MIN_LATITUDE = -Math.PI / 2.0;
    public const double MAX_LATITUDE = Math.PI / 2.0;

    public const double MIN_LONGITUDE = -Math.PI;
    public const double MAX_LONGITUDE = Math.PI;

    public const double RADIANS_TO_DEGREES = 180.0 / PI;
    public const double DEGREES_TO_RADIANS = PI / 180.0;

    /// <summary>
    /// Converts line of latitude (radians) to a latitude tile coordinate (y axis) on the Mercator projection,
    /// using a quadtree to represent the map (each successive zoom level doubles the tiles on the X and Y axis).
    /// To understand map tiling, see: https://www.microimages.com/documentation/TechGuides/78BingStructure.pdf
    ///
    /// Formula from:
    /// https://wiki.openstreetmap.org/wiki/Slippy_map_tilenames#2._Convert_the_coordinate_to_the_Web_Mercator_projection_(https://epsg.io/3857)
    /// </summary>
    public static int LatitudeToTileCoordinateMercator(double lat, int zoom)
    {
        if (zoom == 0)
        {
            return 0;
        }

        lat = Math.Clamp(
            lat,
            MIN_LATITUDE_LEVEL_WEB_MERCATOR,
            MAX_LATITUDE_LEVEL_WEB_MERCATOR
        );

        double tanExpr = Math.Tan(lat);
        double secExpr = 1.0 / Math.Cos(lat);
        double lnExpr = Math.Log(tanExpr + secExpr);

        double divisionExpr = lnExpr / PI;

        double bracketExpr = 1.0 - divisionExpr;

        double finalExpr = bracketExpr * (1 << (zoom - 1));

        return (int)Math.Floor((finalExpr >= (1 << zoom)) ? (1 << zoom) - 1 : finalExpr);
    }

    /// <summary>
    /// Converts line of longitude (radians) to a longitude tile coordinate (y axis) on the Mercator projection,
    /// using a quadtree to represent the map (each successive zoom level doubles the tiles on the X and Y axis).
    /// To understand map tiling, see: https://www.microimages.com/documentation/TechGuides/78BingStructure.pdf
    ///
    /// Formula from:
    /// https://wiki.openstreetmap.org/wiki/Slippy_map_tilenames#2._Convert_the_coordinate_to_the_Web_Mercator_projection_(https://epsg.io/3857)
    /// </summary>
    public static int LongitudeToTileCoordinateMercator(double lon, int zoom)
    {
        int tilesPerSide = 1 << zoom;

        double numeratorExpr = lon + PI;
        double denominatorExpr = TWO_PI;

        double divisionExpr = numeratorExpr / denominatorExpr;

        double finalExpr = divisionExpr * tilesPerSide;

        return (int)Math.Floor((finalExpr >= tilesPerSide) ? (tilesPerSide - 1) : finalExpr);
    }


    /// <summary>
    /// Converts a map tile's x-coordinate to the corresponding line of longitude (radians)
    /// on the Mercator projection, using the Web Mercator tiling system.
    /// Each successive zoom level doubles the number of tiles along both the x and y axes,
    /// and tiles are indexed starting from the top-left of the map (0,0) at zoom level 0.
    /// Formula from:
    /// https://wiki.openstreetmap.org/wiki/Slippy_map_tilenames#Tile_numbers_to_lon..2Flat._2
    /// </summary>
    /// <param name="tx">Tile x-coordinate</param>
    /// <param name="zoom">Zoom level (determines the total number of tiles at this zoom)</param>
    /// <returns>Longitude of the tile's western edge, in radians.</returns>
    public static double TileCoordinateToLongitude(int tx, int zoom)
    {
        return (double)tx / (1 << zoom) * TWO_PI - PI;
    }

    /// <summary>
    /// Converts a map tile's y-coordinate to the corresponding line of latitude (radians)
    /// on the Mercator projection, using the Web Mercator tiling system.
    /// Each successive zoom level doubles the number of tiles along both the x and y axes,
    /// and tiles are indexed starting from the top-left of the map (0,0) at zoom level 0.
    /// Formula from:
    /// https://wiki.openstreetmap.org/wiki/Slippy_map_tilenames#Tile_numbers_to_lon..2Flat._2
    /// </summary>
    /// <param name="ty">Tile y-coordinate</param>
    /// <param name="zoom">Zoom level (determines the total number of tiles at this zoom)</param>
    /// <returns>Latitude of the tile's northern edge, in radians.</returns>
    public static double TileCoordinateToLatitude(int ty, int zoom)
    {
        double n = PI - (2.0 * PI * ty / (1 << zoom));
        return Math.Atan(0.5 * (Math.Exp(n) - Math.Exp(-n)));
    }

    /// <summary>
    /// Calculates the geographical bounds (latitude and longitude) of a specific map tile
    /// at a given zoom level on the Mercator projection. The bounds are defined by the
    /// minimum and maximum latitudes and longitudes that the tile covers.
    ///
    /// Each map tile is a rectangular region on the Earth's surface, and its size
    /// decreases as the zoom level increases (each successive zoom level doubles the
    /// number of tiles along both x and y axes).
    /// </summary>
    /// <param name="tx">Tile x-coordinate (longitude direction)</param>
    /// <param name="ty">Tile y-coordinate (latitude direction)</param>
    /// <param name="zoom">Zoom level (determines the total number of tiles at this zoom)</param>
    /// <returns>
    /// A tuple containing:
    /// - latMin: Minimum latitude (southern edge of the tile), in radians
    /// - latMax: Maximum latitude (northern edge of the tile), in radians
    /// - lonMin: Minimum longitude (western edge of the tile), in radians
    /// - lonMax: Maximum longitude (eastern edge of the tile), in radians
    /// </returns>
    public static (double latMin, double latMax, double lonMin, double lonMax) GetTileLatLonBounds(int tx, int ty,
        int zoom)
    {
        double lonMin = TileCoordinateToLongitude(tx, zoom);
        double lonMax = TileCoordinateToLongitude(tx + 1, zoom);
        double latMin = TileCoordinateToLatitude(ty + 1, zoom);
        double latMax = TileCoordinateToLatitude(ty, zoom);
        return (latMin, latMax, lonMin, lonMax);
    }


    /// <summary>
    /// Converts *TILE* coordinates (x,y) for a line of latitude (y) and line of longitude (x)
    /// at a particular zoom level to a quadkey.
    ///
    /// To understand quadkeys, see: https://www.microimages.com/documentation/TechGuides/78BingStructure.pdf
    /// This kind of tile indexing is used mainly for the Bing maps API
    /// </summary>
    public static string TileCoordinatesToQuadkey(int x, int y, int zoom)
    {
        int maxTile = (1 << zoom) - 1;
        if (x < 0 || x > maxTile || y < 0 || y > maxTile)
        {
            throw new ArgumentException($"Tile coordinates {x},{y} are not valid for zoom level {zoom}");
        }

        StringBuilder quadkey = new StringBuilder();

        int i = zoom;
        while (i > 0)
        {
            i--;
            int digit = 0x00;
            int mask = 1 << i;

            if ((x & mask) != 0)
                digit += 1;
            if ((y & mask) != 0)
                digit += 2;
            quadkey.Append(digit.ToString());
        }

        return quadkey.ToString();
    }

    /// <summary>
    /// Converts a latitude and longitude at a particular zoom level to a quadkey.
    ///
    /// To understand quadkeys, see: https://www.microimages.com/documentation/TechGuides/78BingStructure.pdf
    /// This kind of tile indexing is used mainly for the Bing maps API
    ///
    /// </summary>
    /// <param name="lat"></param>
    /// <param name="lon"></param>
    /// <param name="zoom"></param>
    /// <returns></returns>
    public static string LatLonAndZoomToQuadKey(double lat, double lon, int zoom)
    {
        int latTileCoo = LatitudeToTileCoordinateMercator(lat, zoom);
        int lonTileCoo = LongitudeToTileCoordinateMercator(lon, zoom);
        return TileCoordinatesToQuadkey(lonTileCoo, latTileCoo, zoom);
    }

    /// <summary>
    /// Computes the center latitude of a tile given its row index and zoom level.
    /// </summary>
    public static double ComputeCenterLatitude(int latTileCoo, int zoom)
    {
        double northEdge = TileCoordinateToLatitude(latTileCoo, zoom);
        double latRange = TileToLatRange(latTileCoo, zoom);
        return northEdge - latRange / 2;
    }

    /// <summary>
    /// Computes the center longitude of a tile given its column index and zoom level.
    /// </summary>
    public static double ComputeCenterLongitude(int lonTileCoo, int zoom)
    {
        double westEdge = TileCoordinateToLongitude(lonTileCoo, zoom);
        double lonRange = TileToLonRange(zoom);
        return westEdge + lonRange / 2;
    }


    /// <summary>
    /// Returns the number of radians of latitude that a tile spans at a given tile row and zoom level.
    /// </summary>
    public static double TileToLatRange(int tileY, int zoom)
    {
        if (zoom == 0)
        {
            return PI;
        }

        // Compute the top (northern) latitude of the tile
        double latTop = TileCoordinateToLatitude(tileY, zoom);

        // Compute the bottom (southern) latitude of the tile, which is just
        // the northern part of the tile below us
        double latBottom = TileCoordinateToLatitude(tileY + 1, zoom);

        // The difference in latitude (in radians) is:
        return latTop - latBottom;
    }

    /// <summary>
    /// Returns the number of radians of longitude that a tile spans at a given zoom level.
    /// </summary>
    public static double TileToLonRange(int zoom)
    {
        // The full 360° (2π) of longitude is divided evenly among 2^zoom tiles.
        return TWO_PI / (1 << zoom);
    }


    /// <summary>
    /// Converts latitude and longitude from radians to a normalized Cartesian coordinate
    /// in a Earth-Centered, Earth-Fixed (ECEF) system based on the WGS84 ellipsoid.
    ///
    /// The function returns coordinates normalized to the Earth's semi-major axis (equatorial radius),
    /// which is assigned a length of 1.0, with the semi-minor axis (polar radius) scaled proportionally.
    ///
    /// Input assumptions:
    /// - Latitude range: [-π/2, π/2] (South Pole to North Pole)
    /// - Longitude range: [-π, π] (180°W to 180°E)
    /// - Null island (0,0) lies on the +ve Z-axis in the Godot coordinate system
    /// - Increasing longitude corresponds to eastward movement
    /// </summary>
    /// <param name="lat">Latitude in radians, range [-π/2, π/2]</param>
    /// <param name="lon">Longitude in radians, range [-π, π]</param>
    /// <returns>Normalized Cartesian coordinates as a Vector3</returns>
    public static Vector3 LatLonToCartesianNormalizedWGS84(double lat, double lon)
    {
        lat -= Math.PI / 2.0d;
        lon += Math.PI;

        double a = 1;
        double b = 1;
        double c =
            EARTH_POLAR_RADIUS_KM /
            EARTH_EQUATORIAL_RADIUS_KM;

        double zCoo = a * Math.Sin(lat) * Math.Cos(lon);
        double xCoo = b * Math.Sin(lat) * Math.Sin(lon);
        double yCoo = c * Math.Cos(lat);

        return new Vector3(
            (float)xCoo,
            (float)yCoo,
            (float)zCoo
        );
    }

    public static Vector3 LatLonToCartesian(double lat, double lon, PlanetShapeType pst)
    {
        switch (pst)
        {
            case PlanetShapeType.EARTH_WGS84:
                return LatLonToCartesianWGS84(lat, lon);
        }

        throw new InvalidOperationException("Unknown planet shape type!");
    }

    public static Vector3 LatLonToCartesianNormalized(double lat, double lon, PlanetTileType tileType)
    {
        switch (tileType)
        {
            case PlanetTileType.WEB_MERCATOR_WGS84:
                return LatLonToCartesianNormalizedWGS84(lat, lon);
        }

        throw new InvalidOperationException("Unknown planet shape type!");
    }

    /// <summary>
    /// Converts latitude and longitude from radians to actual Cartesian coordinates
    /// in kilometers in a Earth-Centered, Earth-Fixed (ECEF) system based on the WGS84 ellipsoid.
    ///
    /// Input assumptions:
    /// - Latitude range: [-π/2, π/2] (South Pole to North Pole)
    /// - Longitude range: [-π, π] (180°W to 180°E)
    /// - Null island (0,0) lies on the +ve Z-axis in the Godot coordinate system
    /// - Increasing longitude corresponds to eastward movement
    /// </summary>
    /// <param name="lat">Latitude in radians, range [-π/2, π/2]</param>
    /// <param name="lon">Longitude in radians, range [-π, π]</param>
    /// <returns>Cartesian coordinates in kilometers as a Vector3</returns>
    public static Vector3 LatLonToCartesianWGS84(double lat, double lon)
    {
        lat -= Math.PI / 2.0;
        lon += Math.PI;

        double a = EARTH_EQUATORIAL_RADIUS_KM;
        double b = a;
        double c = EARTH_POLAR_RADIUS_KM;

        double zCoo = a * Math.Sin(lat) * Math.Cos(lon);
        double xCoo = b * Math.Sin(lat) * Math.Sin(lon);
        double yCoo = c * Math.Cos(lat);

        return new Vector3(
            (float)xCoo,
            (float)yCoo,
            (float)zCoo
        );
    }

    /// <summary>
    /// Calculates the normalized X coordinate for a point on the WGS84 ellipsoid
    /// at the given latitude and longitude.
    ///
    /// The X axis points eastward at equator-prime meridian intersection.
    /// The value is normalized to make the major axis (equatorial radius) of length 1.0.
    ///
    /// Input assumptions:
    /// - Latitude range: [-π/2, π/2] (South Pole to North Pole)
    /// - Longitude range: [-π, π] (180°W to 180°E)
    /// - Null island (0,0) lies on the +ve Z-axis in the Godot coordinate system
    /// </summary>
    /// <param name="lat">Latitude in radians, range [-π/2, π/2]</param>
    /// <param name="lon">Longitude in radians, range [-π, π]</param>
    /// <returns>Normalized X coordinate</returns>
    public static double LatLonToCartesianWGS84X(double lat, double lon)
    {
        lat -= Math.PI / 2.0;
        lon += Math.PI;
        return Math.Sin(lat) * Math.Sin(lon);
    }

    /// <summary>
    /// Calculates the normalized Y coordinate for a point on the WGS84 ellipsoid
    /// at the given latitude.
    ///
    /// The Y axis points toward the North Pole.
    /// The value is scaled by the ratio of semi-minor to semi-major axis to accurately
    /// represent Earth's oblate spheroid shape, with the major axis normalized to length 1.0.
    ///
    /// Input assumptions:
    /// - Latitude range: [-π/2, π/2] (South Pole to North Pole)
    /// </summary>
    /// <param name="lat">Latitude in radians, range [-π/2, π/2]</param>
    /// <returns>Normalized Y coordinate</returns>
    public static double LatLonToCartesianWGS84Y(double lat)
    {
        lat -= Math.PI / 2.0;
        double minorToMajorRatio = EARTH_POLAR_RADIUS_KM /
                                   EARTH_EQUATORIAL_RADIUS_KM;
        return minorToMajorRatio * Math.Cos(lat);
    }

    /// <summary>
    /// Calculates the normalized Z coordinate for a point on the WGS84 ellipsoid
    /// at the given latitude and longitude.
    ///
    /// The Z axis points toward the prime meridian at the equator (null island).
    /// The value is normalized to make the major axis (equatorial radius) of length 1.0.
    ///
    /// Input assumptions:
    /// - Latitude range: [-π/2, π/2] (South Pole to North Pole)
    /// - Longitude range: [-π, π] (180°W to 180°E)
    /// - Null island (0,0) lies on the +ve Z-axis in the Godot coordinate system
    /// </summary>
    /// <param name="lat">Latitude in radians, range [-π/2, π/2]</param>
    /// <param name="lon">Longitude in radians, range [-π, π]</param>
    /// <returns>Normalized Z coordinate</returns>
    public static double LatLonToCartesianWGS84Z(double lat, double lon)
    {
        lat -= Math.PI / 2.0;
        lon += Math.PI;
        return Math.Sin(lat) * Math.Cos(lon);
    }

    /// <summary>
    /// Converts latitude, longitude, and altitude to actual Cartesian coordinates
    /// in kilometers in a Earth-Centered, Earth-Fixed (ECEF) system based on the WGS84 ellipsoid.
    ///
    /// The altitude is measured in kilometers from the ellipsoid surface, with positive values
    /// representing points above the surface and negative values representing points below it.
    ///
    /// Input assumptions:
    /// - Latitude range: [-π/2, π/2] (South Pole to North Pole)
    /// - Longitude range: [-π, π] (180°W to 180°E)
    /// - Altitude in kilometers
    /// - Null island (0,0) lies on the +ve Z-axis in the Godot coordinate system
    /// - Increasing longitude corresponds to eastward movement
    /// </summary>
    /// <param name="lat">Latitude in radians, range [-π/2, π/2]</param>
    /// <param name="lon">Longitude in radians, range [-π, π]</param>
    /// <param name="alt">Altitude in kilometers from the WGS84 ellipsoid surface</param>
    /// <returns>Cartesian coordinates in kilometers as a Vector3</returns>
    public static Vector3 LatLonToCartesianWGS84(double lat, double lon, double alt)
    {
        lat -= Math.PI / 2.0;
        lon += Math.PI;

        double a = EARTH_EQUATORIAL_RADIUS_KM;
        double b = EARTH_EQUATORIAL_RADIUS_KM;
        double c = EARTH_POLAR_RADIUS_KM;

        double zCoo = a * Math.Sin(lat) * Math.Cos(lon);
        double xCoo = b * Math.Sin(lat) * Math.Sin(lon);
        double yCoo = c * Math.Cos(lat);

        double dist = Math.Sqrt((xCoo * xCoo) + (yCoo * yCoo) + (zCoo * zCoo));
        double altScalar = 1.0d + (alt / dist);

        zCoo *= altScalar;
        xCoo *= altScalar;
        yCoo *= altScalar;

        return new Vector3(
            (float)xCoo,
            (float)yCoo,
            (float)zCoo
        );
    }

    /// <summary>
    /// Assuming lat/lon of (0,0) is null island, returns the 2D coordinate in cartesian space of where
    /// the lat/lon should be located in the game world.
    ///
    /// Input assumptions:
    /// - Latitude range: [-π/2, π/2] (South Pole to North Pole)
    /// - Longitude range: [-π, π] (180°W to 180°E)
    /// - Null island (0,0) lies on the +ve Y-axis in the Godot coordinate system
    /// - Increasing longitude corresponds to eastward movement (+ve X-axis)
    /// - Web mercator projection
    ///
    /// </summary>
    /// <param name="lat">Input latitude in radians [-π/2, π/2] </param>
    /// <param name="lon">Input longitude in radians [-π, π] </param>
    /// <returns>Location of the lat/lon coordinate in 2D cartesian space, units of kilometers</returns>
    public static Vector2 LatLonToCartesianWebMercator(double lat, double lon)
    {
        if (lat < MIN_LATITUDE_LEVEL_WEB_MERCATOR || lat > MAX_LATITUDE_LEVEL_WEB_MERCATOR)
        {
            throw new ArgumentOutOfRangeException("Latitude outside the proper range for a web mercator projection!");
        }

        // For longitude: direct linear mapping (convert from [-π,π] to [0,2π] first)
        // Then scale by Earth's radius and center
        lon += Math.PI;
        double x = lon * EARTH_EQUATORIAL_RADIUS_KM;

        // For latitude: Web Mercator formula
        // y = R * ln(tan(π/4 + φ/2)) where φ is latitude
        double y = EARTH_EQUATORIAL_RADIUS_KM *
                   Math.Log(Math.Tan(Math.PI / 4 + lat / 2));

        return new Vector2((float)x, (float)y);
    }

    public static (double, double) GetPlanetSemiMajorAxis(PlanetShapeType shapeType)
    {
        switch (shapeType)
        {
            case PlanetShapeType.GENERIC_SPHERE:
                return (BLANK_PLANET_SEMI_MAJOR_AXIS_LEN_KM, BLANK_PLANET_SEMI_MINOR_AXIS_LEN_KM);

            case PlanetShapeType.EARTH_WGS84:
                return (EARTH_EQUATORIAL_RADIUS_KM, EARTH_POLAR_RADIUS_KM);

            case PlanetShapeType.MERCURY:
                return (MERCURY_SEMI_MAJOR_AXIS_LEN_KM, MERCURY_SEMI_MINOR_AXIS_LEN_KM);

            case PlanetShapeType.VENUS:
                return (VENUS_SEMI_MAJOR_AXIS_LEN_KM, VENUS_SEMI_MINOR_AXIS_LEN_KM);

            case PlanetShapeType.MARS:
                return (MARS_SEMI_MAJOR_AXIS_LEN_KM, MARS_SEMI_MINOR_AXIS_LEN_KM);

            case PlanetShapeType.JUPITER:
                return (JUPITER_SEMI_MAJOR_AXIS_LEN_KM, JUPITER_SEMI_MINOR_AXIS_LEN_KM);

            case PlanetShapeType.SATURN:
                return (SATURN_SEMI_MAJOR_AXIS_LEN_KM, SATURN_SEMI_MINOR_AXIS_LEN_KM);

            case PlanetShapeType.URANUS:
                return (URANUS_SEMI_MAJOR_AXIS_LEN_KM, URANUS_SEMI_MINOR_AXIS_LEN_KM);

            case PlanetShapeType.NEPTUNE:
                return (NEPTUNE_SEMI_MAJOR_AXIS_LEN_KM, NEPTUNE_SEMI_MINOR_AXIS_LEN_KM);

            case PlanetShapeType.UNKNOWN:
                return (BLANK_PLANET_SEMI_MAJOR_AXIS_LEN_KM, BLANK_PLANET_SEMI_MINOR_AXIS_LEN_KM);

            default:
                return (BLANK_PLANET_SEMI_MAJOR_AXIS_LEN_KM, BLANK_PLANET_SEMI_MINOR_AXIS_LEN_KM);
        }
    }
    
    /// <summary>
    /// Given a radius and the circumference of a sphere, returns the radians of latitude and longitude
    /// that the radius mapped onto the surface corresponds to. Assumes the radius is mapped parallel to the
    /// lines of latitude and longitude
    /// </summary>
    /// <param name="radius"></param>
    /// <returns>latitude and longitude range of the mapped radius in radians</returns>
    public static (double latRange, double lonRange) DistanceToLatLonRange(
        double radius,
        double sphereCircum
    )
    {
        double range = (radius / sphereCircum) * TWO_PI;
        return (range, range);
    }


    /// <summary>
    /// Converts a quadkey to the latitude and longitude at the center of the corresponding map tile,
    /// as well as the zoom level.
    ///
    /// The quadkey encodes the tile x and y coordinates along with the zoom level.
    /// This method decodes the quadkey, calculates the tile's geographical bounds, and
    /// returns the center point of the tile.
    /// </summary>
    /// <param name="quadkey">The quadkey string.</param>
    /// <returns>
    /// A tuple containing:
    /// - centerLat: Center latitude of the tile (in radians)
    /// - centerLon: Center longitude of the tile (in radians)
    /// - zoom: The zoom level of the quadkey
    /// </returns>
    public static (double centerLat, double centerLon, int zoom) QuadKeyToLatLonAndZoom(string quadkey)
    {
        if (string.IsNullOrEmpty(quadkey))
        {
            throw new ArgumentException("Quadkey cannot be null or empty.");
        }

        // The zoom level is the length of the quadkey
        int zoom = quadkey.Length;
        int x = 0;
        int y = 0;

        // Decode the quadkey to obtain the tile x and y coordinates.
        // Each character in the quadkey represents two bits of the tile coordinates.
        for (int i = 0; i < quadkey.Length; i++)
        {
            int bit = zoom - i - 1;
            int mask = 1 << bit;
            char digit = quadkey[i];

            switch (digit)
            {
                case '0':
                    // No bits are set.
                    break;
                case '1':
                    // Set the bit corresponding to the x coordinate.
                    x |= mask;
                    break;
                case '2':
                    // Set the bit corresponding to the y coordinate.
                    y |= mask;
                    break;
                case '3':
                    // Set both bits.
                    x |= mask;
                    y |= mask;
                    break;
                default:
                    throw new ArgumentException($"Invalid QuadKey digit: {digit}");
            }
        }

        // Get the geographical bounds of the tile.
        var (latMin, latMax, lonMin, lonMax) = GetTileLatLonBounds(x, y, zoom);

        // Calculate the center of the tile.
        double centerLat = (latMin + latMax) / 2.0;
        double centerLon = (lonMin + lonMax) / 2.0;

        return (centerLat, centerLon, zoom);
    }
}