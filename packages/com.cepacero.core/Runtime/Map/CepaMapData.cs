using System;

// Field names are snake_case on purpose: they must match the JSON keys of
// cepacero.map v1 exactly so Unity's JsonUtility can fill these classes
// (tools/osm2tiles/README.md). This is the one exception to the PascalCase rule.
#pragma warning disable IDE1006

namespace CepaCero.Map
{
    /// <summary>Raw `cepacero.map` document (v1, or v2 with storeys per building), as written by tools/osm2tiles.</summary>
    [Serializable]
    public sealed class CepaMapData
    {
        public string format;
        public int version;
        public string generator;
        public string name;
        public string attribution;
        public string license;
        public CepaMapSource source;
        public float meters_per_tile;
        public int width;
        public int height;
        public string origin;
        public CepaMapLegend legend;
        public CepaMapLayers layers;
        public CepaMapBuilding[] buildings;
    }

    [Serializable]
    public sealed class CepaMapSource
    {
        public double[] bbox;
        public string osm_timestamp;
        public string projection;
    }

    [Serializable]
    public sealed class CepaMapLegend
    {
        public string[] ground;
        public string[] blocks;
        public string[] building_kinds;
        public string[] roofs;
    }

    [Serializable]
    public sealed class CepaMapLayers
    {
        public int[] ground;
        public int[] blocks;
    }

    [Serializable]
    public sealed class CepaMapBuilding
    {
        public int id;
        public string kind;
        public int[] bbox;
        public int tiles;
        public int[] doors;

        /// <summary>v2: storeys above ground, ground floor included (missing or 0 in v1: one).</summary>
        public int levels;

        /// <summary>v2: basement levels.</summary>
        public int underground;

        /// <summary>v2: tile, sheet, thatch, flat or glasshouse (empty in v1).</summary>
        public string roof;
    }
}
