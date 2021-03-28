using System;
using System.Linq;
using NesSharp.Debugger;

namespace NesSharp
{
    class Program
    {
        static void Main(string[] args)
        {
            bool stepMode = false;
            if (args != null && args.Contains("--step"))
            {
                stepMode = true;
            }
            bool failOnInvalidOpcode = false;
            if (args != null && args.Contains("--failOnInvalidOpcode"))
            {
                failOnInvalidOpcode = true;
            }

            if (System.IO.File.Exists("operchase.txt"))
            {
                System.IO.File.Delete("operchase.txt");
            }

            /*if (RunTests())
            {
                return;
            }*/

            using (Nes nes = new Nes())
            {
                nes.Debugger.Tracing = true;
                nes.Debugger.TestMode = true;
                nes.Debugger.LogFilters = new string[] { NesDebugger.TAG_SYS };
                // nes.LoadCartFromFile("TestROMs/Super Mario Bros 3 (U) (PRG 1) [h2].nes");
                // nes.LoadCartFromFile("TestROMs/nestest.nes");
                nes.LoadCartFromFile("TestROMs/instr_test-v5/rom_singles/02-implied.nes");
                // nes.Debugger.DumpPage(nes.Cart.PrgRom);
                nes.InitGui();
                // nes.Start(0xC000);
                nes.Start(step: stepMode, failOnInvalidOpcode: failOnInvalidOpcode);
                nes.Quit();
            }
        }

        private static bool RunTests()
        {
            var basepath = "TestROMs/instr_test-v5/rom_singles";
            var files = System.IO.Directory.GetFiles(basepath, "*.nes");

            foreach (var fname in files)
            {
                using (Nes nes = new Nes())
                {
                    nes.Debugger.Tracing = false;
                    nes.Debugger.TestMode = true;
                    nes.Debugger.LogFilters = new string[] { NesDebugger.TAG_SYS };
                    nes.LoadCartFromFile(fname);
                    nes.InitGui();
                    nes.Start(failOnInvalidOpcode: true);
                    nes.Quit();
                }
            }

            return true;
        }
    }
}
