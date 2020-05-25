using System;
using NesSharp.Memory;
using NesSharp.Utils;

namespace NesSharp.CPU
{
    public class NesCpuMemory : NesMemory, IResettable
    {
        public NesCpuMemory(Nes nes)
        {
            this.Nes = nes;
        }

        private Nes Nes;
        private byte[] BidirectionalIO;
        private byte[] WorkingRam;

        public void SoftReset()
        {
        }

        public void HardReset()
        {
            BidirectionalIO = new byte[2];
            WorkingRam = new byte[2 * 1024];
        }

        protected override MemoryMapResponse MapMemory(ushort address)
        {
            if (address >= 0x0000 && address <= 0x0001)
            {
                return new MemoryMapResponse(BidirectionalIO, 0, true);
            }
            else if (address >= 0x0002 && address <= 0x1FFF)
            {
                // 0x0002-0x07FF are mirrored every 0x800 bytes
                ushort offset = (ushort)(address % 0x800);
                return new MemoryMapResponse(WorkingRam, offset, true);
            }
            else if (address >= 0x2000 && address <= 0x3FFF)
            {
                // PPU registers
                throw new UnderhandledMemoryException(AddressBus.CPU, address, "PPU register open bus");
            }
            else if (address >= 0x4000 && address <= 0x4017)
            {
                // APU registers
                throw new UnderhandledMemoryException(AddressBus.CPU, address, "APU register open bus");
            }
            else if (address >= 0x4018)
            {
                throw new UnderhandledMemoryException(AddressBus.CPU, address, "CPU test mode space");
            }

            return new MemoryMapResponse(OpenBus, 0, false);
        }
    }
}