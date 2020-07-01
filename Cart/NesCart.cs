using System;

namespace NesSharp.Cart
{
    public readonly struct NesCart
    {
        public NesCart(
            string filename,
            NesCartHeader header,
            byte[] prgRom,
            byte[] chrRom,
            byte[] expansionArea,
            NesCartMapper mapper
        )
        {
            this.Filename = filename;
            this.Header = header;
            this.PrgRom = prgRom;
            this.ChrRom = chrRom;
            this.ExpansionArea = expansionArea;
            this.Mapper = mapper;
        }

        public readonly string Filename;
        public readonly NesCartHeader Header;
        public readonly byte[] PrgRom;
        public readonly byte[] ChrRom;
        public readonly byte[] ExpansionArea;
        public readonly NesCartMapper Mapper;
    }
}