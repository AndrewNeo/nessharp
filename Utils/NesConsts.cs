using System;

namespace NesSharp.Utils
{
    public static class NesConsts
    {
        public const uint FPS = 60;
        public const uint FRAME_DELAY_MS = 1000 / FPS;

        public const ushort PrgPageSize = 8 * 1024;
        public const ushort ChrPageSize = 1 * 1024;
        public const ushort MEM_STACK_START = 0x100;

        public const ushort IMAGE_BUFFER_SIZE = 256 * 240;
    }
}