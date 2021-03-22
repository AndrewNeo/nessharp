using System;
using System.Diagnostics;
using System.Linq;
using System.Text;
using NesSharp.CPU;
using NesSharp.Memory;
using NesSharp.Utils;

namespace NesSharp.Debugger
{
    public class NesDebugger
    {
        public const string TAG_SYS = "SYS";
        public const string TAG_CPU = "CPU";
        public const string TAG_RAM = "RAM";
        public const string TAG_MAP = "MAP";
        public const string TAG_PPU = "PPU";
        public const string TAG_GUI = "GUI";

        public NesDebugger(Nes nes)
        {
            this.Nes = nes;
            this.ConsoleView = new ConsoleView(nes);
            this._debugInfo = new DebugInfo();
        }

        protected Nes Nes;

        public ConsoleView ConsoleView { get; set; }

        private DebugInfo _debugInfo;
        public DebugInfo DebugInfo { get { return _debugInfo; } }

        public bool FailOnInvalidOpcode { get; set; }
        public bool TestMode { get; set; }
        public bool Tracing { get; set; }
        public bool StepMode { get; set; }
        public string[] LogFilters { get; set; }

        public byte[] CurrentStack
        {
            get
            {
                var stackSize = NesConsts.MEM_STACK_SIZE - Nes.Cpu.PublicCpuState.S;
                var output = new byte[stackSize];
                for (int i = 0; i < stackSize; i++)
                {
                    output[i] = Nes.Cpu.Bus.ReadByte((ushort)(NesConsts.MEM_STACK_START + 0xFF - i));
                }
                return output;
            }
        }

        public byte GetTestStatus()
        {
            return Nes.Cpu.Bus.ReadByte(0x6000, true);
        }

        public bool IsTestMode()
        {
            if (!TestMode)
            {
                return false;
            }

            var a1 = Nes.Cpu.Bus.ReadByte(0x6001, true);
            var a2 = Nes.Cpu.Bus.ReadByte(0x6002, true);
            var a3 = Nes.Cpu.Bus.ReadByte(0x6003, true);

            return a1 == 0xDE && a2 == 0xB0 && a3 == 0x61;
        }

        public string GetTestTextOutput()
        {
            var testStrBytes = new byte[1024];
            var len = 0;
            for (var i = 0; i < testStrBytes.Length; i++)
            {
                var strb = Nes.Cpu.Bus.ReadByte((ushort)(0x6004 + i), true);
                if (strb == 0x00)
                {
                    break;
                }
                testStrBytes[i] = strb;
                len++;
            }

            var testStr = Encoding.ASCII.GetString(testStrBytes.AsSpan(0, len).ToArray());
            return testStr;
        }

        public void CpuStartCycle(uint cycleCount)
        {
            _debugInfo.Ticks = cycleCount;
            _debugInfo.MapperId = Nes.Cart.Header.MapperNumber;
            _debugInfo.CpuState = Nes.Cpu.PublicCpuState;
            _debugInfo.PpuCtrl = Nes.Ppu.ControlRegister;
            _debugInfo.PpuMask = Nes.Ppu.MaskRegister;
            _debugInfo.PpuStatus = Nes.Ppu.StatusRegister;
            _debugInfo.PpuScanline = Nes.Ppu.CurrentScanline;
            _debugInfo.PpuDot = Nes.Ppu.CurrentDot;
        }

        public void CpuStartOpcode(byte opCode, ushort pc, AddressingMode? addressingMode = null)
        {
            _debugInfo.Opcode = opCode;
            _debugInfo.PC = pc;
            _debugInfo.MemoryAddressMode = addressingMode;
        }

        public void CpuSetOperand(ushort operandValue, ushort address)
        {
            _debugInfo.MemoryOperandValue = operandValue;
            _debugInfo.ResolvedAddress = address;
        }

        public void CpuSetOpcodeMem(byte operand)
        {
            _debugInfo.MemoryValue = operand;
        }

        public void CpuEndOpcode()
        {
            if (this.Tracing)
            {
                ExecOpCodeLog();
                ExecOpCodeChaseFile();
            }
        }

        [Conditional("TRACE")]
        public void Log(string component, string message, params object[] parm)
        {
            if (!(LogFilters?.Contains(component) ?? true))
            {
                return;
            }

            System.Diagnostics.Trace.WriteLine($"DEBUGGER [{component}]: " + String.Format(message, parm));
        }

        [Conditional("TRACE")]
        public void ExecOpCodeLog()
        {
            var opCode = _debugInfo.Opcode;

            var name = "???";
            if (NesCpu.AllOpcodeNames.ContainsKey(opCode))
            {
                name = NesCpu.AllOpcodeNames[opCode];
            }

            var stackStr = BitConverter.ToString(CurrentStack).Replace("-", " ");

            var message = String.Format("Executed {1}: {0:X2}", opCode, name);

            if (_debugInfo.MemoryOperandValue.HasValue)
            {
                byte lmv = _debugInfo.MemoryValue.HasValue ? _debugInfo.MemoryValue.Value : (byte)0;
                message += " " + AddressOperandToString(_debugInfo.MemoryAddressMode.Value, _debugInfo.MemoryOperandValue.Value, lmv);

                if (_debugInfo.ResolvedAddress.HasValue && _debugInfo.MemoryValue.HasValue)
                {
                    message += String.Format(" [{0:X4}] -> {1:X2}", _debugInfo.ResolvedAddress.Value, _debugInfo.MemoryValue.Value);
                }
                else if (_debugInfo.ResolvedAddress.HasValue)
                {
                    message += String.Format(" [{0:X4}]", _debugInfo.ResolvedAddress.Value);
                }
            }

            this.Log(TAG_CPU, message);
        }

