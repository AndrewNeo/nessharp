using System;
using NesSharp.Memory;

namespace NesSharp.PPU
{
    public class NesPpuAddressBus : IMemoryAccess
    {
        private Nes Nes;

        public NesPpuAddressBus(Nes nes)
        {
            Nes = nes;
        }

        public byte ReadByte(ushort address, bool quiet = false)
        {
            if (InMapperRange(address))
            {
                return Nes.Cart.Mapper.PpuReadByte(address, quiet);
            }
            else
            {
                return Nes.Ppu.Memory.ReadByte(address, quiet);
            }
        }

        public ushort ReadAddress(ushort address, bool quiet = false)
        {
            if (InMapperRange(address))
            {
                return Nes.Cart.Mapper.PpuReadUShort(address, quiet);
            }
            else
            {
                return Nes.Ppu.Memory.ReadAddress(address, quiet);
            }
        }

        public void Write(ushort address, byte value)
        {
            if (InMapperRange(address))
            {
                Nes.Cart.Mapper.PpuWrite(address, value);
            }
            else
            {
                Nes.Ppu.Memory.Write(address, value);
            }
        }

        protected bool InMapperRange(ushort address)
        {
            if (address >= Nes.Cart.Mapper.PPUStartRange && address <= Nes.Cart.Mapper.PPUEndRange)
            {
                return true;
            }

            return false;
        }
    }
}