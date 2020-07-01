using System;
using NesSharp.Memory;

namespace NesSharp.Cart.Mappers
{
    [Mapper(0)]
    [MemoryMap(AddressBus.CPU, 0x6000, 0xFFFF)]
    [MemoryMap(AddressBus.PPU, 0x0000, 0x1FFF)]
    class Mapper0 : NesCartMapper
    {
        public Mapper0(Nes nes) : base(nes) { }

        protected override MemoryMapResponse MapCpuMemory(ushort address)
        {
            if (address >= 0x6000 && address <= 0x7FFF)
            {
                // PRG RAM on Family Basic only
                if (Nes.Cart.Header.hasPrgRam)
                {
                    return new MemoryMapResponse(MemoryMapOrigin.CartProgramRam, PrgRam, GetPrgRamOffset(0x6000, address), true);
                }
                else
                {
                    return new MemoryMapResponse(MemoryMapOrigin.OpenBus, OpenBus, address, true);
                }
            }
            else if (address >= 0x8000 && address <= 0xBFFF)
            {
                // First 16kb of ROM
                return new MemoryMapResponse(MemoryMapOrigin.CartProgramRom, Nes.Cart.PrgRom, 0x8000, false);
            }
            else if (address >= 0xC000 && address <= 0xFFFF)
            {
                if (Nes.Cart.Header.PrgRomPages == 1)
                {
                    // Mirror of 0x8000-0xBFFF
                    return new MemoryMapResponse(MemoryMapOrigin.CartProgramRom, Nes.Cart.PrgRom, 0x8000, false);
                }
                else
                {
                    // Last 16kb of ROM
                    return new MemoryMapResponse(MemoryMapOrigin.CartProgramRom, GetPrgRomBank((byte)(Nes.Cart.Header.PrgRomPages - 1), 2), 0xC000, false);
                }
            }

            return base.MapCpuMemory(address);
        }

        protected override MemoryMapResponse MapPpuMemory(ushort address)
        {
            if (address >= 0x0 && address <= 0x1FFF)
            {
                return new MemoryMapResponse(MemoryMapOrigin.CartCharacterRom, Nes.Cart.ChrRom, 0, false);
            }

            return base.MapPpuMemory(address);
        }
    }
}