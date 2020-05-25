using System;

namespace NesSharp.Memory
{
    public abstract class NesMemory : IMemoryAccess
    {
        protected byte[] OpenBus = new byte[3];

        public byte ReadByte(ushort address)
        {
            var mmr = MapMemory(address);
            return mmr.ReadByte(address);
        }

        public ushort ReadAddress(ushort address)
        {
            var mmr = MapMemory(address);
            return mmr.ReadAddress(address);
        }

        public void Write(ushort address, byte value)
        {
            var mmr = MapMemory(address);
            mmr.Write(address, value);
        }

        protected abstract MemoryMapResponse MapMemory(ushort pos);
    }
}