using System;
using NesSharp.Memory;
using NesSharp.Utils;

namespace NesSharp.PPU
{
    public class NesPpuMemory : NesMemory, IResettable
    {
        private byte[] Vram;
        private byte[] PaletteRAM;

        public void SoftReset()
        {
        }

        public void HardReset()
        {
            Vram = new byte[NesConsts.ChrPageSize * 2];
            PaletteRAM = new byte[0x20];
        }

        // (memory, offset, writable)
        protected override MemoryMapResponse MapMemory(ushort address)
        {
            if (address < 0x2000)
            {
                throw new UnderhandledMemoryException(AddressBus.PPU, address, "Open bus");
            }
            else if (address >= 0x2000 && address <= 0x2FFF)
            {
                return new MemoryMapResponse(Vram, 0x2000, true);
            }
            else if (address >= 0x3000 && address <= 0x3EFF)
            {
                // Mirror
                return new MemoryMapResponse(Vram, 0x3000, true);
            }
            else if (address >= 0x3F00 && address <= 0x3FFF)
            {
                // 0x3F00-0x3FFF are mirrored every 32 bytes
                ushort offset = (ushort)(address + (0x3F00 % 32));
                return new MemoryMapResponse(PaletteRAM, offset, true);
            }
            else if (address >= 0x4000)
            {
                throw new UnderhandledMemoryException(AddressBus.PPU, address, "Above range");
            }

            return new MemoryMapResponse(OpenBus, 0, false);
        }
    }
}