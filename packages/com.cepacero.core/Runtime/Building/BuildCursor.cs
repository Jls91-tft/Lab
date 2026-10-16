using CepaCero.World;

namespace CepaCero.Building
{
    /// <summary>
    /// What the local player has dialled in for the next piece: shape, colour and rotation (D-022).
    /// The shape is remembered by id, so changing from planks to bricks keeps "door" if the new
    /// material has one. Pure client state: the host only ever sees the resulting placement.
    /// </summary>
    public sealed class BuildCursor
    {
        public const int ColorCount = 16;

        private string _shapeId = "wall";

        public int Color { get; private set; }

        public int Rotation { get; private set; }

        public string ShapeId => _shapeId;

        public byte Variant => BlockVariant.Make(Color, Rotation);

        /// <summary>The piece to place with this item, honouring the remembered shape; null if the item builds nothing.</summary>
        public BuildPiece? Current(BuildPalette palette, string itemId)
        {
            System.Collections.Generic.IReadOnlyList<BuildPiece> pieces = palette.PiecesFor(itemId);
            if (pieces.Count == 0)
            {
                return null;
            }
            int i = IndexOf(pieces, _shapeId);
            return pieces[i < 0 ? 0 : i];
        }

        /// <summary>Q / E: next or previous shape of the held item (wraps).</summary>
        public void CycleShape(BuildPalette palette, string itemId, int step)
        {
            System.Collections.Generic.IReadOnlyList<BuildPiece> pieces = palette.PiecesFor(itemId);
            if (pieces.Count == 0)
            {
                return;
            }
            int i = IndexOf(pieces, _shapeId);
            i = ((i < 0 ? 0 : i) + step) % pieces.Count;
            _shapeId = pieces[i < 0 ? i + pieces.Count : i].ShapeId;
        }

        public void CycleColor(int step) => Color = ((Color + step) % (ColorCount + 1) + ColorCount + 1) % (ColorCount + 1);

        public void Rotate(int step) => Rotation = ((Rotation + step) % 4 + 4) % 4;

        /// <summary>Middle click: copy the colour, rotation and shape of a piece in the world.</summary>
        public void Copy(BlockType type, byte variant)
        {
            if (type.Build != null)
            {
                _shapeId = type.Layer == BuildLayer.Roof ? BuildPalette.RoofShape : type.Build.Shape;
            }
            Color = BlockVariant.Color(variant);
            Rotation = BlockVariant.Rotation(variant);
        }

        private static int IndexOf(System.Collections.Generic.IReadOnlyList<BuildPiece> pieces, string shapeId)
        {
            for (int i = 0; i < pieces.Count; i++)
            {
                if (pieces[i].ShapeId == shapeId)
                {
                    return i;
                }
            }
            return -1;
        }
    }
}
