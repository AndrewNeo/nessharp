using System;

namespace NesSharp
{
    public class NesCpuMemory : IResettable
    {
        public NesCpuMemory(Nes nes)
        {
            this.Nes = nes;
        }

        private Nes Nes;

        public byte ReadByte(ushort i)
        {
            if (i < 0x4018)
            {
                var mmr = MapMemory(i);
                return mmr.ReadByte(i);
            }
            else
            {
                return this.Nes.Cart.Mapper.CpuReadByte(i);
            }
        }

        public ushort ReadUShort(ushort i)
        {
            if (i < 0x4018)
            {
                var mmr = MapMemory(i);
                return mmr.ReadUShort(i);
            }
            else
            {
                return this.Nes.Cart.Mapper.CpuReadUShort(i);
            }
        }

        public void Write(ushort i, byte d)
        {
            if (i < 0x4018)
            {
                var mmr = MapMemory(i);
                mmr.Write(i, d);
            }
            else
            {
                this.Nes.Cart.Mapper.CpuWrite(i, d);
            }
        }

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

        // (memory, offset, writable)
        private MemoryMapResponse MapMemory(ushort pos)
        {
            if (pos >= 0x0000 && pos <= 0x0001)
            {
                return new MemoryMapResponse(BidirectionalIO, 0, true);
            }
            else if (pos >= 0x0002 && pos <= 0x07FF)
            {
                return new MemoryMapResponse(WorkingRam, 0, true);
            }
            else if (pos >= 0x0800 && pos <= 0x0FFF)
            {
                return new MemoryMapResponse(WorkingRam, 0x0800, true);
            }
            else if (pos >= 0x1000 && pos <= 0x1FFF)
            {
                return new MemoryMapResponse(WorkingRam, 0x1000, true);
            }
            else if (pos >= 0x2000 && pos <= 0x3FFF)
            {
                // 0x2000-0x2007 are mirrored every 8 bytes
                ushort offset = (ushort)(pos + (0x2000 % 8));
                return new MemoryMapResponse(Nes.Ppu.Registers, offset, true);
            }
            else if (pos >= 0x4000 && pos <= 0x4017)
            {
                return new MemoryMapResponse(Nes.SharedMem.InternalApuRegisters, 0x4000, true);
            }
            else if (pos >= 0x4018)
            {
                throw new Exception(String.Format("Underhandled memory address at {0:x}", pos));
            }

            return new MemoryMapResponse(new byte[0], 0, false);
        }
    }
}