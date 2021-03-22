using System;
using NesSharp.CPU;

namespace NesSharp.Debugger
{
    public struct DebugInfo
    {
        public uint Ticks;

        public byte Opcode;
        public ushort PC;
        public byte MapperId;
        public NesCpuState CpuState;
        public byte PpuCtrl;
        public byte PpuMask;
        public byte PpuStatus;
        public ushort PpuScanline;
        public ushort PpuDot;

        // Set after execution
        public ushort? MemoryOperandValue;
        public ushort? ResolvedAddress;
        public AddressingMode? MemoryAddressMode;
        public byte? MemoryValue;
    }
}