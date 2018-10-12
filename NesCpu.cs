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

            CurrentState.Reset();

            CacheOpcodes();
        }

        protected Nes Nes;

        private NesCpuState CurrentState = new NesCpuState();

        private bool mayIncurBoundryExpense;
        private byte currentCycleCost;

        public void Update()
        {
            if (CurrentState.CycleTicksRemain > 0)
            {
                CurrentState.CycleTicksRemain--;
                return;
            }

            var instruction = Nes.Cart.Rom[CurrentState.PC];
            CurrentState.PC++;

            // Calling this sets mayIncurBoundryExpense, currentCycleCost, and runs the function
            FUNCTION_LOOKUP[instruction]();

            // Set this here because currentCycleCost may have changed during execution
            CurrentState.CycleTicksRemain = currentCycleCost;

            // This was a tick 
            CurrentState.CycleTicksRemain--;
        }

        public void Reset()
        {
            CurrentState.Reset();
            var jumpVector = Nes.Ram.ReadUShort(0xFFFC);
            CurrentState.PC = jumpVector;
        }

        private ushort FetchOperandAddress(AddressingMode mode)
        {
            ushort addr;

            switch (mode)
            {
                case AddressingMode.Immediate:
                    addr = CurrentState.PC;
                    CurrentState.PC++;
                    break;
                case AddressingMode.ZeroPage:
                case AddressingMode.ZeroPageX:
                case AddressingMode.ZeroPageY:
                    int zo = Nes.Ram.ReadByte(CurrentState.PC);
                    CurrentState.PC++;

                    if (mode == AddressingMode.ZeroPageX)
                    {
                        zo = zo + CurrentState.X;
                    }
                    else if (mode == AddressingMode.ZeroPageY)
                    {
                        zo = zo + CurrentState.Y;
                    }
                    addr = (byte)(((byte)zo) & 0xff); // Zero page ???
                    break;
                case AddressingMode.Absolute:
                case AddressingMode.AbsoluteX:
                case AddressingMode.AbsoluteY:
                    addr = Nes.Ram.ReadUShort(CurrentState.PC);
                    CurrentState.PC++;

                    if (mode == AddressingMode.AbsoluteX)
                    {
                        addr += CurrentState.X;
                    }
                    else if (mode == AddressingMode.AbsoluteY)
                    {
                        addr += CurrentState.X;
                    }
                    break;
                case AddressingMode.IndirectX:
                    addr = AddBytes(Nes.Ram.ReadByte(CurrentState.PC), CurrentState.X);
                    addr = (byte)(addr & 0xff); // Zero page ?
                    CurrentState.PC++;
                    break;
                case AddressingMode.IndirectY:
                    var iyop = Nes.Ram.ReadByte(CurrentState.PC);
                    CurrentState.PC++;

                    var ixy = Nes.Ram.ReadByte(iyop);
                    ixy = (byte)(ixy & 0xff); // Zero page (is this right?)
                    addr = AddBytes(ixy, CurrentState.Y);
                    break;
                default:
                    throw new Exception("Unsupported AddressingMode");
            }

            if (mayIncurBoundryExpense && addr >= 0x0100)
            {
                // TODO: Figure out if this is how this actually works
                currentCycleCost++;
            }

            return addr;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private byte FetchOperandByte(AddressingMode mode)
        {
            var address = FetchOperandAddress(mode);
            return Nes.Ram.ReadByte(address);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ushort AddBytes(byte a, byte b)
        {
            return (ushort)((a << 8) + b);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void UpdateStandardFlags(byte operand)
        {
            // Negative flag is checking a signed byte is < 0. This check is equatable to converting to an sbyte and testing directly
            CurrentState.SetStatusFlag(StatusFlags.Negative, (operand > 0x7F));
            CurrentState.SetStatusFlag(StatusFlags.Zero, (operand == 0x0));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void StackPush(byte value)
        {
            // Is this stack addressing right??
            // TODO: How to handle stack overflow/underflow
            var address = (ushort)(0x0100 + CurrentState.S);
            CurrentState.S++;
            Nes.Ram.Write(address, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private byte StackPull()
        {
            // Is this stack addressing right??
            // TODO: How to handle stack overflow/underflow
            var address = (ushort)(0x0100 + CurrentState.S);
            CurrentState.S--;
            return Nes.Ram.ReadByte(address);
        }

        // Handle opcodes

        /// <summary>Function lookup table</summary>
        private static Action[] FUNCTION_LOOKUP = new Action[0xFF];

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
                    var pageBoundryCostsCycle = opcodeAttr.PageBoundryCostsCycle;

                    Operation del = (Operation)Delegate.CreateDelegate(typeof(Operation), this, method);
                    Action call = () =>
                    {
                        this.mayIncurBoundryExpense = pageBoundryCostsCycle;
                        this.currentCycleCost = cost;
                        del.Invoke(mode);
                    };

                    //var entry = new OperationEntry(call);
                    FUNCTION_LOOKUP[opcode] = call;
                }
            }
        }

        // Opcode definitions
        // https://problemkaputt.de/everynes.htm
        // http://www.thealmightyguru.com/Games/Hacking/Wiki/index.php?title=6502_Opcodes
        // http://www.obelisk.me.uk/6502/reference.html

        #region Memory to register

        [Opcode(0xA8, 2)]
        private void Operation_TAY(AddressingMode mode)
        {
            UpdateStandardFlags(CurrentState.A);
            CurrentState.Y = CurrentState.A;
        }

        [Opcode(0xAA, 2)]
        private void Operation_TAX(AddressingMode mode)
        {
            UpdateStandardFlags(CurrentState.A);
            CurrentState.X = CurrentState.A;
        }

        [Opcode(0xBA, 2)]
        private void Operation_TSX(AddressingMode mode)
        {
            UpdateStandardFlags(CurrentState.S);
            CurrentState.X = CurrentState.S;
        }

        [Opcode(0x98, 2)]
        private void Operation_TYA(AddressingMode mode)
        {
            UpdateStandardFlags(CurrentState.Y);
            CurrentState.A = CurrentState.Y;
        }

        [Opcode(0x8A, 2)]
        private void Operation_TXA(AddressingMode mode)
        {
            UpdateStandardFlags(CurrentState.X);
            CurrentState.A = CurrentState.X;
        }

        [Opcode(0x9A, 2)]
        private void Operation_TXS(AddressingMode mode)
        {
            UpdateStandardFlags(CurrentState.X);
            CurrentState.S = CurrentState.X;
        }

        [Opcode(0xA9, 2, AddressingMode.Immediate)]
        [Opcode(0xA5, 3, AddressingMode.ZeroPage)]
        [Opcode(0xB5, 4, AddressingMode.ZeroPageX)]
        [Opcode(0xAD, 4, AddressingMode.Absolute)]
        [Opcode(0xBD, 4, AddressingMode.AbsoluteX, pageBoundryCostsCycle: true)]
        [Opcode(0xB9, 4, AddressingMode.AbsoluteY, pageBoundryCostsCycle: true)]
        [Opcode(0xA1, 6, AddressingMode.IndirectX)]
        [Opcode(0xB1, 5, AddressingMode.IndirectY, pageBoundryCostsCycle: true)]
        private void Operation_LDA(AddressingMode mode)
        {
            var operand = FetchOperandByte(mode);

            UpdateStandardFlags(operand);
            CurrentState.A = operand;
        }

        [Opcode(0xA2, 2, AddressingMode.Immediate)]
        [Opcode(0xA1, 3, AddressingMode.ZeroPage)]
        [Opcode(0xB1, 4, AddressingMode.ZeroPageY)]
        [Opcode(0xA6, 4, AddressingMode.Absolute)]
        [Opcode(0xBE, 4, AddressingMode.AbsoluteY, pageBoundryCostsCycle: true)]
        private void Operation_LDX(AddressingMode mode)
        {
            var operand = FetchOperandByte(mode);

            UpdateStandardFlags(operand);
            CurrentState.X = operand;
        }

        [Opcode(0x0A, 2, AddressingMode.Immediate)]
        [Opcode(0xA4, 3, AddressingMode.ZeroPage)]
        [Opcode(0xB4, 4, AddressingMode.ZeroPageX)]
        [Opcode(0xAC, 4, AddressingMode.Absolute)]
        [Opcode(0xBC, 4, AddressingMode.AbsoluteX, pageBoundryCostsCycle: true)]
        private void Operation_LDY(AddressingMode mode)
        {
            var operand = FetchOperandByte(mode);

            UpdateStandardFlags(operand);
            CurrentState.Y = operand;
        }

        #endregion

        #region Register to memory

        [Opcode(0x85, 3, AddressingMode.ZeroPage)]
        [Opcode(0x95, 4, AddressingMode.ZeroPageX)]
        [Opcode(0x8D, 4, AddressingMode.Absolute)]
        [Opcode(0x9D, 5, AddressingMode.AbsoluteX)]
        [Opcode(0x99, 5, AddressingMode.AbsoluteY)]
        [Opcode(0x81, 6, AddressingMode.IndirectX)]
        [Opcode(0x91, 6, AddressingMode.IndirectY)]
        private void Operation_STA(AddressingMode mode)
        {
            var address = FetchOperandAddress(mode);
            Nes.Ram.Write(address, CurrentState.A);
        }

        [Opcode(0x86, 3, AddressingMode.ZeroPage)]
        [Opcode(0x96, 4, AddressingMode.ZeroPageY)]
        [Opcode(0x8E, 4, AddressingMode.Absolute)]
        private void Operation_STX(AddressingMode mode)
        {
            var address = FetchOperandAddress(mode);
            Nes.Ram.Write(address, CurrentState.X);
        }

        [Opcode(0x84, 3, AddressingMode.ZeroPage)]
        [Opcode(0x94, 4, AddressingMode.ZeroPageX)]
        [Opcode(0x8C, 4, AddressingMode.Absolute)]
        private void Operation_STY(AddressingMode mode)
        {
            var address = FetchOperandAddress(mode);
            Nes.Ram.Write(address, CurrentState.Y);
        }

        #endregion

        #region Stack

        [Opcode(0x48, 3)]
        private void Operation_PHA(AddressingMode mode)
        {
            StackPush(CurrentState.A);
        }

        [Opcode(0x08, 3)]
        private void Operation_PHP(AddressingMode mode)
        {
            StackPush(CurrentState.P);
        }

        [Opcode(0x68, 4)]
        private void Operation_PLA(AddressingMode mode)
        {
            var operand = StackPull();
            UpdateStandardFlags(operand);
            CurrentState.A = operand;
        }

        [Opcode(0x28, 4)]
        private void Operation_PLP(AddressingMode mode)
        {
            CurrentState.P = StackPull();
        }

        #endregion

        #region Memory to accumulator

        [Opcode(0x69, 2, AddressingMode.Immediate)]
        [Opcode(0x65, 3, AddressingMode.ZeroPage)]
        [Opcode(0x75, 4, AddressingMode.ZeroPageX)]
        [Opcode(0x6D, 4, AddressingMode.Absolute)]
        [Opcode(0x7D, 4, AddressingMode.AbsoluteX, pageBoundryCostsCycle: true)]
        [Opcode(0x79, 4, AddressingMode.AbsoluteY, pageBoundryCostsCycle: true)]
        [Opcode(0x61, 6, AddressingMode.IndirectX)]
        [Opcode(0x71, 5, AddressingMode.IndirectY, pageBoundryCostsCycle: true)]
        private void Operation_ADC(AddressingMode mode)
        {
            var operand = FetchOperandByte(mode);

            byte carry = (byte)(CurrentState.GetStatusFlag(StatusFlags.Carry) ? 1 : 0);
            ushort value = (ushort)(CurrentState.A + carry + operand);

            // from http://rubbermallet.org/fake6502.c
            CurrentState.SetStatusFlag(StatusFlags.Negative, (value & 0x0080) > 0);
            CurrentState.SetStatusFlag(StatusFlags.Zero, (value & 0x00FF) == 0);
            CurrentState.SetStatusFlag(StatusFlags.Carry, (value & 0xFF00) > 0);
            CurrentState.SetStatusFlag(StatusFlags.Overflow, ((value ^ CurrentState.A) & (value ^ operand) & 0x0080) > 0);

            CurrentState.A = (byte)(value & 0x00FF);
        }

        #endregion

        #region Bitwise with accumulator

        [Opcode(0x09, 2, AddressingMode.Immediate)]
        [Opcode(0x05, 3, AddressingMode.ZeroPage)]
        [Opcode(0x15, 4, AddressingMode.ZeroPageX)]
        [Opcode(0x0D, 4, AddressingMode.Absolute)]
        [Opcode(0x1D, 4, AddressingMode.AbsoluteX, pageBoundryCostsCycle: true)]
        [Opcode(0x19, 4, AddressingMode.AbsoluteY, pageBoundryCostsCycle: true)]
        [Opcode(0x01, 6, AddressingMode.IndirectX)]
        [Opcode(0x11, 5, AddressingMode.IndirectY, pageBoundryCostsCycle: true)]
        private void Operation_ORA(AddressingMode mode)
        {
            var operand = FetchOperandByte(mode);

            operand |= CurrentState.A;
            UpdateStandardFlags(operand);
            CurrentState.A = operand;
        }

        #endregion
    }

    delegate void Operation(AddressingMode mode);

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    class OpcodeAttribute : Attribute
    {
        public OpcodeAttribute(byte opcode, byte cost, AddressingMode mode = AddressingMode.Unsupported, bool pageBoundryCostsCycle = false)
        {
            this.Opcode = opcode;
            this.Mode = mode;
            this.Cost = cost;
            this.PageBoundryCostsCycle = pageBoundryCostsCycle;
        }

        public byte Opcode { get; private set; }
        public AddressingMode Mode { get; private set; }
        public byte Cost { get; private set; }
        public bool PageBoundryCostsCycle { get; private set; }
    }

    struct NesCpuState : IResettable
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
                byte[] b = new byte[1];
                // TODO: Make sure the byte doesn't get exported backwards
                StatusRegister.CopyTo(b, 0);
                return b[0];
            }
            set
            {
                StatusRegister = new BitArray(new byte[] { value });
            }
        }

        private BitArray StatusRegister;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool GetStatusFlag(StatusFlags flag)
        {
            return StatusRegister.Get((int)flag);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
        ZeroPageY,
        Absolute,
        AbsoluteX,
        AbsoluteY,
        IndirectX,
        IndirectY
    }

    enum StatusFlags : int
    {
        /// <summary>C flag - 0=no carry, 1=carry</summary>
        Carry = 0x0,
        /// <summary>Z flagm 0=nonzero, 1=zero</summary>
        Zero = 0x1,
        /// <summary>I flag, 0=IRQ enable, 1=IRQ disable</summary>
        Interrupt = 0x2,
        /// <summary>Unused. D flag, 0=normal, 1=bcd mode</summary>
        Decimal = 0x3,
        /// <summary>B flag, 0=IRQ/NMI, 1=reset or BRK/PHP</summary>
        Break = 0x4,
        /// <summary>Unused. (always 1)</summary>
        Always1 = 0x5,
        /// <summary>V flag (0=no overflow, 1=overflow)</summary>
        Overflow = 0x6,
        /// <summary>N flag (0=positive, 1=negative)</summary>
        Negative = 0x7,
    }
}
