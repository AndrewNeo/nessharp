using System;

namespace NesSharp.Utils
{
    public static class FastBits
    {
        private static bool[][] PRECOMPILED;

        static FastBits()
        {
            PRECOMPILED = new bool[0xFF][];
            for (byte i = 0; i < 0xFF; i++)
            {
                PRECOMPILED[i] = new bool[] {
                    (i & 0x1) == 0x1,
                    (i & 0x2) == 0x2,
                    (i & 0x4) == 0x4,
                    (i & 0x8) == 0x8,
                    (i & 0x10) == 0x10,
                    (i & 0x20) == 0x20,
                    (i & 0x40) == 0x40,
                    (i & 0x80) == 0x80,
                };
            }
        }

        public static bool[] Get(byte b)
        {
            return PRECOMPILED[b];
        }

        public static byte Write(bool[] bits)
        {
            return (byte)(
                (bits[0] ? 0x1 : 0x0) &
                (bits[1] ? 0x2 : 0x0) &
                (bits[2] ? 0x4 : 0x0) &
                (bits[3] ? 0x8 : 0x0) &
                (bits[4] ? 0x10 : 0x0) &
                (bits[5] ? 0x20 : 0x0) &
                (bits[6] ? 0x40 : 0x0) &
                (bits[7] ? 0x80 : 0x0)
            );
        }
    }
}
