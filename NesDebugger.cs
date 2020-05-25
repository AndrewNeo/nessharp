using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NesSharp.CPU;
using NesSharp.Memory;

namespace NesSharp
{
    public class NesDebugger
    {
        public const string TAG_SYS = "SYS";
        public const string TAG_CPU = "CPU";
        public const string TAG_RAM = "RAM";
        public const string TAG_MAP = "MAP";
        public const string TAG_PPU = "PPU";

        public NesDebugger(Nes nes)
        {
            this.Nes = nes;
        }

        protected Nes Nes;

        public void Log(string component, string message, params object[] parm)
        {
            Console.Write($"DEBUGGER [{component}]: ");
            Console.WriteLine(message, parm);
        }

        public void ExecOpCode(byte opCode)
        {
            var name = "?";
            if (NesCpu.AllOpcodeNames.ContainsKey(opCode))
            {
                name = NesCpu.AllOpcodeNames[opCode];
            }

            Console.WriteLine("DEBUGGER [CPU]: Opcode 0x{0:x} ({1})", opCode, name);
        }

        public void DumpOpcodes()
        {
            /*foreach (var opcode in NesCpu.AllOpcodeNames)
            {
                Console.WriteLine("Opcode 0x{0:x} {1}", opcode.Key, opcode.Value);
            }*/

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