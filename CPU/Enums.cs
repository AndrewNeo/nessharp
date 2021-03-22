using System;
using System.Collections;

namespace NesSharp.CPU
{
    public struct NesCpuState : IResettable
    {
        /// <summary>Number of ticks remaining before next operation</summary>
        public int CycleTicksRemain;

        /// <summary>Program counter register</summary>
        public ushort PC;

        /// <summary>Accumulator register</summary>
        public byte A;

        /// <summary>X index register</summary>
        public byte X;

        /// <summary>Y index register</summary>
        public byte Y;

        /// <summary>Stack pointer register</summary>
        public byte S;

        /// <summary>Processor status register</summary>
        public byte P
        {
            get
            {
                if (StatusRegister == null)
                {
                    return 0x0;
                }

                byte[] b = new byte[1];
                StatusRegister.CopyTo(b, 0);
                return b[0];
            }
            set
            {
                var tempReg = new BitArray(new byte[] { value });
                SetStatusFlag(StatusFlags.Carry, tempReg[7]);
                SetStatusFlag(StatusFlags.Zero, tempReg[6]);
                SetStatusFlag(StatusFlags.Interrupt, tempReg[5]);
                SetStatusFlag(StatusFlags.Decimal, tempReg[4]);
                // Break can't be set when overwriting P
                SetStatusFlag(StatusFlags.Always1, true);
                SetStatusFlag(StatusFlags.Overflow, tempReg[1]);
                SetStatusFlag(StatusFlags.Negative, tempReg[0]);
            }
        }

        private BitArray StatusRegister;

        public bool GetStatusFlag(StatusFlags flag)
        {
            if (StatusRegister == null)
            {
                return false;
            }

            return StatusRegister.Get((7 - (int)flag));
        }

        public void SetStatusFlag(StatusFlags flag, bool value)
        {
            if (StatusRegister == null)
            {
                // Start with Always1 set
                StatusRegister = new BitArray(new byte[] { 0x20 });
            }

            // 8 - flag because little endian
            StatusRegister.Set((7 - (int)flag), value);
        }

        public void SoftReset()
        {
            // TODO: Verify
            CycleTicksRemain = 0;
            S = (byte)(S - 3);
            SetStatusFlag(StatusFlags.Interrupt, true);
        }

        public void HardReset()
        {
            CycleTicksRemain = 0;
            PC = 0;
            A = 0;
            X = 0;
            Y = 0;
            S = 0;
            P = 0x34;
        }
    }

    public enum AddressingMode : int
    {
        /// <summary>Implied addressing, can't return an address</summary>
        Implicit = 0,
        /// <summary>Special mode for using A as the operand</summary>
        Accumulator,
        /// <summary>XXX #nn</summary>
        Immediate,
        /// <summary>XXX nn</summary>
        ZeroPage,
        /// <summary>XXX nn,X</summary>
        ZeroPageX,
        /// <summary>XXX nn,Y</summary>
        ZeroPageY,
        /// <summary>XXX nnnn</summary>
        Absolute,
        /// <summary>XXX nnnn,X</summary>
        AbsoluteX,
        /// <summary>XXX nnnn,Y</summary>
        AbsoluteY,
        /// <summary>XXX (nnnn)</summary>
        Indirect,
        /// <summary>XXX (nn,X)</summary>
        IndirectX,
        /// <summary>XXX (nn),Y</summary>
        IndirectY,
        /// <summary>XXX nnn</summary>
        Relative
    }

    public enum StatusFlagBytes : byte
    {
        /// <summary>C flag - 0=no carry, 1=carry</summary>
        Carry = 0x1,
        /// <summary>Z flagm 0=nonzero, 1=zero</summary>
        Zero = 0x2,
        /// <summary>I flag, 0=IRQ enable, 1=IRQ disable</summary>
        Interrupt = 0x4,
        /// <summary>Unused. D flag, 0=normal, 1=bcd mode</summary>
        Decimal = 0x8,
        /// <summary>B flag, 0=IRQ/NMI, 1=reset or BRK/PHP</summary>
        Break = 0x10,
        /// <summary>Unused. (always 1)</summary>
        Always1 = 0x20,
        /// <summary>V flag (0=no overflow, 1=overflow)</summary>
        Overflow = 0x40,
        /// <summary>N flag (0=positive, 1=negative)</summary>
        Negative = 0x80,
    }

    public enum StatusFlags : int
    {
        /// <summary>C flag (0=no carry, 1=carry)</summary>
        Carry = 0,
        /// <summary>Z flag (0=nonzero, 1=zero)</summary>
        Zero = 1,
        /// <summary>I flag (0=IRQ enable, 1=IRQ disable)</summary>
        Interrupt = 2,
        /// <summary>Unused. D flag (0=normal, 1=BCD mode)</summary>
        Decimal = 3,
        /// <summary>B flag (0=IRQ/NMI, 1=reset or BRK/PHP)</summary>
        Break = 4,
        /// <summary>Unused. (Always 1)</summary>
        Always1 = 5,
        /// <summary>V flag (0=no overflow, 1=overflow)</summary>
        Overflow = 6,
        /// <summary>N flag (0=positive, 1=negative)</summary>
        Negative = 7,
    }

    enum InterruptMode
    {
        NMI,
        Reset,
        IRQ,
        Break,
    }

    enum FixedJumpVector : ushort
    {
        NMI = 0xFFFA,
        Reset = 0xFFFC,
        IRQ = 0xFFFE,
        BRK = 0xFFFE
    }
}