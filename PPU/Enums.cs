using System;

namespace NesSharp.PPU
{
    public enum MirrorMode : byte
    {
        Vertical = 0,
        Horizontal = 1
    }

    enum ScanlineBand
    {
        Visible,
        AfterVisible,
        NMI,
        PreNext
    }
}