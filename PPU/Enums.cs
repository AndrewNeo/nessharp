using System;

namespace NesSharp.PPU
{
    public enum MirrorMode : byte
    {
        Horizontal = 0,
        Vertical = 1
    }

    enum ScanlineBand
    {
        Visible,
        AfterVisible,
        NMI,
        PreNext
    }
}