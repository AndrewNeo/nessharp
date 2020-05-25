using System;

namespace NesSharp.Memory
{
    interface IMemoryAccess
    {
        byte ReadByte(ushort address);
        ushort ReadAddress(ushort address);
        void Write(ushort address, byte data);
    }
}