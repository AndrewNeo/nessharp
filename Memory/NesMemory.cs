using System;
using NesSharp;
using NesSharp.Debugger;

namespace NesSharp.Memory
{
    public abstract class NesMemory : IMemoryAccess
    {
        protected Nes Nes;
        protected AddressBus Bus;

        protected NesMemory(Nes nes, AddressBus bus)
        {
            this.Nes = nes;
            this.Bus = bus;
        }

        protected readonly byte[] OpenBus = new byte[3];

        public byte ReadByte(ushort address, bool quiet = false)
        {
            if (!quiet)
            {
                Nes.Debugger.Log(NesDebugger.TAG_MAP, "{0} reading byte from internal at 0x{1:X2}", Bus, address);
            }
            var mmr = MapMemory(address);
            return mmr.ReadByte(address);
        }

        public ushort ReadAddress(ushort address, bool quiet = false)
        {
            if (!quiet)
            {
                Nes.Debugger.Log(NesDebugger.TAG_MAP, "{0} reading ushort from internal at 0x{1:X4}", Bus, address);
            }
            var mmr = MapMemory(address);
            return mmr.ReadAddress(address);
        }

        public void Write(ushort address, byte value)
        {
            Nes.Debugger.Log(NesDebugger.TAG_MAP, "{0} attempting to write {2:X2} to internal at 0x{1:X4}", Bus, address, value);
            var mmr = MapMemory(address);
            mmr.Write(address, value);
        }

        protected abstract MemoryMapResponse MapMemory(ushort pos);
    }
}