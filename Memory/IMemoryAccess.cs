using System;

namespace NesSharp.Memory
{
    interface IMemoryAccess
    {
        byte ReadByte(ushort address, bool quiet = false);
        ushort ReadAddress(ushort address, bool quiet = false);
        void Write(ushort address, byte data);
    }
}