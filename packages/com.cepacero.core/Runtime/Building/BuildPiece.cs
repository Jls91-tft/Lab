namespace CepaCero.Building
{
    /// <summary>One thing the held item can place: a shape of its material (or a roof). Key is the block key.</summary>
    public readonly struct BuildPiece
    {
        public BuildPiece(string key, string shapeId, string name)
        {
            Key = key;
            ShapeId = shapeId;
            Name = name;
        }

        public string Key { get; }

        /// <summary>wall, door, window... or "roof".</summary>
        public string ShapeId { get; }

        public string Name { get; }
    }
}
