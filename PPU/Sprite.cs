using System;

namespace NesSharp.PPU
{
    struct Sprite
    {
        public byte ID;
        public byte X;
        public byte Y;
        public byte Tile;
        public byte Attribute;
        public byte DataLow;
        public byte DataHigh;

        public void Clear()
        {
            ID = 0x40;
            X = 0xFF;
            Y = 0xFF;
            Tile = 0xFF;
            Attribute = 0xFF;
            DataLow = 0;
            DataHigh = 0;
        }
    }
}