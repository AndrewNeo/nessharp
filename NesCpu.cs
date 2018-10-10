using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace NesSharp
{
    class NesCpu
    {
        public NesCpu(Nes nes)
        {
            this.Nes = nes;
            nes.Clock += Update;

            CurrentState = new NesCpuState();
        }

        protected Nes Nes { get; set; }

        private NesCpuState CurrentState { get; set; }

        public void Update()
        {
            RunCycle();
        }

        private void RunCycle()
        {
            if (CurrentState.CycleTicksRemain > 0)
            {
                CurrentState.CycleTicksRemain--;
                return;
            }

            CurrentState.PC++;

            var instruction = Nes.Cart.Rom.ElementAt(CurrentState.PC);
            CurrentState.PC++;

            // https://problemkaputt.de/everynes.htm
            // TODO: Convert this into a delegate lookup table instead of a switch statement
            switch (instruction)
            {
                case 0x01:
                case 0x05:
                case 0x09:
                case 0x0D:
                case 0x11:
                case 0x15:
                case 0x19:
                case 0x1D:
                    var operand = FetchOperandByte(StaticData.OPTCODE_MEMORY[instruction]);

                    operand |= CurrentState.A;
                    CurrentState.SetStatusFlag(StatusFlags.Negative, (operand > 0x7F));
                    CurrentState.SetStatusFlag(StatusFlags.Zero, (operand == 0x0));
                    CurrentState.A = operand;

                    CurrentState.CycleTicksRemain = 1 + StaticData.ADDRESS_EXPENSES[StaticData.OPTCODE_MEMORY[instruction]];
                    break;
            }

            // This was a tick 
            CurrentState.CycleTicksRemain--;
        }

        public void Reset()
        {
            CurrentState.Reset();
            var jumpVector = Nes.Ram.ReadUShort(0xFFFC);
            CurrentState.PC = jumpVector;
        }

        private byte FetchOperandByte(AddressingMode mode)
        {
            switch (mode)
            {
                case AddressingMode.Immediate:
                    var io = Nes.Ram.ReadByte(CurrentState.PC);
                    CurrentState.PC++;

                    return io;
                case AddressingMode.ZeroPage:
                case AddressingMode.ZeroPageX:
                    int zo = Nes.Ram.ReadByte(CurrentState.PC);
                    if (mode == AddressingMode.ZeroPageX)
                    {
                        zo = zo + CurrentState.X;
                    }
                    var zob = (byte)(((byte)zo) & 0xff); // Zero page ???
                    CurrentState.PC++;

                    return Nes.Ram.ReadByte(zob);
                case AddressingMode.Absolute:
                case AddressingMode.AbsoluteX:
                case AddressingMode.AbsoluteY:
                    var aop = Nes.Ram.ReadUShort(CurrentState.PC);
                    if (mode == AddressingMode.AbsoluteX)
                    {
                        aop += CurrentState.X;
                    }
                    else if (mode == AddressingMode.AbsoluteY)
                    {
                        aop += CurrentState.X;
                    }

                    var ao = Nes.Ram.ReadByte(aop);
                    CurrentState.PC++;

                    return ao;
                case AddressingMode.IndirectX:
                    var ixop = AddBytes(Nes.Ram.ReadByte(CurrentState.PC), CurrentState.X);
                    ixop = (byte)(ixop & 0xff); // Zero page ?
                    CurrentState.PC++;

                    return Nes.Ram.ReadByte(ixop);
                case AddressingMode.IndirectY:
                    var iyop = Nes.Ram.ReadByte(CurrentState.PC);
                    CurrentState.PC++;

                    var ixy = Nes.Ram.ReadByte(iyop);
                    ixy = (byte)(ixy & 0xff); // Zero page (is this right?)
                    return Nes.Ram.ReadByte(AddBytes(ixy, CurrentState.Y));
            }

            throw new Exception("Unhandled AddressingMode");
        }

        private ushort AddBytes(byte a, byte b)
        {
            return (ushort)((a << 8) + b);
        }
    }

    class NesCpuState
    {
        public NesCpuState()
        {
            Reset();
        }

        public int CycleTicksRemain { get; set; }

        public ushort PC { get; set; }

        public byte A { get; set; }

        public byte B { get; set; }

        public byte D { get; set; }

        public byte H { get; set; }

        public byte X { get; set; }

        public byte Y { get; set; }

        public byte S { get; set; }

        public byte P { get; set; }

        public byte Status
        {
            get
            {
                byte[] b = new byte[1];
                // TODO: Make sure the byte doesn't get exported backwards
                StatusRegister.CopyTo(b, 0);
                return b[0];
            }
        }

        private BitArray StatusRegister { get; set; }

        public void SetStatusFlag(StatusFlags flag, bool value)
        {
            StatusRegister.Set((int)flag, value);
        }

        public void Reset()
        {
            CycleTicksRemain = 0;
            PC = 0; // ?
            A = 0;
            X = 0;
            Y = 0;
            S = 0;
            P = 0;
            StatusRegister = new BitArray(8);
        }
    }

    enum AddressingMode
    {
        Unsupported = 0,
        Immediate,
        ZeroPage,
        ZeroPageX,
        Absolute,
        AbsoluteX,
        AbsoluteY,
        IndirectX,
        IndirectY
    }

    enum StatusFlags : int
    {
        Carry = 0x0,
        Zero = 0x1,
        Interrupt = 0x2,
        Decimal = 0x3,
        Break = 0x4,
        Always1 = 0x5,
        Overflow = 0x6,
        Negative = 0x7,
    }

    internal class StaticData
    {
        public readonly static Dictionary<byte, AddressingMode> OPTCODE_MEMORY = new Dictionary<byte, AddressingMode>()
        {
            {0x01, AddressingMode.IndirectX},
            {0x05, AddressingMode.ZeroPage},
            {0x09, AddressingMode.Immediate},
            {0x0D, AddressingMode.Absolute},
            {0x11, AddressingMode.IndirectY},
            {0x15, AddressingMode.ZeroPageX},
            {0x19, AddressingMode.AbsoluteY},
            {0x1D, AddressingMode.AbsoluteX},
        };

        public readonly static Dictionary<AddressingMode, byte> ADDRESS_EXPENSES = new Dictionary<AddressingMode, byte>() {
            {AddressingMode.Immediate, 1},
            {AddressingMode.ZeroPage, 2},
            {AddressingMode.ZeroPageX, 3},
            {AddressingMode.Absolute, 3},
            {AddressingMode.AbsoluteX, 3},
            {AddressingMode.AbsoluteY, 3},
            {AddressingMode.IndirectX, 5},
            {AddressingMode.IndirectY, 4},
        };
    }
}
