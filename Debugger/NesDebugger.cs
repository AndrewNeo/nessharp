using System;
using System.Linq;
using NesSharp.CPU;
using NesSharp.Memory;

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

        public void Log(string component, string message, params object[] parm)
        {
            if (!(LogFilters?.Contains(component) ?? true))
            {
                return;
            }

            ConsoleView.Log(component, message, parm);
        }

        public void ExecOpCode(byte opCode)
        {
            var name = "?";
            if (NesCpu.AllOpcodeNames.ContainsKey(opCode))
            {
                name = NesCpu.AllOpcodeNames[opCode];
            }

            LastOpcode = opCode;

            ConsoleView.Log(TAG_CPU, "Executed {1}: {0:X2}", opCode, name);
        }

        public void ExecOpCode(byte opCode, ushort data)
        {
            var name = "?";
            if (NesCpu.AllOpcodeNames.ContainsKey(opCode))
            {
                name = NesCpu.AllOpcodeNames[opCode];
            }

            if (data <= 0xFF)
            {
                ConsoleView.Log(TAG_CPU, "Executed {1}: {0:X2} {2:X2}", opCode, name, data);
            }
            else
            {
                ConsoleView.Log(TAG_CPU, "Executed {1}: {0:X2} {2:X4}", opCode, name, data);
            }
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
    }
}