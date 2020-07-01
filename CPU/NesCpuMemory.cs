using System;
using NesSharp.Memory;
using NesSharp.Utils;

namespace NesSharp.CPU
{
    public class NesCpuMemory : NesMemory, IResettable
    {
        private readonly byte[] BidirectionalIO;
        private readonly byte[] WorkingRam;


        public NesCpuMemory(Nes nes) : base(nes, AddressBus.CPU)
        {
            BidirectionalIO = new byte[2];
            WorkingRam = new byte[2 * 1024];
        }

        public void SoftReset()
        {
        }

        public void HardReset()
        {
            FastBits.Clear(BidirectionalIO);
            FastBits.Clear(WorkingRam);
        }

        protected override MemoryMapResponse MapMemory(ushort address)
        {
            if (address >= 0x0000 && address <= 0x0001)
            {
                return new MemoryMapResponse(MemoryMapOrigin.CpuBidirectionalIO, BidirectionalIO, 0, true);
            }
            else if (address >= 0x0002 && address <= 0x1FFF)
            {
                // 0x0002-0x07FF are mirrored every 0x800 bytes
                ushort offset = (ushort)(address % 0x800);
                return new MemoryMapResponse(MemoryMapOrigin.CpuWorkingRam, WorkingRam, offset, true);
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

            return new MemoryMapResponse(MemoryMapOrigin.OpenBus, OpenBus, 0, false);
        }
    }
}