using System;

namespace CepaCero.Util
{
    /// <summary>
    /// Growable little-endian byte buffer for network payloads. Reuse one
    /// instance (call <see cref="Clear"/>) to avoid per-tick allocations.
    /// The network layer (FishNet) only ships the resulting bytes.
    /// </summary>
    public sealed class ByteWriter
    {
        private byte[] _buffer;

        public ByteWriter(int capacity = 256)
        {
            _buffer = new byte[Math.Max(16, capacity)];
        }

        public int Length { get; private set; }

        public ArraySegment<byte> Segment => new ArraySegment<byte>(_buffer, 0, Length);

        public void Clear() => Length = 0;

        public byte[] ToArray()
        {
            var copy = new byte[Length];
            Buffer.BlockCopy(_buffer, 0, copy, 0, Length);
            return copy;
        }

        public void WriteByte(byte value)
        {
            Ensure(1);
            _buffer[Length++] = value;
        }

        public void WriteUShort(ushort value)
        {
            Ensure(2);
            _buffer[Length++] = (byte)value;
            _buffer[Length++] = (byte)(value >> 8);
        }

        public void WriteInt(int value)
        {
            Ensure(4);
            _buffer[Length++] = (byte)value;
            _buffer[Length++] = (byte)(value >> 8);
            _buffer[Length++] = (byte)(value >> 16);
            _buffer[Length++] = (byte)(value >> 24);
        }

        public void WriteUInt(uint value) => WriteInt(unchecked((int)value));

        private void Ensure(int extra)
        {
            if (Length + extra <= _buffer.Length)
            {
                return;
            }
            int size = _buffer.Length * 2;
            while (size < Length + extra)
            {
                size *= 2;
            }
            Array.Resize(ref _buffer, size);
        }
    }
}
