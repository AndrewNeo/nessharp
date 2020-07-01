using System;
using NesSharp.Memory;
using NesSharp.PPU;
using NesSharp.Utils;

namespace NesSharp.Cart.Mappers
{
    [Mapper(4)]
    [MemoryMap(AddressBus.CPU, 0x6000, 0xFFFF)]
    [MemoryMap(AddressBus.PPU, 0x0000, 0x4000)]
    class Mapper4 : NesCartMapper
    {
        public Mapper4(Nes nes) : base(nes)
        {
            Registers = new byte[8];
        }

        private readonly byte[] Registers;

        private byte NextWriteBank;
        private bool PrgRomBankMode;
        private bool ChrA12InversionMode;
        private bool NametableMirroring;
        private bool PrgRamEnabled;
        private bool PrgRamWritesDisabled;
        private byte IRQPeriod;
        private byte IRQCounter;
        private bool IRQEnabled;

        private byte PrgRomPageSize
        {
            get
            {
                // PrgRomPages in header is 16KB but this mapper is 8KB
                return (byte)(this.Nes.Cart.Header.PrgRomPages / 2);
            }
        }

        protected override bool WriteRegister(ushort i, byte d)
        {
            if (i < 0x8000)
            {
                return false;
            }

            ushort baseline = (ushort)(i & 0xE001);
            var bits = FastBits.Get(d);

            switch (baseline)
            {
                case 0x8000:
                    // Bank select
                    NextWriteBank = (byte)(d & 0x7);
                    PrgRomBankMode = bits[6];
                    ChrA12InversionMode = bits[7];
                    return true;
                case 0x8001:
                    // Bank data
                    if (NextWriteBank == 0 || NextWriteBank == 1)
                    {
                        Registers[NextWriteBank] = (byte)(d & 0x1);
                    }
                    else
                    {
                        Registers[NextWriteBank] = d;
                    }
                    return true;
                case 0xA000:
                    // Mirroring
                    NametableMirroring = bits[0];
                    Nes.Ppu.MirrorMode = NametableMirroring ? MirrorMode.Horizontal : MirrorMode.Vertical;
                    return true;
                case 0xA001:
                    // PRG RAM protect
                    PrgRamWritesDisabled = bits[6];
                    PrgRamEnabled = bits[7];
                    return true;
                case 0xC000:
                    // IRQ latch
                    IRQPeriod = d;
                    return true;
                case 0xC001:
                    // IRQ reload
                    IRQCounter = 0;
                    return true;
                case 0xE000:
                    // IRQ disable
                    IRQEnabled = false;
                    Nes.Cpu.ActiveIrq = false;
                    return true;
                case 0xE001:
                    // IRQ enable
                    IRQEnabled = true;
                    return true;
            }

            return false;
        }

        public override void Scanline()
        {
            if (IRQCounter == 0)
            {
                IRQCounter = IRQPeriod;
            }
            else
            {
                IRQCounter--;
            }

            if (IRQEnabled && IRQCounter == 0)
            {
                this.Nes.Cpu.ActiveIrq = true;
            }
        }

