using System;
using NesSharp.Mappers;

namespace NesSharp
{
    public abstract class NesCartMapper
    {
        protected NesCartMapper(Nes nes)
        {
            this.Nes = nes;
        }

        protected Nes Nes;

        public static NesCartMapper Build(Nes nes, NesCartHeader header)
        {
            var mapper = header.MapperNumber;
            if (mapper == 4)
            {
                return new Mapper4(nes);
            }
            else
            {
                throw new UnsupportedMapperException(mapper);
            }
        }

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
            return mmr.ReadUShort(i);
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
            return mmr.ReadUShort(i);
        }

        public void PpuWrite(ushort i, byte d)
        {
            Nes.Debugger.Log(NesDebugger.TAG_MAP, "PPU attempting to write {1:x} to {0:x}", i, d);
            var mmr = MapPpuMemory(i);
            mmr.Write(i, d);
        }

        protected bool WriteRegister(ushort i, byte d)
        {
            return false;
        }

        public void Scanline() { }

        protected MemoryMapResponse MapCpuMemory(ushort pos)
        {
            if (pos >= 0x4020 && pos <= 0x5FFF)
            {
                return new MemoryMapResponse(Nes.Cart.ExpansionArea, 0x4000, true);
            }

            throw new Exception(String.Format("Unhandled CPU memory access at 0x{0:x}", pos));
        }

        protected MemoryMapResponse MapPpuMemory(ushort pos)
        {
            throw new Exception(String.Format("Unhandled PPU memory access at 0x{0:x}", pos));
        }

    }

    public class UnsupportedMapperException : Exception
    {
        public UnsupportedMapperException(byte mapperId) : base(String.Format("Unsupported mapper: {0:x}", mapperId)) { }
    }
}