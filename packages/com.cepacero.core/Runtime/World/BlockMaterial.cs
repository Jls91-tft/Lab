namespace CepaCero.World
{
    /// <summary>What a block is made of. Drives special interactions (acid vs wood, La Mole vs metal).</summary>
    public enum BlockMaterial : byte
    {
        None = 0,
        Wood = 1,
        Metal = 2,
        Concrete = 3,
        Natural = 4,
    }
}