        protected override MemoryMapResponse MapCpuMemory(ushort address)
        {
            if (address >= 0x6000 && address <= 0x7FFF)
            {
                // PRG RAM bank
                if (Nes.Cart.Header.hasPrgRam && PrgRamEnabled)
                {
                    return new MemoryMapResponse(MemoryMapOrigin.CartProgramRam, PrgRam, 0x6000, !PrgRamWritesDisabled);
                }
                else
                {
                    // open bus
                    return new MemoryMapResponse(MemoryMapOrigin.OpenBus, OpenBus, address, false);
                }
            }
            else if (address >= 0x8000 && address <= 0x9FFF)
            {
                // Switchable PRG ROM bank
                if (PrgRomBankMode)
                {
                    return new MemoryMapResponse(MemoryMapOrigin.CartProgramRom, GetPrgRomBank((byte)(PrgRomPageSize - 1)), 0x8000, false);
                }
                else
                {
                    var page = Registers[1];
                    return new MemoryMapResponse(MemoryMapOrigin.CartProgramRom, GetPrgRomBank(Registers[6]), 0x8000, false);
                }
            }
            else if (address >= 0xA000 && address <= 0xBFFF)
            {
                // Switchable PRG ROM bank, always R7
                return new MemoryMapResponse(MemoryMapOrigin.CartProgramRom, GetPrgRomBank(Registers[7]), 0xA000, false);
            }
            else if (address >= 0xC000 && address <= 0xDFFF)
            {
                // Switchable PRG ROM bank
                if (!PrgRomBankMode)
                {
                    return new MemoryMapResponse(MemoryMapOrigin.CartProgramRom, GetPrgRomBank((byte)(PrgRomPageSize - 2)), 0xC000, false);
                }
                else
                {
                    return new MemoryMapResponse(MemoryMapOrigin.CartProgramRom, GetPrgRomBank(Registers[6]), 0xC000, false);
                }
            }
            else if (address >= 0xE000 && address <= 0xFFFF)
            {
                // PRG ROM bank, Fixed to last
                return new MemoryMapResponse(MemoryMapOrigin.CartProgramRom, GetPrgRomBank((byte)(PrgRomPageSize - 1)), 0xE000, false);
            }

            return base.MapCpuMemory(address);
        }

        protected override MemoryMapResponse MapPpuMemory(ushort address)
        {
            if (address >= 0x0000 && address <= 0x07FF)
            {
                if (ChrA12InversionMode)
                {
                    if (address < 0x0400)
                    {
                        return new MemoryMapResponse(MemoryMapOrigin.CartCharacterRom, GetChrRomBank(Registers[2]), 0x0000, false);
                    }
                    else
                    {
                        return new MemoryMapResponse(MemoryMapOrigin.CartCharacterRom, GetChrRomBank(Registers[3]), 0x0400, false);
                    }
                }
                else
                {
                    return new MemoryMapResponse(MemoryMapOrigin.CartCharacterRom, GetChrRomBank(Registers[0], 2), 0x0000, false);
                }
            }
            else if (address >= 0x0800 && address <= 0x0FFF)
            {
                if (ChrA12InversionMode)
                {
                    if (address < 0x0C00)
                    {
                        return new MemoryMapResponse(MemoryMapOrigin.CartCharacterRom, GetChrRomBank(Registers[4]), 0x0800, false);
                    }
                    else
                    {
                        return new MemoryMapResponse(MemoryMapOrigin.CartCharacterRom, GetChrRomBank(Registers[5]), 0x0C00, false);
                    }
                }
                else
                {
                    return new MemoryMapResponse(MemoryMapOrigin.CartCharacterRom, GetChrRomBank(Registers[1], 2), 0x0800, false);
                }
            }
            else if (address >= 0x1000 && address <= 0x17FF)
            {
                if (!ChrA12InversionMode)
                {
                    if (address < 0x1400)
                    {
                        return new MemoryMapResponse(MemoryMapOrigin.CartCharacterRom, GetChrRomBank(Registers[2]), 0x1000, false);
                    }
                    else
                    {
                        return new MemoryMapResponse(MemoryMapOrigin.CartCharacterRom, GetChrRomBank(Registers[3]), 0x1400, false);
                    }
                }
                else
                {
                    return new MemoryMapResponse(MemoryMapOrigin.CartCharacterRom, GetChrRomBank(Registers[0], 2), 0x1000, false);
                }
            }
            else if (address >= 0x1800 && address <= 0x1FFF)
            {
                if (!ChrA12InversionMode)
                {
                    if (address < 0x1C00)
                    {
                        return new MemoryMapResponse(MemoryMapOrigin.CartCharacterRom, GetChrRomBank(Registers[4]), 0x1800, false);
                    }
                    else
                    {
                        return new MemoryMapResponse(MemoryMapOrigin.CartCharacterRom, GetChrRomBank(Registers[5]), 0x1C00, false);
                    }
                }
                else
                {
                    return new MemoryMapResponse(MemoryMapOrigin.CartCharacterRom, GetChrRomBank(Registers[1], 2), 0x1800, false);
                }
            }

            return base.MapPpuMemory(address);
        }
    }
}