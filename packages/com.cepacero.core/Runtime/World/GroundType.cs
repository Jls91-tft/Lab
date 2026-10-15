namespace CepaCero.World
{
    /// <summary>Floor under the blocks. Values match the `ground` legend of cepacero.map v1, but the importer maps by name.</summary>
    public enum GroundType : byte
    {
        Dirt = 0,
        Asphalt = 1,
        Sidewalk = 2,
        Grass = 3,
        Water = 4,
        Floor = 5,
        Rail = 6,
    }
}
