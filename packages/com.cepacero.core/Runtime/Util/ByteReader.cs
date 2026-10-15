using System;

namespace CepaCero.Util
{
    /// <summary>Little-endian reader matching <see cref="ByteWriter"/>. Throws on truncated input.</summary>
    public sealed class ByteReader
    {
        private readonly byte[] _buffer;
        private readonly int _end;

        public ByteReader(ArraySegment<byte> data)
        {
            _buffer = data.Array ?? Array.Empty<byte>();
            Position = data.Offset;
            _end = data.Offset + data.Count;
        }

        public ByteReader(byte[] data) : this(new ArraySegment<byte>(data))
        {
        }

        public int Position { get; private set; }

        public int Remaining => _end - Position;

        public byte ReadByte()
        {
            Require(1);
            return _buffer[Position++];
        }

        public ushort ReadUShort()
        {
            Require(2);
            int value = _buffer[Position] | (_buffer[Position + 1] << 8);
            Position += 2;
            return (ushort)value;
        }

        public int ReadInt()
        {
            Require(4);
            int value = _buffer[Position]
                        | (_buffer[Position + 1] << 8)
                        | (_buffer[Position + 2] << 16)
                        | (_buffer[Position + 3] << 24);
            Position += 4;
            return value;
        }

        public uint ReadUInt() => unchecked((uint)ReadInt());

        private void Require(int count)
        {
            if (Position + count > _end)
            {
                throw new FormatException($"Unexpected end of data: need {count} bytes, {Remaining} left.");
            }
        }
    }
}
