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
                System.IO.Path.GetFileName(filename),
                header,
                prgRom.ToArray(),
                chrRom.ToArray(),
                new byte[NesConsts.PrgPageSize + 1],
                NesCartMapper.Build(nes, header)
            );
        }

        private static NesCartHeader ReadHeader(Span<byte> rom)
        {
            var fileId = rom.Slice(0, 4).ToArray();
            if (!fileId.SequenceEqual(INES_FORMAT))
            {
                throw new RomFormatException(String.Format("Unsupported ROM type: {0:X}{1:X}{2:X}{3:X}", fileId[0], fileId[1], fileId[2], fileId[3]));
            }

            var header = new NesCartHeader(
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

            if (header.isNes20)
            {
                throw new RomFormatException("iNES 2.0 currently unsupported");
            }

            return header;
        }
    }
}