        [Conditional("DEBUG")]
        public void ExecOpCodeChaseFile()
        {
            var opCode = _debugInfo.Opcode;
            var pc = _debugInfo.PC;

            var name = "???";
            if (NesCpu.AllOpcodeNames.ContainsKey(opCode))
            {
                name = NesCpu.AllOpcodeNames[opCode];
            }

            var stackStr = BitConverter.ToString(CurrentStack).Replace("-", " ");

            var message = String.Format("{0:X4} | {1:X2} {2}", pc, opCode, name);

            if (_debugInfo.MemoryOperandValue.HasValue)
            {
                byte lmv = _debugInfo.MemoryValue.HasValue ? _debugInfo.MemoryValue.Value : (byte)0;
                message += " " + AddressOperandToString(_debugInfo.MemoryAddressMode.Value, _debugInfo.MemoryOperandValue.Value, lmv).PadRight(6);

                if (_debugInfo.ResolvedAddress.HasValue && _debugInfo.MemoryValue.HasValue)
                {
                    message += String.Format(" [{0:X4}] -> {1:X2}", _debugInfo.ResolvedAddress.Value, _debugInfo.MemoryValue.Value);
                }
                else if (_debugInfo.ResolvedAddress.HasValue)
                {
                    message += String.Format(" [{0:X4}]", _debugInfo.ResolvedAddress.Value);
                }
            }

            message = message.PadRight(37);

            var cpuState = Nes.Cpu.PublicCpuState;

            message += String.Format("A:{0:X2} X:{1:X2} Y:{2:X2} S:{3:X2}",
                cpuState.A,
                cpuState.X,
                cpuState.Y,
                cpuState.S
            );
            message += String.Format(" C:{0} Z:{1} I:{2} B:{4} V:{6} N:{7}",
                (cpuState.GetStatusFlag(StatusFlags.Carry) ? 1 : 0),
                (cpuState.GetStatusFlag(StatusFlags.Zero) ? 1 : 0),
                (cpuState.GetStatusFlag(StatusFlags.Interrupt) ? 1 : 0),
                (cpuState.GetStatusFlag(StatusFlags.Decimal) ? 1 : 0),
                (cpuState.GetStatusFlag(StatusFlags.Break) ? 1 : 0),
                (cpuState.GetStatusFlag(StatusFlags.Always1) ? 1 : 0),
                (cpuState.GetStatusFlag(StatusFlags.Overflow) ? 1 : 0),
                (cpuState.GetStatusFlag(StatusFlags.Negative) ? 1 : 0)
            );
            message += String.Format("  PPU: {0,3},{1,3}", _debugInfo.PpuScanline, _debugInfo.PpuDot);
            message += String.Format("  CYC: {0,8}", _debugInfo.Ticks);

            message += "    [" + stackStr + "]";

            WriteToChasefile(message);
        }

        [Conditional("DEBUG")]
        public void DumpOpcodes()
        {
            Console.WriteLine("Total of {0} opcodes implemented", NesCpu.AllOpcodeNames.Count);
        }

        [Conditional("DEBUG")]
        public void DumpPage(MemoryMapResponse mmr)
        {
            System.IO.File.WriteAllBytes("dump.bin", mmr.ToArray());
        }

        [Conditional("DEBUG")]
        public void DumpPage(byte[] bytes)
        {
            System.IO.File.WriteAllBytes("dump.bin", bytes);
        }

        [Conditional("DEBUG")]
        public void DumpAllMemory()
        {
            var cpuMemory = new byte[0x10000];
            for (var i = 0; i <= 0xFFFF; i++)
            {
                cpuMemory[i] = Nes.Cpu.Bus.ReadByte((ushort)i, true);
            }
            System.IO.File.WriteAllBytes("dump_cpu.bin", cpuMemory);

            var ppuMemory = new byte[0x4000];
            for (var i = 0; i <= 0x3FFF; i++)
            {
                ppuMemory[i] = Nes.Ppu.Bus.ReadByte((ushort)i, true);
            }
            System.IO.File.WriteAllBytes("dump_ppu.bin", ppuMemory);
        }

        private void WriteToChasefile(string output)
        {
            System.IO.File.AppendAllText("operchase.txt", output + "\r\n");
        }

        private string AddressOperandToString(AddressingMode mode, ushort address, byte value = 0)
        {
            switch (mode)
            {
                case AddressingMode.Implicit:
                    return "";
                case AddressingMode.Immediate:
                    return $"#{value:X2}";
                case AddressingMode.Relative:
                    var saddr = (sbyte)address;
                    var ssign = saddr < 0 ? "-" : "+";
                    var sstr = saddr < 0 ? -saddr : saddr;
                    return $"*{ssign}{sstr:X}";
                case AddressingMode.ZeroPage:
                    return $"{address:X2}";
                case AddressingMode.ZeroPageX:
                    return $"{address:X2},X";
                case AddressingMode.ZeroPageY:
                    return $"{address:X2},Y";
                case AddressingMode.Absolute:
                    return $"{address:X4}";
                case AddressingMode.AbsoluteX:
                    return $"{address:X4},X";
                case AddressingMode.AbsoluteY:
                    return $"{address:X4},Y";
                case AddressingMode.Indirect:
                    return $"({address:X4})";
                case AddressingMode.IndirectX:
                    return $"({address:X2},X)";
                case AddressingMode.IndirectY:
                    return $"({address:X2}),Y";
                default:
                    return $"?{address:X4}";
            }
        }
    }
}