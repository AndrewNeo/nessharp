using System;

namespace NesSharp
{
    public static class NesConsts
    {
        public const ushort PrgPageSize = 8 * 1024;
        public const ushort ChrPageSize = 1 * 1024;
        public const ushort MEM_STACK_START = 0x100;
        public const ushort IMAGE_BUFFER_SIZE = 256 * 240;
    }

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

        public byte ReadByte(ushort i)
        {
            return memory[i - offset];
        }

        public ushort ReadUShort(ushort i)
        {
            return BitConverter.ToUInt16(memory.Slice(i - offset, 2));
        }

        public void Write(ushort i, byte d)
        {
            if (writable)
            {
                memory[i - offset] = d;
            }
        }

        public byte[] ToArray()
        {
            return this.memory.ToArray();
        }
    }
}