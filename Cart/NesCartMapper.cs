using System;
using System.Linq;
using NesSharp.Cart.Mappers;
using NesSharp.Memory;

namespace NesSharp.Cart
{
    public abstract class NesCartMapper
    {
        public static NesCartMapper Build(Nes nes, NesCartHeader header)
        {
            var mapper = header.MapperNumber;

            // TODO: Convert this to look up mapper by MapperAttribute
            if (mapper == 4)
            {
                return new Mapper4(nes);
            }
            else
            {
                throw new UnsupportedMapperException(mapper);
            }
        }

        protected Nes Nes;

        protected NesCartMapper(Nes nes)
        {
            this.Nes = nes;

            CPUStartRange = GetMemoryMapAttribute(AddressBus.CPU).Start;
            CPUEndRange = GetMemoryMapAttribute(AddressBus.CPU).End;
            PPUStartRange = GetMemoryMapAttribute(AddressBus.PPU).Start;
            PPUEndRange = GetMemoryMapAttribute(AddressBus.PPU).End;
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

        public byte CpuReadByte(ushort i)
        {
            Nes.Debugger.Log(NesDebugger.TAG_MAP, "CPU reading byte from {0:x}", i);
            var mmr = MapCpuMemory(i);
            return mmr.ReadByte(i);
        }

        public ushort CpuReadUShort(ushort i)
        {
            Nes.Debugger.Log(NesDebugger.TAG_MAP, "CPU reading ushort from {0:x}", i);
            var mmr = MapCpuMemory(i);
            return mmr.ReadAddress(i);
        }

        public void CpuWrite(ushort i, byte d)
        {
            Nes.Debugger.Log(NesDebugger.TAG_MAP, "CPU attempting to write {1:x} to {0:x}", i, d);
            if (!WriteRegister(i, d))
            {
                var mmr = MapCpuMemory(i);
                mmr.Write(i, d);
            }
        }

        public byte PpuReadByte(ushort i)
        {
            Nes.Debugger.Log(NesDebugger.TAG_MAP, "PPU reading byte from {0:x}", i);
            var mmr = MapPpuMemory(i);
            return mmr.ReadByte(i);
        }

        public ushort PpuReadUShort(ushort i)
        {
            Nes.Debugger.Log(NesDebugger.TAG_MAP, "PPU reading ushort from {0:x}", i);
            var mmr = MapPpuMemory(i);
            return mmr.ReadAddress(i);
        }

        public void PpuWrite(ushort i, byte d)
        {
            Nes.Debugger.Log(NesDebugger.TAG_MAP, "PPU attempting to write {1:x} to {0:x}", i, d);
            var mmr = MapPpuMemory(i);
            mmr.Write(i, d);
        }

        // Overrides

        protected virtual bool WriteRegister(ushort i, byte d)
        {
            return false;
        }

        public virtual void Scanline() { }

        protected virtual MemoryMapResponse MapCpuMemory(ushort pos)
        {
            if (pos >= 0x4020 && pos <= 0x5FFF)
            {
                return new MemoryMapResponse(Nes.Cart.ExpansionArea, 0x4000, true);
            }

            throw new UnderhandledMemoryException(AddressBus.CPU, pos);
        }

        protected virtual MemoryMapResponse MapPpuMemory(ushort pos)
        {
            throw new UnderhandledMemoryException(AddressBus.PPU, pos);
        }
    }

    public class UnsupportedMapperException : Exception
    {
        public UnsupportedMapperException(byte mapperId) : base(String.Format("Unsupported mapper: {0:x}", mapperId)) { }
    }
}