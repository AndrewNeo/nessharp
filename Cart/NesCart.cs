using System;
using System.Linq;
using NesSharp.Utils;

namespace NesSharp.Cart
{
    public readonly struct NesCart
    {
        public NesCart(
            NesCartHeader header,
            byte[] prgRom,
            byte[] chrRom,
            byte[] expansionArea,
            NesCartMapper mapper
        )
        {
            this.Header = header;
            this.PrgRom = prgRom;
            this.ChrRom = chrRom;
            this.ExpansionArea = expansionArea;
            this.Mapper = mapper;
        }

        public readonly NesCartHeader Header;
        public readonly byte[] PrgRom;
        public readonly byte[] ChrRom;
        public readonly byte[] ExpansionArea;
        public readonly NesCartMapper Mapper;
    }

    public readonly struct NesCartHeader
    {
        public NesCartHeader(
            byte[] fileId,
            byte prgRomPages,
            byte chrRomPages,
            byte cartTypeLsb,
            byte cartTypeMsb,
            byte prgRamPages,
            byte tvSystem1,
            byte tvSystem2,
            byte[] padding
        )
        {
            this.FileId = fileId;
            this.PrgRomPages = prgRomPages;
            this.ChrRomPages = chrRomPages;
            this.CartTypeLsb = FastBits.Get(cartTypeLsb);
            this.CartTypeMsb = FastBits.Get(cartTypeMsb);
            this.PrgRamPages = prgRamPages;
            this.TvSystem1 = tvSystem1;
            this.TvSystem2 = tvSystem2;
            this.Padding = padding;

            this.MapperNumber = (byte)(((cartTypeLsb & 0xF0) >> 4) | (cartTypeMsb & 0xF0));
        }

        public readonly byte[] FileId;
        /// <summary>Size of PRG ROM in 16KB units</summary>
        public readonly byte PrgRomPages;
        /// <summary>Size of CHR ROM in 8KB units, 0 means board uses CHR RAM</summary>
        public readonly byte ChrRomPages;
        public readonly bool[] CartTypeLsb;
        public readonly bool[] CartTypeMsb;
        /// <summary>Size of PRG RAM in 8KB units, 0 infers 8KB for compatibility</summary>
        public readonly byte PrgRamPages;
        public readonly byte TvSystem1;
        public readonly byte TvSystem2;
        public readonly byte[] Padding;

        // header byte 6 flags

        public bool mirroring { get { return CartTypeLsb[0]; } }
        public bool hasPrgRam { get { return CartTypeLsb[1]; } }
        public bool hasTrainer { get { return CartTypeLsb[2]; } }
        public bool useFourScreenVram { get { return CartTypeLsb[3]; } }

        // header byte 7 flags

        public bool isVsUnisystem { get { return CartTypeMsb[0]; } }
        public bool isPlaychoice10 { get { return CartTypeMsb[1]; } }
        public bool isNes20 { get { return CartTypeMsb[2] == true && CartTypeMsb[3] == true; } }

        // combined flags

        public readonly byte MapperNumber;
    }
}