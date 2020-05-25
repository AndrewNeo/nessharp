using System;
using NesSharp.Memory;

namespace NesSharp.CPU
{
    public class NesCpuAddressBus : IMemoryAccess
    {
        private Nes Nes;

        public NesCpuAddressBus(Nes nes)
        {
            Nes = nes;
        }

        public byte ReadByte(ushort address)
        {
            if (InMapperRange(address))
            {
                return Nes.Cart.Mapper.CpuReadByte(address);
            }
            else if (address >= 0x2000 && address <= 0x3FFF)
            {
                // 0x2000-0x2007 are mirrored every 8 bytes
                ushort offset = (ushort)(address % 8);
                return Nes.Ppu.Read(offset);
            }
            else if (address >= 0x4000 && address <= 0x4017)
            {
                throw new Exception("APU not yet implemented");
            }
            else
            {
                return Nes.Cpu.Memory.ReadByte(address);
            }
        }

        public ushort ReadAddress(ushort address)
        {
            if (InMapperRange(address))
            {
                return Nes.Cart.Mapper.CpuReadUShort(address);
            }
            else if (address >= 0x2000 && address <= 0x3FFF)
            {
                throw new IllegalMemoryAccessException(AddressBus.CPU, address, "Attempted to read 16 bits from PPU registers");
            }
            else if (address >= 0x4000 && address <= 0x4013)
            {
                throw new IllegalMemoryAccessException(AddressBus.CPU, address, "Attempted to read 16 bits from APU registers");
            }
            else if (address >= 0x4014 && address <= 0x4017)
            {
                throw new Exception("DMA not yet implemented");
            }
            else
            {
                return Nes.Cpu.Memory.ReadAddress(address);
            }
        }

        public void Write(ushort address, byte value)
        {
            if (InMapperRange(address))
            {
                Nes.Cart.Mapper.CpuWrite(address, value);
            }
            else if (address >= 0x2000 && address <= 0x3FFF)
            {
                // 0x2000-0x2007 are mirrored every 8 bytes
                ushort offset = (ushort)(address % 8);
                Nes.Ppu.Write(offset, value);
            }
            else if (address >= 0x4000 && address <= 0x4013)
            {
                throw new Exception("APU not yet implemented");
            }
            else if (address >= 0x4014 && address <= 0x4017)
            {
                throw new Exception("DMA not yet implemented");
            }
            else
            {
                Nes.Cpu.Memory.Write(address, value);
            }
        }

        protected bool InMapperRange(ushort address)
        {
            if (address >= Nes.Cart.Mapper.CPUStartRange && address <= Nes.Cart.Mapper.CPUEndRange)
            {
                return true;
            }

            return false;
        }
    }
}