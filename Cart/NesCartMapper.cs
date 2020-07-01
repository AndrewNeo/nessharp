using System;
using System.Linq;
using NesSharp.Debugger;
using NesSharp.Memory;
using NesSharp.Utils;

namespace NesSharp.Cart
{
    public abstract class NesCartMapper
    {
        public static NesCartMapper Build(Nes nes, NesCartHeader header)
        {
            var mapperId = header.MapperNumber;
            var mapper = GetMapper(nes, mapperId);
            return mapper;
        }

        private static NesCartMapper GetMapper(Nes nes, int id)
        {
            var targetedMapper = typeof(NesCartMapper)
                .Assembly.GetTypes()
                .FirstOrDefault(s => s.GetCustomAttributes(typeof(MapperAttribute), false)
                    .Cast<MapperAttribute>()
                    .FirstOrDefault()
                    ?.MapperNumber == id
                );

            if (targetedMapper == null)
            {
                throw new MapperNotImplemented(id);
            }

            return (NesCartMapper)Activator.CreateInstance(targetedMapper, new object[] { nes });
        }

        protected Nes Nes;

        protected readonly byte[] OpenBus;
        protected readonly byte[] PrgRam;

        protected NesCartMapper(Nes nes)
        {
            this.Nes = nes;

            CPUStartRange = GetMemoryMapAttribute(AddressBus.CPU).Start;
            CPUEndRange = GetMemoryMapAttribute(AddressBus.CPU).End;
            PPUStartRange = GetMemoryMapAttribute(AddressBus.PPU).Start;
            PPUEndRange = GetMemoryMapAttribute(AddressBus.PPU).End;

            OpenBus = new byte[3];
            PrgRam = new byte[8 * 1024];
        }

        // Mapper handling range definitions

        private MemoryMapAttribute GetMemoryMapAttribute(AddressBus bus)
        {
            var attrs = this.GetType().GetCustomAttributes(typeof(MemoryMapAttribute), true).Cast<MemoryMapAttribute>();
            return attrs.First(a => a.Bus == bus);
        }

        public ushort CPUStartRange { get; private set; }

        public ushort CPUEndRange { get; private set; }

        public ushort PPUStartRange { get; private set; }

        public ushort PPUEndRange { get; private set; }

        // I/O

        public byte CpuReadByte(ushort i, bool quiet = false)
        {
            if (!quiet)
            {
                Nes.Debugger.Log(NesDebugger.TAG_MAP, "CPU reading byte from cart at 0x{0:X2}", i);
            }
            var mmr = MapCpuMemory(i);
            return mmr.ReadByte(i);
        }

        public ushort CpuReadUShort(ushort i, bool quiet = false)
        {
            if (!quiet)
            {
                Nes.Debugger.Log(NesDebugger.TAG_MAP, "CPU reading ushort from cart at 0x{0:X4}", i);
            }
            var mmr = MapCpuMemory(i);
            return mmr.ReadAddress(i);
        }

        public void CpuWrite(ushort i, byte d)
        {
            Nes.Debugger.Log(NesDebugger.TAG_MAP, "CPU attempting to write {1:X2} to cart at 0x{0:X4}", i, d);
            if (!WriteRegister(i, d))
            {
                var mmr = MapCpuMemory(i);
                mmr.Write(i, d);
            }
        }

        public byte PpuReadByte(ushort i, bool quiet = false)
        {
            if (!quiet)
            {
                Nes.Debugger.Log(NesDebugger.TAG_MAP, "PPU reading byte from cart at 0x{0:X4}", i);
            }
            var mmr = MapPpuMemory(i);
            return mmr.ReadByte(i);
        }

        public ushort PpuReadUShort(ushort i, bool quiet = false)
        {
            if (!quiet)
            {
                Nes.Debugger.Log(NesDebugger.TAG_MAP, "PPU reading ushort from cart at 0x{0:X4}", i);
            }
            var mmr = MapPpuMemory(i);
            return mmr.ReadAddress(i);
        }

        public void PpuWrite(ushort i, byte d)
        {
            Nes.Debugger.Log(NesDebugger.TAG_MAP, "PPU attempting to write {1:X2} to cart at 0x{0:X4}", i, d);
            var mmr = MapPpuMemory(i);
            mmr.Write(i, d);
        }

        // Overrides

        protected virtual bool WriteRegister(ushort i, byte d)
        {
            return false;
        }

        public virtual void Scanline() { }

        protected virtual MemoryMapResponse MapCpuMemory(ushort address)
        {
            if (address >= 0x4020 && address <= 0x5FFF)
            {
                return new MemoryMapResponse(MemoryMapOrigin.CartExpansionArea, Nes.Cart.ExpansionArea, 0x4000, true);
            }

            throw new UnderhandledMemoryException(AddressBus.CPU, address);
        }

        protected virtual MemoryMapResponse MapPpuMemory(ushort address)
        {
            throw new UnderhandledMemoryException(AddressBus.PPU, address);
        }

        protected Span<byte> GetPrgRomBank(byte page, byte pageSizeMul = 1)
        {
            if (Nes.Cart.Header.PrgRomPages < 1)
            {
                return OpenBus;
            }

            // Is this right?
            page = (byte)(page % Nes.Cart.Header.PrgRomPages);

            return this.Nes.Cart.PrgRom.AsSpan(page * NesConsts.PrgPageSize * pageSizeMul, NesConsts.PrgPageSize * pageSizeMul);
        }

        protected Span<byte> GetChrRomBank(byte page, byte pageSizeMul = 1)
        {
            if (Nes.Cart.Header.ChrRomPages < 1)
            {
                return OpenBus;
            }

            // Is this right?
            page = (byte)(page % Nes.Cart.Header.ChrRomPages);

            return this.Nes.Cart.ChrRom.AsSpan(page * NesConsts.ChrPageSize * pageSizeMul, NesConsts.ChrPageSize * pageSizeMul);
        }

        protected ushort GetPrgRamOffset(ushort baseAddress, ushort address)
        {
            if (Nes.Cart.Header.PrgRamPages < 1)
            {
                return baseAddress;
            }

            return (ushort)(address % Nes.Cart.Header.PrgRamPages);
        }
    }
}