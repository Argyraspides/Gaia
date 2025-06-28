using Gaia.Common;
using Gaia.Common.Enums;
using Gaia.PlanetEngine.Utils;
using Gaia.Common.Utils.Caching;
using Godot;
using System;

namespace Gaia.PlanetEngine.MapTiles;

public class MapTile : GaiaResource
{
    // Tile dimensions
    public int Width { get; protected set; } = 256;
    public int Height { get; protected set; } = 256;

    // Geographic coordinates and ranges
    public double Latitude { get; protected set; }
    public double Longitude { get; protected set; }
    public int LatitudeTileCoo { get; protected set; }
    public int LongitudeTileCoo { get; protected set; }
    public double LatitudeRange { get; protected set; }
    public double LongitudeRange { get; protected set; }

    // Tile metadata
    public int ZoomLevel { get; protected set; } = 12;
    public MapType MapType { get; protected set; } = MapType.SATELLITE;
    public ImageType MapImageType { get; protected set; } = ImageType.PNG;
    public Texture2D Texture2D { get; protected set; } = null;

    // If the map tile is a street view map tile/hybrid, the names of various places
    // will show up, hence a map tile must have a language field
    public HumanLanguage Language { get; protected set; } = HumanLanguage.en;

    public MapTileType MapTileType { get; protected set; } = MapTileType.WEB_MERCATOR_EARTH;

    public MapTile()
    {
        // Default to null island (0,0) with a small range
        Latitude = 0.0f;
        Longitude = 0.0f;

        // Default zoom level for city-scale viewing
        ZoomLevel = 12;

        AutoDetermineFields(Latitude, Longitude, ZoomLevel);
    }

    public MapTile(double latitude, double longitude, int zoomLevel,
        MapTileType tileType = MapTileType.WEB_MERCATOR_EARTH)
    {
        Latitude = latitude;
        Longitude = longitude;
        ZoomLevel = zoomLevel;
        MapTileType = tileType;

        AutoDetermineFields(Latitude, Longitude, ZoomLevel);
    }

    private void AutoDetermineFields(double latitude, double longitude, int zoomLevel)
    {
        // Automatically determine tile coordinate, latitude/longitude range
        LatitudeTileCoo = PlanetUtils.LatitudeToTileCoordinate(MapTileType, latitude, zoomLevel);
        LongitudeTileCoo = PlanetUtils.LongitudeToTileCoordinate(MapTileType, longitude, zoomLevel);
        LatitudeRange = PlanetUtils.TileToLatRange(MapTileType, LatitudeTileCoo, zoomLevel);
        LongitudeRange = PlanetUtils.TileToLonRange(MapTileType, LongitudeTileCoo, zoomLevel);
    }

    public override bool IsHashable()
    {
        throw new NotImplementedException("Resource " + this +
                                          " cannot be determined hashable. You must implement this function in any derived class of Resource");
    }

    public override string GenerateHashCore()
    {
        throw new NotImplementedException("Resource " + this +
                                          " cannot have a hash generated. You must implement this function in any derived class of Resource");
    }
}