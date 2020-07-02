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
        }

        protected Nes Nes;

        public ConsoleView ConsoleView { get; set; }

        public bool FailOnInvalidOpcode { get; set; }
        public bool StepMode { get; set; }
        public string[] LogFilters { get; set; }
        public byte LastOpcode { get; set; }
        public ushort? LastOpcodeMemoryOperandValue { get; set; }
        public ushort? LastOpcodeMemoryResolvedAddress { get; set; }
        public AddressingMode? LastOpcodeMemoryAddressMode { get; set; }
        public byte? LastOpcodeMemoryValue { get; set; }

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

        public void Log(string component, string message, params object[] parm)
        {
            if (!(LogFilters?.Contains(component) ?? true))
            {
                return;
            }

            ConsoleView.Log(component, message, parm);
        }

        [Conditional("DEBUG")]
        public void ExecOpCode(ushort pc, byte opCode)
        {
            var name = "???";
            if (NesCpu.AllOpcodeNames.ContainsKey(opCode))
            {
                name = NesCpu.AllOpcodeNames[opCode];
            }

            LastOpcode = opCode;
            var stackStr = BitConverter.ToString(CurrentStack).Replace("-", " ");

            var logMessage = String.Format("Executed {1}: {0:X2}", opCode, name);
            var chaseMessage = String.Format("{0:X4} | {1:X2} {2}", pc, opCode, name);

            if (LastOpcodeMemoryOperandValue.HasValue)
            {
                byte lmv = LastOpcodeMemoryValue.HasValue ? LastOpcodeMemoryValue.Value : (byte)0;
                logMessage += " " + AddressOperandToString(LastOpcodeMemoryAddressMode.Value, LastOpcodeMemoryOperandValue.Value, lmv);
                chaseMessage += " " + AddressOperandToString(LastOpcodeMemoryAddressMode.Value, LastOpcodeMemoryOperandValue.Value, lmv).PadRight(6);

                if (LastOpcodeMemoryResolvedAddress.HasValue && LastOpcodeMemoryValue.HasValue)
                {
                    logMessage += String.Format(" [{0:X4}] -> {1:X2}", LastOpcodeMemoryResolvedAddress.Value, LastOpcodeMemoryValue.Value);
                    chaseMessage += String.Format(" [{0:X4}] -> {1:X2}", LastOpcodeMemoryResolvedAddress.Value, LastOpcodeMemoryValue.Value);
                }
                else if (LastOpcodeMemoryResolvedAddress.HasValue)
                {
                    logMessage += String.Format(" [{0:X4}[", LastOpcodeMemoryResolvedAddress.Value);
                    chaseMessage += String.Format(" [{0:X4}]", LastOpcodeMemoryResolvedAddress.Value);
                }
            }

            ConsoleView.Log(TAG_CPU, logMessage);

            chaseMessage = chaseMessage.PadRight(37);

            var cpuState = Nes.Cpu.PublicCpuState;

            chaseMessage += String.Format("A:{0:X2} X:{1:X2} Y:{2:X2} S:{3:X2}",
                cpuState.A,
                cpuState.X,
                cpuState.Y,
                cpuState.S
            );
            chaseMessage += String.Format(" C:{0} Z:{1} I:{2} B:{4} V:{6} N:{7}",
                (cpuState.P & (byte)StatusFlagBytes.Carry) > 0 ? 1 : 0,
                (cpuState.P & (byte)StatusFlagBytes.Zero) > 0 ? 1 : 0,
                (cpuState.P & (byte)StatusFlagBytes.Interrupt) > 0 ? 1 : 0,
                (cpuState.P & (byte)StatusFlagBytes.Decimal) > 0 ? 1 : 0,
                (cpuState.P & (byte)StatusFlagBytes.Break) > 0 ? 1 : 0,
                (cpuState.P & (byte)StatusFlagBytes.Always1) > 0 ? 1 : 0,
                (cpuState.P & (byte)StatusFlagBytes.Overflow) > 0 ? 1 : 0,
                (cpuState.P & (byte)StatusFlagBytes.Negative) > 0 ? 1 : 0
            );

            chaseMessage += "    [" + stackStr + "]";

            WriteToChasefile(chaseMessage);
        }

        public void DumpOpcodes()
        {
            Console.WriteLine("Total of {0} opcodes implemented", NesCpu.AllOpcodeNames.Count);
        }

        public void DumpPage(MemoryMapResponse mmr)
        {
            System.IO.File.WriteAllBytes("dump.bin", mmr.ToArray());
        }

        public void DumpPage(byte[] bytes)
        {
            System.IO.File.WriteAllBytes("dump.bin", bytes);
        }

        public void DumpAllMemory()
        {
            var cpuMemory = new byte[0x10000];
            for (var i = 0; i <= 0xFFFF; i++)
            {
                cpuMemory[i] = Nes.Cpu.Bus.ReadByte((ushort)i, true);
            }
            System.IO.File.WriteAllBytes("dump_cpu.bin", cpuMemory);

            /*var ppuMemory = new byte[0x4000];
            for (var i = 0; i <= 0x3FFF; i++)
            {
                ppuMemory[i] = Nes.Ppu.Bus.ReadByte((ushort)i, true);
            }
            System.IO.File.WriteAllBytes("dump_cpu.bin", ppuMemory);*/
        }

        private void WriteToChasefile(string output)
        {
            System.IO.File.AppendAllText("operchase.txt", output + "\r\n");
        }

        public string AddressOperandToString(AddressingMode mode, ushort address, byte value = 0)
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