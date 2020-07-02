using System;

namespace NesSharp.Memory
{
    public ref struct MemoryMapResponse
    {
        public MemoryMapResponse(MemoryMapOrigin origin, byte[] memory, ushort offset, bool writable = false, ushort repeat = 0)
            : this(origin, memory.AsSpan(), offset, writable, repeat) { }

        public MemoryMapResponse(MemoryMapOrigin origin, Span<byte> memory, ushort offset, bool writable = false, ushort repeat = 0)
        {
            this.origin = origin;
            this.memory = memory;
            this.offset = offset;
            this.writable = writable;
            this.repeat = repeat;
        }

        MemoryMapOrigin origin;
        Span<byte> memory;
        ushort offset;
        bool writable;
        ushort repeat;

        public byte ReadByte(ushort address)
        {
            if (repeat > 0)
            {
                address = (ushort)(address % repeat);
            }

            return memory[address - offset];
        }

        public ushort ReadAddress(ushort address)
        {
            return (ushort)((ReadByte(address) | (ReadByte((ushort)(address + 1)) << 8)));
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

    public enum MemoryMapOrigin
    {
        OpenBus,
        CpuBidirectionalIO,
        CpuWorkingRam,
        PpuVram,
        PpuPaletteRam,
        CartExpansionArea,
        CartProgramRam,
        CartProgramRom,
        CartCharacterRom,
    }
}