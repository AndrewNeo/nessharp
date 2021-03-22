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
            WorkingRam = new byte[(2 * 1024) + 1];
        }

        public void SoftReset()
        {
        }

        public void HardReset()
        {
            FastBits.Clear(BidirectionalIO, 0xFF);
            FastBits.Clear(WorkingRam, 0xFF);
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
                return new MemoryMapResponse(MemoryMapOrigin.CpuWorkingRam, WorkingRam, 0, true, repeat: 0x800);
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
                return new MemoryMapResponse(MemoryMapOrigin.OpenBus, OpenBus, address);
                // throw new UnderhandledMemoryException(AddressBus.CPU, address, "CPU test mode space");
            }

            return new MemoryMapResponse(MemoryMapOrigin.OpenBus, OpenBus, address);
        }
    }
}