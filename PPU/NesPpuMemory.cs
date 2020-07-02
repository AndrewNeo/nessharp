using System;
using NesSharp.Memory;
using NesSharp.Utils;

namespace NesSharp.PPU
{
    public class NesPpuMemory : NesMemory, IResettable
    {
        private readonly byte[] Vram;
        private readonly byte[] PaletteRAM;
        internal readonly byte[] OAMram;

        public NesPpuMemory(Nes nes) : base(nes, AddressBus.PPU)
        {
            Vram = new byte[(NesConsts.ChrPageSize * 2) + 1];
            PaletteRAM = new byte[0x20 + 1];
            OAMram = new byte[0x100 + 1];
        }

        public void SoftReset()
        {
        }

        public void HardReset()
        {
            FastBits.Clear(Vram);
            FastBits.Clear(PaletteRAM);
            FastBits.Clear(OAMram);
        }

        protected override MemoryMapResponse MapMemory(ushort address)
        {
            if (address < 0x2000)
            {
                throw new UnderhandledMemoryException(AddressBus.PPU, address, "Open bus");
            }
            else if (address >= 0x2000 && address <= 0x3EFF)
            {
                // 0x2000-0x3EFF is mirrored with nametable mirror direction
                var offset = GetNametableMirror(address);
                return new MemoryMapResponse(MemoryMapOrigin.PpuVram, Vram, offset, true);
            }
            else if (address >= 0x3F00 && address <= 0x3FFF)
            {
                // 0x3F00-0x3FFF are mirrored every 32 bytes
                return new MemoryMapResponse(MemoryMapOrigin.PpuPaletteRam, PaletteRAM, 0x3F00, true, repeat: 32);
            }
            else if (address >= 0x4000)
            {
                throw new UnderhandledMemoryException(AddressBus.PPU, address, "Above range");
            }

            return new MemoryMapResponse(MemoryMapOrigin.OpenBus, OpenBus, 0, false);
        }

        private ushort GetNametableMirror(ushort address)
        {
            switch (Nes.Ppu.MirrorMode)
            {
                case MirrorMode.Vertical:
                    return (ushort)(address % 0x800);
                case MirrorMode.Horizontal:
                    return (ushort)(((address / 2) & 0x400) + (address % 0x400));
                default:
                    return (ushort)(address - 0x2000);
            }
        }
    }
}