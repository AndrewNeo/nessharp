using System;

namespace NesSharp.Mappers
{
    class Mapper4 : NesCartMapper
    {
        public Mapper4(Nes nes) : base(nes)
        {
            PrgRam = new byte[8 * 1024];
            Registers = new byte[8];
        }

        private byte[] PrgRam;
        private byte[] Registers;

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
                // PrgRomPages is 16KB but this mapper is 8KB
                return (byte)(this.Nes.Cart.Header.PrgRomPages * 2);
            }
        }

        protected new bool WriteRegister(ushort i, byte d)
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
                    // TODO: Tell the PPU
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

        protected new MemoryMapResponse MapCpuMemory(ushort pos)
        {
            if (pos >= 0x6000 && pos <= 0x7FFF)
            {
                // PRG RAM bank
                if (Nes.Cart.Header.hasPrgRam && PrgRamEnabled)
                {
                    return new MemoryMapResponse(PrgRam, 0x6000, !PrgRamWritesDisabled);
                }
                else
                {
                    // open bus
                    return new MemoryMapResponse(new byte[NesConsts.PrgPageSize], 0x6000, false);
                }
            }
            else if (pos >= 0x8000 && pos <= 0x9FFF)
            {
                // Switchable PRG ROM bank
                if (PrgRomBankMode)
                {
                    return new MemoryMapResponse(GetPrgRomBank((byte)(PrgRomPageSize - 1)), 0x8000, false);
                }
                else
                {
                    var page = Registers[1];
                    return new MemoryMapResponse(GetPrgRomBank(Registers[6]), 0x8000, false);
                }
            }
            else if (pos >= 0xA000 && pos <= 0xBFFF)
            {
                // Switchable PRG ROM bank, always R7
                return new MemoryMapResponse(GetPrgRomBank(Registers[7]), 0xA000, false);
            }
            else if (pos >= 0xC000 && pos <= 0xDFFF)
            {
                // Switchable PRG ROM bank
                if (!PrgRomBankMode)
                {
                    return new MemoryMapResponse(GetPrgRomBank((byte)(PrgRomPageSize - 2)), 0xC000, false);
                }
                else
                {
                    return new MemoryMapResponse(GetPrgRomBank(Registers[6]), 0xC000, false);
                }
            }
            else if (pos >= 0xE000 && pos <= 0xFFFF)
            {
                // PRG ROM bank, Fixed to last
                return new MemoryMapResponse(GetPrgRomBank((byte)(PrgRomPageSize - 1)), 0xE000, false);
            }

            return base.MapCpuMemory(pos);
        }

        private Span<byte> GetPrgRomBank(byte page)
        {
            return this.Nes.Cart.PrgRom.AsSpan(page * NesConsts.PrgPageSize, NesConsts.PrgPageSize);
        }

        protected new MemoryMapResponse MapPpuMemory(ushort pos)
        {
            if (pos >= 0x0000 && pos <= 0x07FF)
            {
                if (ChrA12InversionMode)
                {
                    if (pos < 0x0400)
                    {
                        return new MemoryMapResponse(GetChrRomBank(Registers[2], 1), 0x0000, false);
                    }
                    else
                    {
                        return new MemoryMapResponse(GetChrRomBank(Registers[3], 1), 0x0400, false);
                    }
                }
                else
                {
                    return new MemoryMapResponse(GetChrRomBank(Registers[0], 2), 0x0000, false);
                }
            }
            else if (pos >= 0x0800 && pos <= 0x0FFF)
            {
                if (ChrA12InversionMode)
                {
                    if (pos < 0x0C00)
                    {
                        return new MemoryMapResponse(GetChrRomBank(Registers[4], 1), 0x0800, false);
                    }
                    else
                    {
                        return new MemoryMapResponse(GetChrRomBank(Registers[5], 1), 0x0C00, false);
                    }
                }
                else
                {
                    return new MemoryMapResponse(GetChrRomBank(Registers[1], 2), 0x0800, false);
                }
            }
            else if (pos >= 0x1000 && pos <= 0x17FF)
            {
                if (!ChrA12InversionMode)
                {
                    if (pos < 0x1400)
                    {
                        return new MemoryMapResponse(GetChrRomBank(Registers[2], 1), 0x1000, false);
                    }
                    else
                    {
                        return new MemoryMapResponse(GetChrRomBank(Registers[3], 1), 0x1400, false);
                    }
                }
                else
                {
                    return new MemoryMapResponse(GetChrRomBank(Registers[0], 2), 0x1000, false);
                }
            }
            else if (pos >= 0x1800 && pos <= 0x1FFF)
            {
                if (!ChrA12InversionMode)
                {
                    if (pos < 0x1C00)
                    {
                        return new MemoryMapResponse(GetChrRomBank(Registers[4], 1), 0x1800, false);
                    }
                    else
                    {
                        return new MemoryMapResponse(GetChrRomBank(Registers[5], 1), 0x1C00, false);
                    }
                }
                else
                {
                    return new MemoryMapResponse(GetChrRomBank(Registers[1], 2), 0x1800, false);
                }
            }

            return base.MapPpuMemory(pos);
        }

        private Span<byte> GetChrRomBank(byte page, byte size)
        {
            return this.Nes.Cart.ChrRom.AsSpan(page * NesConsts.ChrPageSize * size, NesConsts.ChrPageSize * size);
        }

        public new void Scanline()
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
    }
}