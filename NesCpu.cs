using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace NesSharp
{
    class NesCpu : IResettable
    {
        public NesCpu(Nes nes)
        {
            this.Nes = nes;
            nes.Clock += Update;

            CurrentState = new NesCpuState();
            CacheOpcodes();
        }

        protected Nes Nes { get; set; }

        private NesCpuState CurrentState { get; set; }

        public void Update()
        {
            if (CurrentState.CycleTicksRemain > 0)
            {
                CurrentState.CycleTicksRemain--;
                return;
            }

            var instruction = Nes.Cart.Rom.ElementAt(CurrentState.PC);
            CurrentState.PC++;

            CurrentState.CycleTicksRemain = FUNCTION_LOOKUP[instruction]();

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

        // Handle opcodes

        /// <summary>Function lookup table</summary>
        private static Func<byte>[] FUNCTION_LOOKUP = new Func<byte>[0xFF];

        private void CacheOpcodes()
        {
            // Get all opcode methods
            var opcodeMethods = typeof(NesCpu).GetMethods(BindingFlags.NonPublic).Where(w => w.GetCustomAttributes(typeof(OpcodeAttribute), false).Length > 0).Distinct();
            foreach (var method in opcodeMethods)
            {
                var opcodeAttrs = method.GetCustomAttributes(typeof(OpcodeAttribute), false).Cast<OpcodeAttribute>();
                foreach (var opcodeAttr in opcodeAttrs)
                {
                    var opcode = opcodeAttr.Opcode;
                    var mode = opcodeAttr.Mode;
                    var cost = opcodeAttr.Cost;

                    Operation del = (Operation)Delegate.CreateDelegate(typeof(Operation), this, method);
                    Func<byte> call = () =>
                    {
                        del.Invoke(mode);
                        return cost;
                    };

                    //var entry = new OperationEntry(call);
                    FUNCTION_LOOKUP[opcode] = call;
                }
            }
        }

        // Opcode definitions
        // https://problemkaputt.de/everynes.htm

        [Opcode(0xA8, 2)]
        private void Operation_TAY(AddressingMode mode)
        {
            CurrentState.Y = CurrentState.A;
        }

        [Opcode(0xAA, 2)]
        private void Operation_TAX(AddressingMode mode)
        {
            CurrentState.X = CurrentState.A;
        }

        [Opcode(0xBA, 2)]
        private void Operation_TSX(AddressingMode mode)
        {
            CurrentState.X = CurrentState.S;
        }

        [Opcode(0x98, 2)]
        private void Operation_TYA(AddressingMode mode)
        {
            CurrentState.A = CurrentState.Y;
        }

        [Opcode(0x8A, 2)]
        private void Operation_TXA(AddressingMode mode)
        {
            CurrentState.A = CurrentState.X;
        }

        [Opcode(0x9A, 2)]
        private void Operation_TXS(AddressingMode mode)
        {
            CurrentState.S = CurrentState.X;
        }

        [Opcode(0xA9, 2, AddressingMode.ZeroPage)]
        private void Operation_LDA(AddressingMode mode)
        {
            var operand = FetchOperandByte(mode);
            CurrentState.A = operand;
        }

        [Opcode(0xA2, 2, AddressingMode.ZeroPage)]
        private void Operation_LDX(AddressingMode mode)
        {
            var operand = FetchOperandByte(mode);
            CurrentState.X = operand;
        }

        [Opcode(0x0A, 2, AddressingMode.ZeroPage)]
        private void Operation_LDY(AddressingMode mode)
        {
            var operand = FetchOperandByte(mode);
            CurrentState.Y = operand;
        }

        [Opcode(0x01, 6, AddressingMode.IndirectX)]
        private void Operation_ORA(AddressingMode mode)
        {
            var operand = FetchOperandByte(mode);

            operand |= CurrentState.A;
            CurrentState.SetStatusFlag(StatusFlags.Negative, (operand > 0x7F));
            CurrentState.SetStatusFlag(StatusFlags.Zero, (operand == 0x0));
            CurrentState.A = operand;
        }
    }

    delegate void Operation(AddressingMode mode);

    /*struct OperationEntry
    {
        public OperationEntry(byte opcode, AddressingMode mode, byte cost, Func<byte> call)
        {
            this.opcode = opcode;
            this.mode = mode;
            this.cost = cost;
            this.call = call;
        }

        public byte opcode;
        public AddressingMode mode;
        public byte cost;
        /// <summary>Function that performs the operation and returns the cost</summary>
        public Func<byte> call;
    }*/

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    class OpcodeAttribute : Attribute
    {
        public OpcodeAttribute(byte opcode, byte cost, AddressingMode mode = AddressingMode.Unsupported)
        {
            this.Opcode = opcode;
            this.Mode = mode;
            this.Cost = cost;
        }

        public byte Opcode { get; private set; }
        public AddressingMode Mode { get; private set; }
        public byte Cost { get; private set; }
    }

    class NesCpuState : IResettable
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

    enum AddressingMode : int
    {
        Unsupported = 0,
        /// <summary>XXX #nn</summary>
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
}
