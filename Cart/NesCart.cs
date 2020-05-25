using System;
using System.Linq;
using NesSharp.Utils;

namespace NesSharp.Cart
{
    public static class NesCartBuilder
    {
        private static byte[] INES_FORMAT = new byte[] { 0x4E, 0x45, 0x53, 0x1A };

        public static NesCart LoadFromFile(Nes nes, string filename)
        {
            var fileContents = System.IO.File.ReadAllBytes(filename);

            var position = 0;
            var header = ReadHeader(fileContents.AsSpan(0, 16));
            position += 16;

            if (header.hasTrainer)
            {
                position += 512;
            }

            var prgRomSize = 16 * 1024 * header.PrgRomPages;
            var prgRom = fileContents.AsSpan(position, prgRomSize);
            position += prgRomSize;

            var chrRomSize = 8 * 1024 * header.ChrRomPages;
            var chrRom = fileContents.AsSpan(position, chrRomSize);

            return new NesCart(
                header,
                prgRom.ToArray(),
                chrRom.ToArray(),
                new byte[NesConsts.PrgPageSize],
                NesCartMapper.Build(nes, header)
            );
        }

        private static NesCartHeader ReadHeader(Span<byte> rom)
        {
            var fileId = rom.Slice(0, 4).ToArray();
            if (!fileId.SequenceEqual(INES_FORMAT))
            {
                throw new RomFormatException(String.Format("Unsupported ROM type: 0x{0:X}{1:X}{2:X}{3:X}", fileId[0], fileId[1], fileId[2], fileId[3]));
            }

            return new NesCartHeader(
                fileId,
                rom[4],
                rom[5],
                rom[6],
                rom[7],
                rom[8],
                rom[9],
                rom[10],
                rom.Slice(11, 5).ToArray()
            );
        }
    }

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

    class RomFormatException : Exception
    {
        public RomFormatException(string message) : base(message) { }
    }
}