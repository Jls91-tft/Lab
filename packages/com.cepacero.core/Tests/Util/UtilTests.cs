using System;
using CepaCero.Util;
using NUnit.Framework;

namespace CepaCero.Tests.Util
{
    public class UtilTests
    {
        [Test]
        public void BytesRoundTrip()
        {
            var w = new ByteWriter(1);
            w.WriteByte(250);
            w.WriteUShort(65000);
            w.WriteInt(-123456789);
            w.WriteUInt(4000000000);
            var r = new ByteReader(w.ToArray());
            Assert.That(r.ReadByte(), Is.EqualTo(250));
            Assert.That(r.ReadUShort(), Is.EqualTo(65000));
            Assert.That(r.ReadInt(), Is.EqualTo(-123456789));
            Assert.That(r.ReadUInt(), Is.EqualTo(4000000000));
            Assert.That(r.Remaining, Is.EqualTo(0));
            Assert.Throws<FormatException>(() => r.ReadByte());
        }

        [Test]
        public void RandomIsReproducibleAndInRange()
        {
            var a = new DeterministicRandom(42);
            var b = new DeterministicRandom(42);
            for (int i = 0; i < 1000; i++)
            {
                int va = a.Range(-5, 5);
                Assert.That(va, Is.EqualTo(b.Range(-5, 5)));
                Assert.That(va, Is.InRange(-5, 4));
                float f = a.NextFloat();
                b.NextFloat();
                Assert.That(f, Is.GreaterThanOrEqualTo(0f).And.LessThan(1f));
            }
        }

        [Test]
        public void FloatTileFloorsNegativeCoordinates()
        {
            Assert.That(new Float2(-0.5f, 2.9f).Tile, Is.EqualTo(new Int2(-1, 2)));
        }
    }
}
