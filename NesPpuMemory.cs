using System;

namespace NesSharp
{
    public class NesPpuMemory : IResettable
    {
        public NesPpuMemory(Nes nes)
        {
            this.Nes = nes;
        }

        private Nes Nes;

        public byte ReadByte(ushort i)
        {
            if (i >= 0x2000)
            {
                var mmr = MapMemory(i);
                return mmr.ReadByte(i);
            }
            else
            {
                return this.Nes.Cart.Mapper.PpuReadByte(i);
            }
        }

        public ushort ReadUShort(ushort i)
        {
            if (i >= 0x2000)
            {
                var mmr = MapMemory(i);
                return mmr.ReadUShort(i);
            }
            else
            {
                return this.Nes.Cart.Mapper.PpuReadUShort(i);
            }
        }

        public void Write(ushort i, byte d)
        {
            if (i >= 0x2000)
            {
                var mmr = MapMemory(i);
                mmr.Write(i, d);
            }
            else
            {
                this.Nes.Cart.Mapper.PpuWrite(i, d);
            }
        }

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
        private MemoryMapResponse MapMemory(ushort pos)
        {
            if (pos < 0x2000)
            {
                throw new Exception(String.Format("Underhandled memory address at {0:x}", pos));
            }
            else if (pos >= 0x2000 && pos <= 0x2FFF)
            {
                return new MemoryMapResponse(Vram, 0x2000, true);
            }
            else if (pos >= 0x3000 && pos <= 0x3EFF)
            {
                // Mirror
                return new MemoryMapResponse(Vram, 0x3000, true);
            }
            else if (pos >= 0x3F00 && pos <= 0x3FFF)
            {
                // 0x3F00-0x3FFF are mirrored every 32 bytes
                ushort offset = (ushort)(pos + (0x3F00 % 32));
                return new MemoryMapResponse(PaletteRAM, offset, true);
            }
            else if (pos >= 0x4000)
            {
                throw new Exception(String.Format("Over-range memory address at {0:x}", pos));
            }

            return new MemoryMapResponse(new byte[0], 0, false);
        }
    }
}