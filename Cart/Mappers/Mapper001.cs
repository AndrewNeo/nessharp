using System;
using NesSharp.Memory;
using NesSharp.PPU;

namespace NesSharp.Cart.Mappers
{
    [Mapper(1)]
    [MemoryMap(AddressBus.CPU, 0x6000, 0xFFFF)]
    [MemoryMap(AddressBus.PPU, 0x0000, 0x1FFF)]
    class Mapper1 : NesCartMapper
    {
        public Mapper1(Nes nes) : base(nes)
        {
            Registers = new byte[4];
        }

        private readonly byte[] Registers;
        private byte intWriteNum;
        private byte intTempRegister;

        protected override bool WriteRegister(ushort i, byte d)
        {
            if (i < 0x8000)
            {
                return false;
            }

            if ((d & 0x80) > 0)
            {
                // Reset
                intWriteNum = 0;
                intTempRegister = 0;
                Registers[0] |= 0x0C;
            }
            else
            {
                intTempRegister = (byte)(((d & 1) << 4) | (intTempRegister >> 1));
                intWriteNum++;
                if (intWriteNum == 5)
                {
                    Registers[(i >> 13) & 0b11] = intTempRegister;
                    intWriteNum = 0;
                    intTempRegister = 0;
                }
            }

            // Update mirror setting
            switch (Registers[0] & 0b11)
            {
                case 2:
                    Nes.Ppu.MirrorMode = MirrorMode.Vertical;
                    break;
                case 3:
                    Nes.Ppu.MirrorMode = MirrorMode.Horizontal;
                    break;
            }

            return true;
        }

        protected override MemoryMapResponse MapCpuMemory(ushort address)
        {
            if (address >= 0x6000 && address <= 0x7FFF)
            {
                if (Nes.Cart.Header.hasPrgRam)
                {
                    return new MemoryMapResponse(MemoryMapOrigin.CartProgramRam, PrgRam, GetPrgRamOffset(0x6000, address), true);
                }
                else
                {
                    return new MemoryMapResponse(MemoryMapOrigin.OpenBus, OpenBus, address, true);
                }
            }
            else if (address >= 0x8000 && address <= 0xFFFF)
            {
                var swappableBank = (byte)(Registers[3] & 0xF);

                if ((Registers[0] & 0b1000) > 0)
                {
                    // 16kb PRG
                    if ((Registers[0] & 0b100) > 0)
                    {
                        // First bank swappable, second bank fixed to 15
                        if (address >= 0x8000 && address <= 0xBFFF)
                        {
                            return new MemoryMapResponse(MemoryMapOrigin.CartProgramRom, GetPrgRomBank(swappableBank, 2), 0x8000, false);
                        }
                        else if (address >= 0xC000 && address <= 0xFFFF)
                        {
                            return new MemoryMapResponse(MemoryMapOrigin.CartProgramRom, GetPrgRomBank(15), 0xC000, false);
                        }
                    }
                    else
                    {
                        // First bank fixed to 0, second bank swappable
                        if (address >= 0x8000 && address <= 0xBFFF)
                        {
                            return new MemoryMapResponse(MemoryMapOrigin.CartProgramRom, GetPrgRomBank(0), 0x8000, false);
                        }
                        else if (address >= 0xC000 && address <= 0xFFFF)
                        {
                            return new MemoryMapResponse(MemoryMapOrigin.CartProgramRom, GetPrgRomBank(swappableBank, 2), 0xC000, false);
                        }
                    }
                }
                else
                {
                    // 32kb PRG
                    return new MemoryMapResponse(MemoryMapOrigin.CartProgramRom, GetPrgRomBank((byte)(swappableBank >> 1), 4), 0x8000, false);
                }
            }

            return base.MapCpuMemory(address);
        }

        protected override MemoryMapResponse MapPpuMemory(ushort address)
        {
            if (address >= 0x0 && address <= 0x1FFF)
            {
                if ((Registers[0] & 0b10000) > 0)
                {
                    // 4kb CHR
                    if (address >= 0x0 && address <= 0x0FFF)
                    {
                        return new MemoryMapResponse(MemoryMapOrigin.CartCharacterRom, GetChrRomBank(Registers[1], 4), 0, false);
                    }
                    else if (address >= 0x1000 && address <= 0x1FFF)
                    {
                        return new MemoryMapResponse(MemoryMapOrigin.CartCharacterRom, GetChrRomBank(Registers[2], 4), 0x1000, false);
                    }
                }
                else
                {
                    // 8kb CHR
                    return new MemoryMapResponse(MemoryMapOrigin.CartCharacterRom, GetChrRomBank((byte)(Registers[1] >> 1), 8), 0x0, false);
                }
            }

            return base.MapPpuMemory(address);
        }
    }
}