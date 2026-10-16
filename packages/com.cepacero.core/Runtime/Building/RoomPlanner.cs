using System;
using System.Collections.Generic;
using CepaCero.Util;

namespace CepaCero.Building
{
    public enum RoomRole : byte
    {
        Wall,
        Door,
        Floor,
        Roof,
    }

    public readonly struct RoomCell
    {
        public RoomCell(Int2 cell, RoomRole role)
        {
            Cell = cell;
            Role = role;
        }

        public Int2 Cell { get; }

        public RoomRole Role { get; }
    }

    /// <summary>The room tool (D-022): drag a rectangle and get walls around, floor and roof inside and a gap for the door.</summary>
    public static class RoomPlanner
    {
        /// <summary>Which side the door is on, seen on the map with y growing north.</summary>
        public enum DoorSide : byte
        {
            South,
            East,
            North,
            West,
        }

        /// <summary>Walls first, then the door, then floor and roof, so a half-built room is already closed.</summary>
        public static void Plan(Int2 a, Int2 b, DoorSide side, bool floor, bool roof, List<RoomCell> into)
        {
            int x0 = Math.Min(a.X, b.X), x1 = Math.Max(a.X, b.X), y0 = Math.Min(a.Y, b.Y), y1 = Math.Max(a.Y, b.Y);
            bool hasInside = x1 - x0 >= 2 && y1 - y0 >= 2;
            Int2 door = hasInside ? DoorCell(x0, y0, x1, y1, side) : new Int2(-1, -1);
            for (int x = x0; x <= x1; x++)
            {
                for (int y = y0; y <= y1; y++)
                {
                    bool border = x == x0 || x == x1 || y == y0 || y == y1;
                    if (border && !(x == door.X && y == door.Y))
                    {
                        into.Add(new RoomCell(new Int2(x, y), RoomRole.Wall));
                    }
                }
            }
            if (hasInside)
            {
                into.Add(new RoomCell(door, RoomRole.Door));
            }
            if (!hasInside)
            {
                return;
            }
            for (int x = x0 + 1; x < x1; x++)
            {
                for (int y = y0 + 1; y < y1; y++)
                {
                    if (floor)
                    {
                        into.Add(new RoomCell(new Int2(x, y), RoomRole.Floor));
                    }
                    if (roof)
                    {
                        into.Add(new RoomCell(new Int2(x, y), RoomRole.Roof));
                    }
                }
            }
        }

        /// <summary>The door goes in the middle of the chosen side.</summary>
        public static Int2 DoorCell(int x0, int y0, int x1, int y1, DoorSide side)
        {
            int mx = (x0 + x1) / 2, my = (y0 + y1) / 2;
            switch (side)
            {
                case DoorSide.South: return new Int2(mx, y0);
                case DoorSide.North: return new Int2(mx, y1);
                case DoorSide.East: return new Int2(x1, my);
                default: return new Int2(x0, my);
            }
        }

        /// <summary>The side of the rectangle nearest to the player, so the door faces where they stand.</summary>
        public static DoorSide SideFacing(Int2 a, Int2 b, Float2 player)
        {
            int x0 = Math.Min(a.X, b.X), x1 = Math.Max(a.X, b.X), y0 = Math.Min(a.Y, b.Y), y1 = Math.Max(a.Y, b.Y);
            float halfW = (x1 + 1 - x0) * 0.5f, halfH = (y1 + 1 - y0) * 0.5f;
            float dx = (player.X - (x0 + halfW)) / halfW, dy = (player.Y - (y0 + halfH)) / halfH;
            if (Math.Abs(dy) >= Math.Abs(dx))
            {
                return dy < 0f ? DoorSide.South : DoorSide.North;
            }
            return dx < 0f ? DoorSide.West : DoorSide.East;
        }
    }
}
