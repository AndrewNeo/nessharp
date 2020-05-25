using System;

namespace NesSharp.Utils
{
    public static class NesConsts
    {
        public const ushort PrgPageSize = 8 * 1024;
        public const ushort ChrPageSize = 1 * 1024;
        public const ushort MEM_STACK_START = 0x100;
        public const ushort IMAGE_BUFFER_SIZE = 256 * 240;
    }
}