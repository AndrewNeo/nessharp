using System;

namespace NesSharp.Memory
{    
    public ref struct MemoryMapResponse
    {
        public MemoryMapResponse(byte[] memory, ushort offset, bool writable = false) : this(memory.AsSpan(), offset, writable) { }
        public MemoryMapResponse(Span<byte> memory, ushort offset, bool writable = false)
        {
            this.memory = memory;
            this.offset = offset;
            this.writable = writable;
        }

        Span<byte> memory;
        ushort offset;
        bool writable;

        public byte ReadByte(ushort address)
        {
            return memory[address - offset];
        }

        public ushort ReadAddress(ushort address)
        {
            return BitConverter.ToUInt16(memory.Slice(address - offset, 2));
        }

        public void Write(ushort address, byte value)
        {
            if (writable)
            {
                memory[address - offset] = value;
            }
        }

        public byte[] ToArray()
        {
            return this.memory.ToArray();
        }
    }
}