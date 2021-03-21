﻿using System;
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

            using (Nes nes = new Nes())
            {
                //nes.LoadCartFromFile("TestROMs/Super Mario Bros 3 (U) (PRG 1) [h2].nes");
                nes.Debugger.LogFilters = new string[] { NesDebugger.TAG_SYS };
                nes.LoadCartFromFile("TestROMs/instr_test-v5/rom_singles/01-basics.nes");
                // nes.Debugger.DumpPage(nes.Cart.PrgRom);
                nes.InitGui();
                // nes.Start(0xC000);
                nes.Start(step: stepMode, failOnInvalidOpcode: failOnInvalidOpcode);
                nes.Quit();
            }
        }
    }
}
