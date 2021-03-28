using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using NesSharp.Debugger;
using NesSharp.Utils;

namespace NesSharp.CPU
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

        private readonly Nes Nes;

        public readonly NesCpuAddressBus Bus;
        internal readonly NesCpuMemory Memory;

        private uint CycleCount;
        private NesCpuState CurrentState;
        private byte currentCycleCost;
        private byte currentOpcode;
        private ushort? entryPoint;

        public NesCpu(Nes nes)
        {
            this.Nes = nes;
            this.Bus = new NesCpuAddressBus(nes);
            this.Memory = new NesCpuMemory(nes);

            CacheOpcodes();

            CurrentState = new NesCpuState();
        }

        public bool ActiveNmi { get; set; }
        public bool ActiveIrq { get; set; }
        public NesCpuState PublicCpuState { get { var copy = this.CurrentState; return copy; } }

        public bool Update()
        {
            CycleCount++;

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
            else if (ActiveIrq && !CurrentState.P_Interrupt)
            {
                Interrupt(InterruptMode.IRQ);
                CurrentState.CycleTicksRemain = currentCycleCost;
                return true;
            }

            Nes.Debugger.CpuStartCycle(CycleCount);

            var instruction = Bus.ReadByte(CurrentState.PC);
            CurrentState.PC++;

            // Calling this sets currentCycleCost and runs the function
            if (instruction == 0xFF)
            {
                throw new Exception("Caught illegal instruction (bad read?)");
            }

            var methodCall = FUNCTION_LOOKUP[instruction];

            if (methodCall != null)
            {
                methodCall();
            }
            else
            {
                Nes.Debugger.Log(NesDebugger.TAG_CPU, "Unsupported opcode 0x{0:X2}", instruction);

                if (Nes.Debugger.FailOnInvalidOpcode)
                {
                    // Update this here or we lose it
                    Nes.Debugger.CpuStartOpcode(instruction, (ushort)(CurrentState.PC - 1));
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
            Memory.SoftReset();
            Reset();
        }

        public void HardReset()
        {
            Nes.Debugger.Log(NesDebugger.TAG_CPU, "Hard-resetting CPU");
            CurrentState.HardReset();
            Memory.HardReset();
            Reset();
        }

        private void Reset()
        {
            ActiveNmi = false;
            ActiveIrq = false;

            if (entryPoint.HasValue)
            {
                CurrentState.PC = entryPoint.Value;
            }
            else
            {
                JumpWithVectorTable(FixedJumpVector.Reset);
            }
        }

        private void JumpWithVectorTable(FixedJumpVector fjv)
        {
            JumpWithVectorTable((ushort)fjv);
        }

        private void JumpWithVectorTable(ushort address)
        {
            var jumpVector = Bus.ReadAddress(address);
            if (jumpVector == 0xFFFF)
            {
                throw new IllegalMemoryAccessException(AddressBus.CPU, jumpVector, "Tried jumping to 0xFFFF, bad mapper configuration?");
            }

            CurrentState.PC = jumpVector;

            Nes.Debugger.Log(NesDebugger.TAG_CPU, "Set jump vector to 0x{0:X4}", jumpVector);
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
                    temp = addr = CurrentState.PC;
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
                    temp = addr = ReadUshortAtPC();

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
                        addr += CurrentState.Y;
                        if (isWrite || IsCrossingPageBoundry(addr, CurrentState.Y))
                        {
                            currentCycleCost++;
                        }
                    }
                    break;
                case AddressingMode.Indirect:
                    temp = ReadUshortAtPC();

                    // Zero page wraparound
                    var ixi_a = Bus.ReadByte((ushort)(temp & 0x00FF));
                    var ixi_b = Bus.ReadByte((ushort)((temp + 1) & 0x00FF));

                    addr = (ushort)(ixi_a | (ixi_b << 8));
                    break;
                case AddressingMode.IndirectX:
                    temp = ReadByteAtPC();

                    // Zero page wraparound
                    temp2 = (ushort)((temp + CurrentState.X) & 0x00FF);

                    var ixx_a = Bus.ReadByte((ushort)(temp2 & 0x00FF));
                    var ixx_b = Bus.ReadByte((ushort)((temp2 + 1) & 0x00FF));

                    addr = (ushort)(ixx_a | (ixx_b << 8));
                    break;
                case AddressingMode.IndirectY:
                    temp = ReadByteAtPC();

                    // Zero page wraparound
                    temp2 = (ushort)((temp & 0xFF00) | ((temp + 1) & 0x00FF));

                    var ixy_a = Bus.ReadByte(temp);
                    var ixy_b = Bus.ReadByte(temp2);
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

            Nes.Debugger.CpuSetOperand(temp, addr);

            return addr;
        }

        private sbyte FetchOperandRelative()
        {
            var uoffset = ReadByteAtPC();
            var offset = (sbyte)uoffset;

            Nes.Debugger.CpuSetOperand(uoffset, (ushort)(CurrentState.PC + offset));

            return offset;
        }

        private byte FetchOperandByte(AddressingMode mode, bool isWrite = false)
        {
            return FetchOperand(mode, isWrite).Item2;
        }

        private (ushort, byte) FetchOperand(AddressingMode mode, bool isWrite = false)
        {
            if (mode == AddressingMode.Accumulator)
            {
                return (0xFFFF, CurrentState.A);
            }

            var address = FetchOperandAddress(mode, isWrite);
            var operand = Bus.ReadByte(address);

            Nes.Debugger.CpuSetOpcodeMem(operand);

            return (address, operand);
        }

        private void WriteOperand(AddressingMode mode, ushort address, byte value)
        {
            if (mode == AddressingMode.Accumulator)
            {
                CurrentState.A = value;
            }

            Bus.Write(address, value);
        }

        private void WriteOperand(AddressingMode mode, (ushort, byte) param)
        {
            if (mode == AddressingMode.Accumulator)
            {
                CurrentState.A = param.Item2;
            }

            Bus.Write(param.Item1, param.Item2);
        }

        private bool IsCrossingPageBoundry(ushort address, int i)
        {
            return ((address + i) & 0xFF00) != ((address & 0xFF00));
        }

        private byte ReadByteAtPC()
        {
            var value = Bus.ReadByte(CurrentState.PC);
            CurrentState.PC++;
            return value;
        }

        private ushort ReadUshortAtPC()
        {
            var value = Bus.ReadAddress(CurrentState.PC);
            CurrentState.PC += 2;
            return value;
        }

        private void UpdateNZFlags(byte operand)
        {
            // Negative flag is checking a signed byte is < 0. This check is equatable to converting to an sbyte and testing directly
            CurrentState.P_Negative = (operand & 0x80) > 0;
            CurrentState.P_Zero = (operand == 0x0);
        }

        private void StackPush(byte value)
        {
            var address = (ushort)(NesConsts.MEM_STACK_START + CurrentState.S--);
            Bus.Write(address, value);
        }

        private void StackPush(ushort value)
        {
            StackPush((byte)((value >> 8) & 0xFF));
            StackPush((byte)(value & 0xFF));
        }

        private byte StackPull()
        {
            var address = (ushort)(NesConsts.MEM_STACK_START + ++CurrentState.S);
            return Bus.ReadByte(address);
        }

        private ushort StackPull16()
        {
            var lowByte = StackPull();
            var highByte = StackPull();
            var value = (ushort)(lowByte | highByte << 8);
            return value;
        }

        #endregion

        #region Opcodes

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
                        ushort startPC = (ushort)(CurrentState.PC - 1);
                        Nes.Debugger.CpuStartOpcode(opcode, startPC, mode);

                        this.currentCycleCost = cost;
                        this.currentOpcode = opcode;
                        del.Invoke(mode);

                        Nes.Debugger.CpuEndOpcode();
                    };

                    FUNCTION_LOOKUP[opcode] = call;
                }
            }
        }

        #endregion

        #region Memory to register

        [Opcode(0xA8, "TAY", 2)]
        private void Operation_TAY(AddressingMode mode)
        {
            UpdateNZFlags(CurrentState.A);
            CurrentState.Y = CurrentState.A;
        }

        [Opcode(0xAA, "TAX", 2)]
        private void Operation_TAX(AddressingMode mode)
        {
            UpdateNZFlags(CurrentState.A);
            CurrentState.X = CurrentState.A;
        }

        [Opcode(0xBA, "TSX", 2)]
        private void Operation_TSX(AddressingMode mode)
        {
            UpdateNZFlags(CurrentState.S);
            CurrentState.X = CurrentState.S;
        }

        [Opcode(0x98, "TYA", 2)]
        private void Operation_TYA(AddressingMode mode)
        {
            UpdateNZFlags(CurrentState.Y);
            CurrentState.A = CurrentState.Y;
        }

        [Opcode(0x8A, "TXA", 2)]
        private void Operation_TXA(AddressingMode mode)
        {
            UpdateNZFlags(CurrentState.X);
            CurrentState.A = CurrentState.X;
        }

        [Opcode(0x9A, "TXS", 2)]
        private void Operation_TXS(AddressingMode mode)
        {
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

            UpdateNZFlags(operand);
            CurrentState.A = operand;
        }

        [Opcode(0xA2, "LDX", 2, AddressingMode.Immediate)]
        [Opcode(0xA6, "LDX", 3, AddressingMode.ZeroPage)]
        [Opcode(0xB6, "LDX", 4, AddressingMode.ZeroPageX)]
        [Opcode(0xAE, "LDX", 4, AddressingMode.Absolute)]
        [Opcode(0xBE, "LDX", 4, AddressingMode.AbsoluteX)]
        private void Operation_LDX(AddressingMode mode)
        {
            var operand = FetchOperandByte(mode);

            UpdateNZFlags(operand);
            CurrentState.X = operand;
        }

        [Opcode(0xA0, "LDY", 2, AddressingMode.Immediate)]
        [Opcode(0xA4, "LDY", 3, AddressingMode.ZeroPage)]
        [Opcode(0xB4, "LDY", 4, AddressingMode.ZeroPageX)]
        [Opcode(0xAC, "LDY", 4, AddressingMode.Absolute)]
        [Opcode(0xBC, "LDY", 4, AddressingMode.AbsoluteX)]
        private void Operation_LDY(AddressingMode mode)
        {
            var operand = FetchOperandByte(mode);

            UpdateNZFlags(operand);
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
            Bus.Write(address, CurrentState.A);
        }

        [Opcode(0x86, "STX", 3, AddressingMode.ZeroPage)]
        [Opcode(0x96, "STX", 4, AddressingMode.ZeroPageY)]
        [Opcode(0x8E, "STX", 4, AddressingMode.Absolute)]
        private void Operation_STX(AddressingMode mode)
        {
            var address = FetchOperandAddress(mode, true);
            Bus.Write(address, CurrentState.X);
        }

        [Opcode(0x84, "STY", 3, AddressingMode.ZeroPage)]
        [Opcode(0x94, "STY", 4, AddressingMode.ZeroPageX)]
        [Opcode(0x8C, "STY", 4, AddressingMode.Absolute)]
        private void Operation_STY(AddressingMode mode)
        {
            var address = FetchOperandAddress(mode, true);
            Bus.Write(address, CurrentState.Y);
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
            var flags = (byte)(CurrentState.P | ((byte)StatusFlags.Break));
            StackPush(flags);
        }

        [Opcode(0x68, "PLA", 4)]
        private void Operation_PLA(AddressingMode mode)
        {
            var operand = StackPull();
            UpdateNZFlags(operand);
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

            byte carry = (byte)(CurrentState.P_Carry ? 1 : 0);
            ushort value = (ushort)(CurrentState.A + carry + operand);

            CurrentState.P_Negative = ((value & 0x0080) > 0);
            CurrentState.P_Zero = ((value & 0x00FF) == 0);
            CurrentState.P_Carry = ((value & 0xFF00) > 0);
            CurrentState.P_Overflow = (((value ^ CurrentState.A) & (value ^ operand) & 0x0080) > 0);

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

            byte carry = (byte)(CurrentState.P_Carry ? 1 : 0);
            ushort value = (ushort)(CurrentState.A - operand - (1 - carry));

            CurrentState.P_Negative = ((value & 0x0080) > 0);
            CurrentState.P_Zero = ((value & 0x00FF) == 0);
            CurrentState.P_Carry = ((value & 0xFF00) == 0);
            CurrentState.P_Overflow = (((value ^ CurrentState.A) & (value ^ operand) & 0x0080) > 0);

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
            UpdateNZFlags(operand);
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
            UpdateNZFlags(operand);
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
            UpdateNZFlags(operand);
            CurrentState.A = operand;
        }

        #endregion

        #region Compares

        private void Compares(AddressingMode mode, byte register)
        {
            var operand = FetchOperandByte(mode);

            var value = (byte)(register - operand);

            UpdateNZFlags(value);
            CurrentState.P_Carry = (register >= value);
        }

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
            Compares(mode, CurrentState.A);
        }

        [Opcode(0xE0, "CPX", 2, AddressingMode.Immediate)]
        [Opcode(0xE4, "CPX", 3, AddressingMode.ZeroPage)]
        [Opcode(0xEC, "CPX", 4, AddressingMode.Absolute)]
        private void Operation_CPX(AddressingMode mode)
        {
            Compares(mode, CurrentState.X);
        }

        [Opcode(0xC0, "CPY", 2, AddressingMode.Immediate)]
        [Opcode(0xC4, "CPY", 3, AddressingMode.ZeroPage)]
        [Opcode(0xCC, "CPY", 4, AddressingMode.Absolute)]
        private void Operation_CPY(AddressingMode mode)
        {
            Compares(mode, CurrentState.Y);
        }

        [Opcode(0x24, "BIT", 3, AddressingMode.ZeroPage)]
        [Opcode(0x2C, "BIT", 4, AddressingMode.Absolute)]
        private void Operation_BIT(AddressingMode mode)
        {
            var operand = FetchOperandByte(mode);

            var value = (byte)(CurrentState.A & operand);

            CurrentState.P_Negative = ((operand & 0x80) > 0);
            CurrentState.P_Zero = ((value) == 0x0);
            CurrentState.P_Overflow = ((operand & 0x40) > 0);
        }

        [Opcode(0xE6, "INC", 5, AddressingMode.ZeroPage)]
        [Opcode(0xF6, "INC", 6, AddressingMode.ZeroPageX)]
        [Opcode(0xEE, "INC", 6, AddressingMode.Absolute)]
        [Opcode(0xFE, "INC", 7, AddressingMode.AbsoluteX)]
        private void Operation_INC(AddressingMode mode)
        {
            var (address, operand) = FetchOperand(mode, true);

            byte value = (byte)(operand + 1);
            UpdateNZFlags(value);

            WriteOperand(mode, address, value);
        }

        [Opcode(0xE8, "INX", 2)]
        private void Operation_INX(AddressingMode mode)
        {
            byte value = (byte)(CurrentState.X + 1);
            UpdateNZFlags(value);
            CurrentState.X = value;
        }

        [Opcode(0xC8, "INY", 2)]
        private void Operation_INY(AddressingMode mode)
        {
            byte value = (byte)(CurrentState.Y + 1);
            UpdateNZFlags(value);
            CurrentState.Y = value;
        }

        [Opcode(0xC6, "DEC", 5, AddressingMode.ZeroPage)]
        [Opcode(0xD6, "DEC", 6, AddressingMode.ZeroPageX)]
        [Opcode(0xCE, "DEC", 6, AddressingMode.Absolute)]
        [Opcode(0xDE, "DEC", 7, AddressingMode.AbsoluteX)]
        private void Operation_DEC(AddressingMode mode)
        {
            var (address, operand) = FetchOperand(mode, true);

            byte value = (byte)(operand - 1);
            UpdateNZFlags(value);

            WriteOperand(mode, address, value);
        }

        [Opcode(0xCA, "DEX", 2)]
        private void Operation_DEX(AddressingMode mode)
        {
            byte value = (byte)(CurrentState.X - 1);
            UpdateNZFlags(value);
            CurrentState.X = value;
        }

        [Opcode(0x88, "DEY", 2)]
        private void Operation_DEY(AddressingMode mode)
        {
            byte value = (byte)(CurrentState.Y - 1);
            UpdateNZFlags(value);
            CurrentState.Y = value;
        }

        #endregion

        #region Rotate and shift

        [Opcode(0x0A, "ASL", 2, AddressingMode.Accumulator)]
        [Opcode(0x06, "ASL", 5, AddressingMode.ZeroPage)]
        [Opcode(0x16, "ASL", 6, AddressingMode.ZeroPageX)]
        [Opcode(0x0E, "ASL", 6, AddressingMode.Absolute)]
        [Opcode(0x1E, "ASL", 7, AddressingMode.AbsoluteX)]
        private void Operation_ASL(AddressingMode mode)
        {
            var (address, operand) = FetchOperand(mode, true);

            var result = (byte)(operand << 1);
            UpdateNZFlags(result);
            CurrentState.P_Carry = ((operand & 0x80) > 0);

            WriteOperand(mode, address, result);
        }

        [Opcode(0x4A, "LSR", 5, AddressingMode.Accumulator)]
        [Opcode(0x46, "LSR", 5, AddressingMode.ZeroPage)]
        [Opcode(0x56, "LSR", 6, AddressingMode.ZeroPageX)]
        [Opcode(0x4E, "LSR", 6, AddressingMode.Absolute)]
        [Opcode(0x5E, "LSR", 7, AddressingMode.AbsoluteX)]
        private void Operation_LSR(AddressingMode mode)
        {
            var (address, operand) = FetchOperand(mode, true);

            var result = (byte)(operand >> 1);
            UpdateNZFlags(result);
            CurrentState.P_Carry = ((operand & 0x1) > 0);

            WriteOperand(mode, address, result);
        }

        [Opcode(0x2A, "ROL", 5, AddressingMode.Accumulator)]
        [Opcode(0x26, "ROL", 5, AddressingMode.ZeroPage)]
        [Opcode(0x36, "ROL", 6, AddressingMode.ZeroPageX)]
        [Opcode(0x2E, "ROL", 6, AddressingMode.Absolute)]
        [Opcode(0x3E, "ROL", 7, AddressingMode.AbsoluteX)]
        private void Operation_ROL(AddressingMode mode)
        {
            var (address, operand) = FetchOperand(mode, true);

            var carry = CurrentState.P_Carry ? 1 : 0;
            var result = (byte)((operand << 1) | carry);
            UpdateNZFlags(result);
            CurrentState.P_Carry = ((operand & 0x80) > 0);

            WriteOperand(mode, address, result);
        }

        [Opcode(0x6A, "ROR", 5, AddressingMode.Accumulator)]
        [Opcode(0x66, "ROR", 5, AddressingMode.ZeroPage)]
        [Opcode(0x76, "ROR", 6, AddressingMode.ZeroPageX)]
        [Opcode(0x6E, "ROR", 6, AddressingMode.Absolute)]
        [Opcode(0x7E, "ROR", 7, AddressingMode.AbsoluteX)]
        private void Operation_ROR(AddressingMode mode)
        {
            var (address, operand) = FetchOperand(mode, true);

            var carry = CurrentState.P_Carry ? 1 : 0;
            var result = (byte)((operand >> 1) | (carry << 7));
            UpdateNZFlags(result);
            CurrentState.P_Carry = ((operand & 0x1) > 0);

            WriteOperand(mode, address, result);
        }

        #endregion

        #region Jumps

        private void Jump(ushort address)
        {
            if (address >= 0x0002 && address <= 0x1FFF)
            {
                Nes.Debugger.Log(NesDebugger.TAG_CPU, "Jumping to working RAM address 0x{0:X4}", address);
            }
            else if (address >= 0x6000 && address <= 0xFFFF)
            {
                Nes.Debugger.Log(NesDebugger.TAG_CPU, "Jumping to ROM address 0x{0:X4}", address);
            }
            else
            {
                Nes.Debugger.Log(NesDebugger.TAG_CPU, "WARN: Jumping to bad address? 0x{0:X4}", address);
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
            var returnPoint = (ushort)(CurrentState.PC + 1);

            StackPush(returnPoint);

            // Jump to the target address
            var address = FetchOperandAddress(mode);
            Jump(address);
        }

        [Opcode(0x40, "RTI", 6)]
        private void Operation_RTI(AddressingMode mode)
        {
            // Pull the flags from the stack
            var flags = StackPull();

            // Restore processor flags
            CurrentState.P = flags;

            // Pull the PC from the stack
            var returnPoint = StackPull16();

            // Jump to restored address
            CurrentState.PC = returnPoint;
        }

        [Opcode(0x60, "RTS", 6)]
        private void Operation_RTS(AddressingMode mode)
        {
            // Pull the PC from the stack
            var returnPoint = StackPull16();

            // Jump to restored address +1
            CurrentState.PC = (ushort)(returnPoint + 1);
        }

        #endregion

        #region Branches

        private void BranchHandler(StatusFlags flag, bool expected)
        {
            var offset = FetchOperandRelative();

            var flagState = ((CurrentState.P & (byte)flag) > 0);
            if (flagState == expected)
            {
                currentCycleCost++;

                if (IsCrossingPageBoundry(CurrentState.PC, offset))
                {
                    currentCycleCost++;
                }

                CurrentState.PC = (ushort)(CurrentState.PC + offset);
            }
        }

        [Opcode(0x10, "BPL", 2, AddressingMode.Relative)]
        private void Operation_BPL(AddressingMode mode)
        {
            BranchHandler(StatusFlags.Negative, false);
        }

        [Opcode(0x30, "BMI", 2, AddressingMode.Relative)]
        private void Operation_BMI(AddressingMode mode)
        {
            BranchHandler(StatusFlags.Negative, true);
        }

        [Opcode(0x50, "BVC", 2, AddressingMode.Relative)]
        private void Operation_BVC(AddressingMode mode)
        {
            BranchHandler(StatusFlags.Overflow, false);
        }

        [Opcode(0x70, "BVS", 2, AddressingMode.Relative)]
        private void Operation_BVS(AddressingMode mode)
        {
            BranchHandler(StatusFlags.Overflow, true);
        }

        [Opcode(0x90, "BCC", 2, AddressingMode.Relative)]
        private void Operation_BCC(AddressingMode mode)
        {
            BranchHandler(StatusFlags.Carry, false);
        }

        [Opcode(0xB0, "BCS", 2, AddressingMode.Relative)]
        private void Operation_BCS(AddressingMode mode)
        {
            BranchHandler(StatusFlags.Carry, true);
        }

        [Opcode(0xD0, "BNE", 2, AddressingMode.Relative)]
        private void Operation_BNE(AddressingMode mode)
        {
            BranchHandler(StatusFlags.Zero, false);
        }

        [Opcode(0xF0, "BEQ", 2, AddressingMode.Relative)]
        private void Operation_BEQ(AddressingMode mode)
        {
            BranchHandler(StatusFlags.Zero, true);
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

                // Write the CPU status onto the stack, setting break flag
                var statusRegister = CurrentState.P;
                if (mode == InterruptMode.Break)
                {
                    statusRegister = (byte)(statusRegister | ((byte)StatusFlags.Break));
                }
                StackPush(statusRegister);
            }
            else
            {
                // Wait a bit
                CurrentState.S -= 3;
                currentCycleCost += 3;
            }

            // Set the interrupt flag
            CurrentState.P_Interrupt = true;

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
            CurrentState.P_Carry = true;
        }

        [Opcode(0x18, "CLC", 2)]
        private void Operation_CLC(AddressingMode mode)
        {
            CurrentState.P_Carry = false;
        }

        [Opcode(0xF8, "SED", 2)]
        private void Operation_SED(AddressingMode mode)
        {
            CurrentState.P_Decimal = true;
        }

        [Opcode(0xD8, "CLD", 2)]
        private void Operation_CLD(AddressingMode mode)
        {
            CurrentState.P_Decimal = false;
        }

        [Opcode(0x78, "SEI", 2)]
        private void Operation_SEI(AddressingMode mode)
        {
            CurrentState.P_Interrupt = true;
        }

        [Opcode(0x58, "CLI", 2)]
        private void Operation_CLI(AddressingMode mode)
        {
            CurrentState.P_Interrupt = false;
        }

        [Opcode(0xB8, "CLV", 2)]
        private void Operation_CLV(AddressingMode mode)
        {
            CurrentState.P_Overflow = false;
        }

        [Opcode(0xEA, "NOP", 2)]
        private void Operation_NOP(AddressingMode mode)
        {
            return;
        }

        #endregion

        #endregion

        #region Debugging

        public void Debug_SetEntryPoint(ushort address)
        {
            entryPoint = address;
        }

        #endregion
    }

    delegate void Operation(AddressingMode mode);
}
