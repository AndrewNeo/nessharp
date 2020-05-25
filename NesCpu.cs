using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace NesSharp
{
    public class NesCpu : IResettable
    {
        private static IEnumerable<MethodInfo> AllOpcodeMethods;
        public static Dictionary<byte, string> AllOpcodeNames { get; private set; }

        static NesCpu()
        {
            AllOpcodeMethods = typeof(NesCpu)
                .GetMethods(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(w => w.GetCustomAttributes(typeof(OpcodeAttribute), false).Length > 0)
                .Distinct();

            AllOpcodeNames = AllOpcodeMethods
                .SelectMany(method => method.GetCustomAttributes(typeof(OpcodeAttribute), false).Cast<OpcodeAttribute>())
                .ToDictionary(attr => attr.Opcode, attr => attr.Name);
        }

        protected Nes Nes;

        private NesCpuState CurrentState;

        private byte currentCycleCost;

        public NesCpu(Nes nes)
        {
            this.Nes = nes;

            CacheOpcodes();

            CurrentState = new NesCpuState();
        }

        public bool ActiveNmi { get; set; }
        public bool ActiveIrq { get; set; }

        public bool Update(bool stopOnInvalidOpcode = false)
        {
            if (CurrentState.CycleTicksRemain > 0)
            {
                Nes.Debugger.Log(NesDebugger.TAG_CPU, "Sleeping for {0} cycles", CurrentState.CycleTicksRemain);
                CurrentState.CycleTicksRemain--;
                return true;
            }

            if (ActiveNmi)
            {
                Interrupt(InterruptMode.NMI);
                CurrentState.CycleTicksRemain = currentCycleCost;
                return true;
            }
            else if (ActiveIrq && !CurrentState.GetStatusFlag(StatusFlags.Interrupt))
            {
                Interrupt(InterruptMode.IRQ);
                CurrentState.CycleTicksRemain = currentCycleCost;
                return true;
            }

            var instruction = Nes.CpuMem.ReadByte(CurrentState.PC);
            Nes.Debugger.ExecOpCode(instruction);
            CurrentState.PC++;

            // Calling this sets currentCycleCost and runs the function
            if (instruction == 0xFF)
            {
                throw new Exception("Caught invalid instruction");
            }

            var methodCall = FUNCTION_LOOKUP[instruction];

            if (methodCall != null)
            {
                methodCall();
            }
            else
            {
                Nes.Debugger.Log(NesDebugger.TAG_CPU, "Unsupported opcode 0x{0:x}", instruction);

                if (stopOnInvalidOpcode)
                {
                    return false;
                }
                else
                {
                    FUNCTION_LOOKUP[0xEA]();
                }
            }

            // Set this here because currentCycleCost may have changed during execution
            CurrentState.CycleTicksRemain = currentCycleCost;

            // This was a tick 
            CurrentState.CycleTicksRemain--;

            return true;
        }

        #region Resetters

        public void SoftReset()
        {
            Nes.Debugger.Log(NesDebugger.TAG_CPU, "Soft-resetting CPU");
            CurrentState.SoftReset();
            Nes.CpuMem.SoftReset();
            Reset();
        }

        public void HardReset()
        {
            Nes.Debugger.Log(NesDebugger.TAG_CPU, "Hard-resetting CPU");
            CurrentState.HardReset();
            Nes.CpuMem.HardReset();
            Reset();
        }

        private void Reset()
        {
            ActiveNmi = false;
            ActiveIrq = false;
            JumpWithVectorTable(FixedJumpVector.Reset);
        }

        private void JumpWithVectorTable(FixedJumpVector fjv)
        {
            var jumpVector = Nes.CpuMem.ReadUShort((ushort)fjv);
            CurrentState.PC = jumpVector;

            Nes.Debugger.Log(NesDebugger.TAG_CPU, "Set jump vector to {0:x}", jumpVector);
        }

        #endregion

        #region Internal helpers

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ushort FetchOperandAddress(AddressingMode mode, bool isWrite = false)
        {
            ushort addr;
            ushort temp, temp2;

            switch (mode)
            {
                case AddressingMode.Immediate:
                case AddressingMode.Relative:
                    addr = CurrentState.PC;
                    CurrentState.PC++;
                    break;
                case AddressingMode.ZeroPage:
                case AddressingMode.ZeroPageX:
                case AddressingMode.ZeroPageY:
                    temp = ReadByteAtPC();

                    if (mode == AddressingMode.ZeroPageX)
                    {
                        addr = (ushort)((temp + CurrentState.X) & 0x00FF); // Zero page wraparound
                    }
                    else if (mode == AddressingMode.ZeroPageY)
                    {
                        addr = (ushort)((temp + CurrentState.Y) & 0x00FF); // Zero page wraparound
                    }
                    else
                    {
                        addr = temp;
                    }
                    break;
                case AddressingMode.Absolute:
                case AddressingMode.AbsoluteX:
                case AddressingMode.AbsoluteY:
                    addr = ReadUshortAtPC();

                    if (mode == AddressingMode.AbsoluteX)
                    {
                        addr += CurrentState.X;
                        if (isWrite || IsCrossingPageBoundry(addr, CurrentState.X))
                        {
                            currentCycleCost++;
                        }
                    }
                    else if (mode == AddressingMode.AbsoluteY)
                    {
                        addr = CurrentState.Y;
                        if (isWrite || IsCrossingPageBoundry(addr, CurrentState.Y))
                        {
                            currentCycleCost++;
                        }
                    }
                    break;
                case AddressingMode.Indirect:
                    temp = ReadUshortAtPC();

                    // Zero page wraparound
                    var ixi_a = Nes.CpuMem.ReadByte((ushort)(temp & 0x00FF));
                    var ixi_b = Nes.CpuMem.ReadByte((ushort)((temp + 1) & 0x00FF));

                    addr = (ushort)(ixi_a | (ixi_b << 8));
                    break;
                case AddressingMode.IndirectX:
                    temp = ReadByteAtPC();

                    // Zero page wraparound
                    temp2 = (ushort)((temp + CurrentState.X) & 0x00FF);

                    var ixx_a = Nes.CpuMem.ReadByte((ushort)(temp2 & 0x00FF));
                    var ixx_b = Nes.CpuMem.ReadByte((ushort)((temp2 + 1) & 0x00FF));

                    addr = (ushort)(ixx_a | (ixx_b << 8));
                    break;
                case AddressingMode.IndirectY:
                    temp = ReadByteAtPC();

                    // Zero page wraparound
                    temp2 = (ushort)((temp & 0xFF00) | ((temp + 1) & 0x00FF));

                    var ixy_a = Nes.CpuMem.ReadByte(temp);
                    var ixy_b = Nes.CpuMem.ReadByte(temp2);
                    var ixy_r = (ushort)(ixy_a | (ixy_b << 8));

                    addr = (ushort)(ixy_r + CurrentState.Y);

                    if (isWrite || IsCrossingPageBoundry(ixy_r, CurrentState.Y))
                    {
                        currentCycleCost++;
                    }
                    break;
                default:
                    throw new Exception("Unsupported AddressingMode");
            }

            return addr;
        }

        private byte FetchOperandByte(AddressingMode mode, bool isWrite = false)
        {
            var address = FetchOperandAddress(mode, isWrite);
            return Nes.CpuMem.ReadByte(address);
        }

        private bool IsCrossingPageBoundry(ushort address, ushort i)
        {
            return ((address + i) & 0xFF00) != ((address & 0xFF00));
        }

        private byte ReadByteAtPC()
        {
            var value = Nes.CpuMem.ReadByte(CurrentState.PC);
            CurrentState.PC++;
            return value;
        }

        private ushort ReadUshortAtPC()
        {
            var value = Nes.CpuMem.ReadUShort(CurrentState.PC);
            CurrentState.PC += 2;
            return value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void UpdateStandardFlags(byte operand)
        {
            // Negative flag is checking a signed byte is < 0. This check is equatable to converting to an sbyte and testing directly
            CurrentState.SetStatusFlag(StatusFlags.Negative, (operand > 0x7F));
            CurrentState.SetStatusFlag(StatusFlags.Zero, (operand == 0x0));
        }

        private void StackPush(byte value)
        {
            var address = (ushort)(NesConsts.MEM_STACK_START + CurrentState.S--);
            Nes.CpuMem.Write(address, value);
        }

        private void StackPush(ushort value)
        {
            StackPush((byte)(value & 0xFF));
            StackPush((byte)((value >> 8) & 0xFF));
        }

        private byte StackPull()
        {
            var address = (ushort)(NesConsts.MEM_STACK_START + ++CurrentState.S);
            return Nes.CpuMem.ReadByte(address);
        }

        private ushort StackPull16()
        {
            var lowByte = StackPull();
            var highByte = StackPull();
            var value = (ushort)(lowByte | highByte << 8);
            return value;
        }

        #endregion

        // Handle opcodes

        #region Opcode registration system

        /// <summary>Function lookup table</summary>
        private Action[] FUNCTION_LOOKUP = new Action[0xFF];

        private void CacheOpcodes()
        {
            // Get all opcode methods
            foreach (var method in AllOpcodeMethods)
            {
                var opcodeAttrs = method.GetCustomAttributes(typeof(OpcodeAttribute), false).Cast<OpcodeAttribute>();
                foreach (var opcodeAttr in opcodeAttrs)
                {
                    var opcode = opcodeAttr.Opcode;
                    var mode = opcodeAttr.Mode;
                    var cost = opcodeAttr.Cost;

                    Operation del = (Operation)Delegate.CreateDelegate(typeof(Operation), this, method);
                    Action call = () =>
                    {
                        this.currentCycleCost = cost;
                        del.Invoke(mode);
                    };

                    //var entry = new OperationEntry(call);
                    FUNCTION_LOOKUP[opcode] = call;
                }
            }
        }

        #endregion

        // Opcode definitions
        // https://problemkaputt.de/everynes.htm
        // http://www.thealmightyguru.com/Games/Hacking/Wiki/index.php?title=6502_Opcodes
        // http://www.obelisk.me.uk/6502/reference.html
        // https://wiki.nesdev.com/w/index.php/CPU_unofficial_opcodes
        // http://rubbermallet.org/fake6502.c
        // https://github.com/AndreaOrru/LaiNES/blob/master/src/cpu.cpp

        #region Memory to register

        [Opcode(0xA8, "TAY", 2)]
        private void Operation_TAY(AddressingMode mode)
        {
            UpdateStandardFlags(CurrentState.A);
            CurrentState.Y = CurrentState.A;
        }

        [Opcode(0xAA, "TAX", 2)]
        private void Operation_TAX(AddressingMode mode)
        {
            UpdateStandardFlags(CurrentState.A);
            CurrentState.X = CurrentState.A;
        }

        [Opcode(0xBA, "TSX", 2)]
        private void Operation_TSX(AddressingMode mode)
        {
            UpdateStandardFlags(CurrentState.S);
            CurrentState.X = CurrentState.S;
        }

        [Opcode(0x98, "TYA", 2)]
        private void Operation_TYA(AddressingMode mode)
        {
            UpdateStandardFlags(CurrentState.Y);
            CurrentState.A = CurrentState.Y;
        }

        [Opcode(0x8A, "TXA", 2)]
        private void Operation_TXA(AddressingMode mode)
        {
            UpdateStandardFlags(CurrentState.X);
            CurrentState.A = CurrentState.X;
        }

        [Opcode(0x9A, "TXS", 2)]
        private void Operation_TXS(AddressingMode mode)
        {
            UpdateStandardFlags(CurrentState.X);
            CurrentState.S = CurrentState.X;
        }

        [Opcode(0xA9, "LDA", 2, AddressingMode.Immediate)]
        [Opcode(0xA5, "LDA", 3, AddressingMode.ZeroPage)]
        [Opcode(0xB5, "LDA", 4, AddressingMode.ZeroPageX)]
        [Opcode(0xAD, "LDA", 4, AddressingMode.Absolute)]
        [Opcode(0xBD, "LDA", 4, AddressingMode.AbsoluteX)]
        [Opcode(0xB9, "LDA", 4, AddressingMode.AbsoluteY)]
        [Opcode(0xA1, "LDA", 6, AddressingMode.IndirectX)]
        [Opcode(0xB1, "LDA", 5, AddressingMode.IndirectY)]
        private void Operation_LDA(AddressingMode mode)
        {
            var operand = FetchOperandByte(mode);

            UpdateStandardFlags(operand);
            CurrentState.A = operand;
        }

        [Opcode(0xA2, "LDX", 2, AddressingMode.Immediate)]
        [Opcode(0xAE, "LDX", 3, AddressingMode.ZeroPage)]
        [Opcode(0xB6, "LDX", 4, AddressingMode.ZeroPageY)]
        [Opcode(0xA6, "LDX", 4, AddressingMode.Absolute)]
        [Opcode(0xBE, "LDX", 4, AddressingMode.AbsoluteY)]
        private void Operation_LDX(AddressingMode mode)
        {
            var operand = FetchOperandByte(mode);

            UpdateStandardFlags(operand);
            CurrentState.X = operand;
        }

        [Opcode(0x0A, "LDY", 2, AddressingMode.Immediate)]
        [Opcode(0xA4, "LDY", 3, AddressingMode.ZeroPage)]
        [Opcode(0xB4, "LDY", 4, AddressingMode.ZeroPageX)]
        [Opcode(0xAC, "LDY", 4, AddressingMode.Absolute)]
        [Opcode(0xBC, "LDY", 4, AddressingMode.AbsoluteX)]
        private void Operation_LDY(AddressingMode mode)
        {
            var operand = FetchOperandByte(mode);

            UpdateStandardFlags(operand);
            CurrentState.Y = operand;
        }

        #endregion

        #region Register to memory

        [Opcode(0x85, "STA", 3, AddressingMode.ZeroPage)]
        [Opcode(0x95, "STA", 4, AddressingMode.ZeroPageX)]
        [Opcode(0x8D, "STA", 4, AddressingMode.Absolute)]
        [Opcode(0x9D, "STA", 5, AddressingMode.AbsoluteX)]
        [Opcode(0x99, "STA", 5, AddressingMode.AbsoluteY)]
        [Opcode(0x81, "STA", 6, AddressingMode.IndirectX)]
        [Opcode(0x91, "STA", 6, AddressingMode.IndirectY)]
        private void Operation_STA(AddressingMode mode)
        {
            var address = FetchOperandAddress(mode, true);
            Nes.CpuMem.Write(address, CurrentState.A);
        }

        [Opcode(0x86, "STX", 3, AddressingMode.ZeroPage)]
        [Opcode(0x96, "STX", 4, AddressingMode.ZeroPageY)]
        [Opcode(0x8E, "STX", 4, AddressingMode.Absolute)]
        private void Operation_STX(AddressingMode mode)
        {
            var address = FetchOperandAddress(mode, true);
            Nes.CpuMem.Write(address, CurrentState.X);
        }

        [Opcode(0x84, "STY", 3, AddressingMode.ZeroPage)]
        [Opcode(0x94, "STY", 4, AddressingMode.ZeroPageX)]
        [Opcode(0x8C, "STY", 4, AddressingMode.Absolute)]
        private void Operation_STY(AddressingMode mode)
        {
            var address = FetchOperandAddress(mode, true);
            Nes.CpuMem.Write(address, CurrentState.Y);
        }

        #endregion

        #region Stack

        [Opcode(0x48, "PHA", 3)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Operation_PHA(AddressingMode mode)
        {
            StackPush(CurrentState.A);
        }

        [Opcode(0x08, "PHP", 3)]
        private void Operation_PHP(AddressingMode mode)
        {
            StackPush(CurrentState.P);
        }

        [Opcode(0x68, "PLA", 4)]
        private void Operation_PLA(AddressingMode mode)
        {
            var operand = StackPull();
            UpdateStandardFlags(operand);
            CurrentState.A = operand;
        }

        [Opcode(0x28, "PLP", 4)]
        private void Operation_PLP(AddressingMode mode)
        {
            CurrentState.P = StackPull();
        }

        #endregion

        #region Memory to accumulator

        [Opcode(0x69, "ADC", 2, AddressingMode.Immediate)]
        [Opcode(0x65, "ADC", 3, AddressingMode.ZeroPage)]
        [Opcode(0x75, "ADC", 4, AddressingMode.ZeroPageX)]
        [Opcode(0x6D, "ADC", 4, AddressingMode.Absolute)]
        [Opcode(0x7D, "ADC", 4, AddressingMode.AbsoluteX)]
        [Opcode(0x79, "ADC", 4, AddressingMode.AbsoluteY)]
        [Opcode(0x61, "ADC", 6, AddressingMode.IndirectX)]
        [Opcode(0x71, "ADC", 5, AddressingMode.IndirectY)]
        private void Operation_ADC(AddressingMode mode)
        {
            var operand = FetchOperandByte(mode);

            byte carry = (byte)(CurrentState.GetStatusFlag(StatusFlags.Carry) ? 1 : 0);
            ushort value = (ushort)(CurrentState.A + carry + operand);

            CurrentState.SetStatusFlag(StatusFlags.Negative, (value & 0x0080) > 0);
            CurrentState.SetStatusFlag(StatusFlags.Zero, (value & 0x00FF) == 0);
            CurrentState.SetStatusFlag(StatusFlags.Carry, (value & 0xFF00) > 0);
            CurrentState.SetStatusFlag(StatusFlags.Overflow, ((value ^ CurrentState.A) & (value ^ operand) & 0x0080) > 0);

            CurrentState.A = (byte)(value & 0x00FF);
        }

        [Opcode(0xE9, "SBC", 2, AddressingMode.Immediate)]
        [Opcode(0xE5, "SBC", 3, AddressingMode.ZeroPage)]
        [Opcode(0xF5, "SBC", 4, AddressingMode.ZeroPageX)]
        [Opcode(0xED, "SBC", 4, AddressingMode.Absolute)]
        [Opcode(0xFD, "SBC", 4, AddressingMode.AbsoluteX)]
        [Opcode(0xF9, "SBC", 4, AddressingMode.AbsoluteY)]
        [Opcode(0xE1, "SBC", 6, AddressingMode.IndirectX)]
        [Opcode(0xF1, "SBC", 5, AddressingMode.IndirectY)]
        private void Operation_SBC(AddressingMode mode)
        {
            var operand = FetchOperandByte(mode);

            byte carry = (byte)(CurrentState.GetStatusFlag(StatusFlags.Carry) ? 1 : 0);
            ushort value = (ushort)(CurrentState.A - operand - (1 - carry));

            CurrentState.SetStatusFlag(StatusFlags.Negative, (value & 0x0080) > 0);
            CurrentState.SetStatusFlag(StatusFlags.Zero, (value & 0x00FF) == 0);
            CurrentState.SetStatusFlag(StatusFlags.Carry, (value & 0xFF00) == 0);
            CurrentState.SetStatusFlag(StatusFlags.Overflow, ((value ^ CurrentState.A) & (value ^ operand) & 0x0080) > 0);

            CurrentState.A = (byte)(value & 0x00FF);
        }

        #endregion

        #region Bitwise with accumulator

        [Opcode(0x29, "AND", 2, AddressingMode.Immediate)]
        [Opcode(0x25, "AND", 3, AddressingMode.ZeroPage)]
        [Opcode(0x35, "AND", 4, AddressingMode.ZeroPageX)]
        [Opcode(0x2D, "AND", 4, AddressingMode.Absolute)]
        [Opcode(0x3D, "AND", 4, AddressingMode.AbsoluteX)]
        [Opcode(0x39, "AND", 4, AddressingMode.AbsoluteY)]
        [Opcode(0x21, "AND", 6, AddressingMode.IndirectX)]
        [Opcode(0x31, "AND", 5, AddressingMode.IndirectY)]
        private void Operation_AND(AddressingMode mode)
        {
            var operand = FetchOperandByte(mode);

            operand &= CurrentState.A;
            UpdateStandardFlags(operand);
            CurrentState.A = operand;
        }

        [Opcode(0x49, "EOR", 2, AddressingMode.Immediate)]
        [Opcode(0x45, "EOR", 3, AddressingMode.ZeroPage)]
        [Opcode(0x55, "EOR", 4, AddressingMode.ZeroPageX)]
        [Opcode(0x4D, "EOR", 4, AddressingMode.Absolute)]
        [Opcode(0x5D, "EOR", 4, AddressingMode.AbsoluteX)]
        [Opcode(0x59, "EOR", 4, AddressingMode.AbsoluteY)]
        [Opcode(0x41, "EOR", 6, AddressingMode.IndirectX)]
        [Opcode(0x51, "EOR", 5, AddressingMode.IndirectY)]
        private void Operation_EOR(AddressingMode mode)
        {
            var operand = FetchOperandByte(mode);

            operand ^= CurrentState.A;
            UpdateStandardFlags(operand);
            CurrentState.A = operand;
        }

        [Opcode(0x09, "ORA", 2, AddressingMode.Immediate)]
        [Opcode(0x05, "ORA", 3, AddressingMode.ZeroPage)]
        [Opcode(0x15, "ORA", 4, AddressingMode.ZeroPageX)]
        [Opcode(0x0D, "ORA", 4, AddressingMode.Absolute)]
        [Opcode(0x1D, "ORA", 4, AddressingMode.AbsoluteX)]
        [Opcode(0x19, "ORA", 4, AddressingMode.AbsoluteY)]
        [Opcode(0x01, "ORA", 6, AddressingMode.IndirectX)]
        [Opcode(0x11, "ORA", 5, AddressingMode.IndirectY)]
        private void Operation_ORA(AddressingMode mode)
        {
            var operand = FetchOperandByte(mode);

            operand |= CurrentState.A;
            UpdateStandardFlags(operand);
            CurrentState.A = operand;
        }

        #endregion

        #region Compares

        [Opcode(0xC9, "CMP", 2, AddressingMode.Immediate)]
        [Opcode(0xC5, "CMP", 3, AddressingMode.ZeroPage)]
        [Opcode(0xD5, "CMP", 4, AddressingMode.ZeroPageX)]
        [Opcode(0xCD, "CMP", 4, AddressingMode.Absolute)]
        [Opcode(0xDD, "CMP", 4, AddressingMode.AbsoluteX)]
        [Opcode(0xD9, "CMP", 4, AddressingMode.AbsoluteY)]
        [Opcode(0xC1, "CMP", 6, AddressingMode.IndirectX)]
        [Opcode(0xD1, "CMP", 5, AddressingMode.IndirectY)]
        private void Operation_CMP(AddressingMode mode)
        {
            var operand = FetchOperandByte(mode);

            var value = (ushort)(CurrentState.A - operand);

            CurrentState.SetStatusFlag(StatusFlags.Negative, (value & 0x0080) > 0);
            CurrentState.SetStatusFlag(StatusFlags.Zero, CurrentState.A == (value & 0x00FF));
            CurrentState.SetStatusFlag(StatusFlags.Carry, CurrentState.A >= (value & 0x00FF));
        }

        [Opcode(0xE0, "CPX", 2, AddressingMode.Immediate)]
        [Opcode(0xE4, "CPX", 3, AddressingMode.ZeroPage)]
        [Opcode(0xEC, "CPX", 4, AddressingMode.Absolute)]
        private void Operation_CPX(AddressingMode mode)
        {
            var operand = FetchOperandByte(mode);

            var value = (ushort)(CurrentState.X - operand);

            CurrentState.SetStatusFlag(StatusFlags.Negative, (value & 0x0080) > 0);
            CurrentState.SetStatusFlag(StatusFlags.Zero, CurrentState.X == (value & 0x00FF));
            CurrentState.SetStatusFlag(StatusFlags.Carry, CurrentState.X >= (value & 0x00FF));
        }

        [Opcode(0xC0, "CPY", 2, AddressingMode.Immediate)]
        [Opcode(0xC4, "CPY", 3, AddressingMode.ZeroPage)]
        [Opcode(0xCC, "CPY", 4, AddressingMode.Absolute)]
        private void Operation_CPY(AddressingMode mode)
        {
            var operand = FetchOperandByte(mode);

            var value = (ushort)(CurrentState.Y - operand);

            CurrentState.SetStatusFlag(StatusFlags.Negative, (value & 0x0080) > 0);
            CurrentState.SetStatusFlag(StatusFlags.Zero, CurrentState.Y == (value & 0x00FF));
            CurrentState.SetStatusFlag(StatusFlags.Carry, CurrentState.Y >= (value & 0x00FF));
        }

        [Opcode(0x24, "BIT", 3, AddressingMode.ZeroPage)]
        [Opcode(0x2C, "BIT", 4, AddressingMode.Absolute)]
        private void Operation_BIT(AddressingMode mode)
        {
            var operand = FetchOperandByte(mode);

            var value = (byte)(CurrentState.A & operand);

            CurrentState.SetStatusFlag(StatusFlags.Negative, (value & 0x80) == 1);
            CurrentState.SetStatusFlag(StatusFlags.Zero, value == 0x0);
            CurrentState.SetStatusFlag(StatusFlags.Overflow, (value & 0x40) == 1);
        }

        #endregion

        #region Jumps

        private void Jump(ushort address)
        {
            if (address >= 0x0002 && address <= 0x1FFF)
            {
                Nes.Debugger.Log(NesDebugger.TAG_CPU, "Jumping to working RAM address 0x{0:x}", address);
            }
            else if (address >= 0x6000 && address <= 0xFFFF)
            {
                Nes.Debugger.Log(NesDebugger.TAG_CPU, "Jumping to ROM address 0x{0:x}", address);
            }
            else
            {
                Nes.Debugger.Log(NesDebugger.TAG_CPU, "WARN: Jumping to bad address? 0x{0:x}", address);
            }

            CurrentState.PC = address;
        }

        [Opcode(0x4C, "JMP", 3, AddressingMode.Absolute)]
        [Opcode(0x6C, "JMP", 5, AddressingMode.Indirect)]
        private void Operation_JMP(AddressingMode mode)
        {
            var address = FetchOperandAddress(mode);
            Jump(address);
        }

        [Opcode(0x20, "JSR", 6, AddressingMode.Absolute)]
        private void Operation_JSR(AddressingMode mode)
        {
            // Write the PC onto the stack
            var returnPoint = (ushort)(CurrentState.PC - 1);

            StackPush(returnPoint);

            // Jump to the target address
            var address = FetchOperandAddress(mode);
            Jump(address);
        }

        [Opcode(0x60, "RTS", 6)]
        private void Operation_RTS(AddressingMode mode)
        {
            // Pull the PC from the stack
            var returnPoint = StackPull16();

            // Jump to restored address +1
            CurrentState.PC = (byte)(returnPoint + 1);
        }

        #endregion

        #region Branches

        private void BranchHandler(AddressingMode mode, StatusFlags flag, bool expected)
        {
            var offset = FetchOperandByte(mode);
            if (CurrentState.GetStatusFlag(flag) == expected)
            {
                currentCycleCost++;

                if (IsCrossingPageBoundry(CurrentState.PC, offset))
                {
                    currentCycleCost++;
                }

                CurrentState.PC += offset;
            }
        }

        [Opcode(0x10, "BPL", 2, AddressingMode.Relative)]
        private void Operation_BPL(AddressingMode mode)
        {
            BranchHandler(mode, StatusFlags.Negative, false);
        }

        [Opcode(0x30, "BMI", 2, AddressingMode.Relative)]
        private void Operation_BMI(AddressingMode mode)
        {
            BranchHandler(mode, StatusFlags.Negative, true);
        }

        [Opcode(0x50, "BVC", 2, AddressingMode.Relative)]
        private void Operation_BVC(AddressingMode mode)
        {
            BranchHandler(mode, StatusFlags.Overflow, false);
        }

        [Opcode(0x70, "BVS", 2, AddressingMode.Relative)]
        private void Operation_BVS(AddressingMode mode)
        {
            BranchHandler(mode, StatusFlags.Overflow, true);
        }

        [Opcode(0x90, "BCC", 2, AddressingMode.Relative)]
        private void Operation_BCC(AddressingMode mode)
        {
            BranchHandler(mode, StatusFlags.Carry, false);
        }

        [Opcode(0xB0, "BCS", 2, AddressingMode.Relative)]
        private void Operation_BCS(AddressingMode mode)
        {
            BranchHandler(mode, StatusFlags.Carry, true);
        }

        [Opcode(0xD0, "BNE", 2, AddressingMode.Relative)]
        private void Operation_BNE(AddressingMode mode)
        {
            BranchHandler(mode, StatusFlags.Zero, false);
        }

        [Opcode(0xF0, "BEQ", 2, AddressingMode.Relative)]
        private void Operation_BEQ(AddressingMode mode)
        {
            BranchHandler(mode, StatusFlags.Zero, true);
        }

        #endregion

        #region Interrupts

        private void Interrupt(InterruptMode mode)
        {
            if (mode != InterruptMode.Break)
            {
                currentCycleCost++;
            }
            if (mode != InterruptMode.Reset)
            {
                // Write the PC onto the stack
                StackPush(CurrentState.PC);

                // Write the CPU status onto the stack, setting interrupt flag
                var statusRegister = (byte)(CurrentState.P | ((byte)StatusFlagBytes.Break));
                StackPush(statusRegister);
            }
            else
            {
                // Wait a bit
                CurrentState.S -= 3;
                currentCycleCost += 3;
            }

            // Set the interrupt flag
            CurrentState.SetStatusFlag(StatusFlags.Interrupt, true);

            // Jump
            JumpWithVectorTable(FixedJumpVector.BRK);

            if (mode == InterruptMode.NMI)
            {
                this.ActiveNmi = false;
            }
        }

        [Opcode(0x00, "BRK", 0)]
        private void Operation_BRK(AddressingMode mode)
        {
            // why
            CurrentState.PC++;

            Interrupt(InterruptMode.Break);
        }

        #endregion

        #region CPU Control

        [Opcode(0x38, "SEC", 2)]
        private void Operation_SEC(AddressingMode mode)
        {
            CurrentState.SetStatusFlag(StatusFlags.Carry, true);
        }

        [Opcode(0x18, "CLC", 2)]
        private void Operation_CLC(AddressingMode mode)
        {
            CurrentState.SetStatusFlag(StatusFlags.Carry, true);
        }

        [Opcode(0xF8, "SED", 2)]
        private void Operation_SED(AddressingMode mode)
        {
            CurrentState.SetStatusFlag(StatusFlags.Decimal, true);
        }

        [Opcode(0xD8, "CLD", 2)]
        private void Operation_CLD(AddressingMode mode)
        {
            CurrentState.SetStatusFlag(StatusFlags.Decimal, true);
        }

        [Opcode(0x78, "SEI", 2)]
        private void Operation_SEI(AddressingMode mode)
        {
            CurrentState.SetStatusFlag(StatusFlags.Interrupt, true);
        }

        [Opcode(0x58, "CLI", 2)]
        private void Operation_CLI(AddressingMode mode)
        {
            CurrentState.SetStatusFlag(StatusFlags.Interrupt, false);
        }

        [Opcode(0xB8, "CLV", 2)]
        private void Operation_CLV(AddressingMode mode)
        {
            CurrentState.SetStatusFlag(StatusFlags.Overflow, false);
        }

        [Opcode(0xEA, "NOP", 2)]
        private void Operation_NOP(AddressingMode mode)
        {
            // Do nothing!
        }

        #endregion
    }

    delegate void Operation(AddressingMode mode);

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    class OpcodeAttribute : Attribute
    {
        public OpcodeAttribute(
            byte opcode,
            string name,
            byte cost,
            AddressingMode mode = AddressingMode.Unsupported
        )
        {
            this.Opcode = opcode;
            this.Name = name;
            this.Mode = mode;
            this.Cost = cost;
        }

        public byte Opcode { get; private set; }
        public string Name { get; private set; }
        public AddressingMode Mode { get; private set; }
        public byte Cost { get; private set; }
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
            StatusRegister.Set((byte)flag, value);
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
            S = 0xFD;
            P = 0x34;
        }
    }

    enum AddressingMode : int
    {
        Unsupported = 0,
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

    enum StatusFlagBytes : byte
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

    enum StatusFlags : int
    {
        /// <summary>C flag - 0=no carry, 1=carry</summary>
        Carry = 0,
        /// <summary>Z flagm 0=nonzero, 1=zero</summary>
        Zero = 1,
        /// <summary>I flag, 0=IRQ enable, 1=IRQ disable</summary>
        Interrupt = 2,
        /// <summary>Unused. D flag, 0=normal, 1=bcd mode</summary>
        Decimal = 3,
        /// <summary>B flag, 0=IRQ/NMI, 1=reset or BRK/PHP</summary>
        Break = 4,
        /// <summary>Unused. (always 1)</summary>
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
