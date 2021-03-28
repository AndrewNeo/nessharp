using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using NesSharp.CPU;

namespace NesSharp.Debugger
{
    public class ConsoleView
    {
        private Nes Nes;

        public ConsoleView(Nes nes)
        {
            this.Nes = nes;
            this.TickTrackTimer = new Stopwatch();

            try
            {
                Console.Clear();
                AttachedToConsole = true;
            }
            catch (IOException)
            {
                AttachedToConsole = false;
            }
        }

        public bool AttachedToConsole { get; private set; }
        private Stopwatch TickTrackTimer;
        public float ClockspeedCounterKhz { get; private set; }
        private uint LastFrameTickCount;
        private int LastLine { get { return Console.WindowHeight - 2; } }
        private const int OUTPUT_START = 5;

        public void Start()
        {
            if (!AttachedToConsole) return;

            TickTrackTimer.Start();

            Console.Clear();

            string header = "NesSharp";

            if (Nes.Cart.Filename != null)
            {
                header = $"NesSharp: {Nes.Cart.Filename}";
            }
            else
            {
                header = "NesSharp: No file loaded";
            }

            Console.Title = header;

            WriteWide(0, header);
            WriteWide(1, "Tick: xxxxxx    Last executed command: 0xXX    Mapper ID: XX    Clockspeed: XXXXXkHz    RTX: XXX%");
            WriteWide(2, "PC:XXXX A:XX X:XX Y:XX S:XX PP:X PP:X PP:X PP:X PP:X PP:X PP:X PP:X    ");
            WriteWide(3, "PPUCTRL: XX    PPUMASK: XX    PPUSTATUS: XX    Scanline: XXX");

            if (Nes.Debugger.TestMode)
            {
                WriteWide(4, "Memory at 0x6000: XX");
            }

            WriteWide(OUTPUT_START, " === Console output ===");

            Update();
        }

        public void Update()
        {
            if (TickTrackTimer.ElapsedMilliseconds > 1000)
            {
                ClockspeedCounterKhz = ((Nes.Debugger.DebugInfo.Ticks - LastFrameTickCount) / (float)TickTrackTimer.ElapsedMilliseconds);
                TickTrackTimer.Restart();
                LastFrameTickCount = Nes.Debugger.DebugInfo.Ticks;
            }

            if (!AttachedToConsole) return;

            // Status line
            Console.SetCursorPosition(6, 1);
            Console.Write("{0:D6}", Nes.Debugger.DebugInfo.Ticks);
            Console.SetCursorPosition(41, 1);
            Console.Write("{0:X2}", Nes.Debugger.DebugInfo.Opcode);
            Console.SetCursorPosition(58, 1);
            Console.Write("{0:D2}", Nes.Debugger.DebugInfo.MapperId);
            Console.SetCursorPosition(76, 1);
            Console.Write("{0:G4}kHz   ", this.ClockspeedCounterKhz);
            Console.SetCursorPosition(93, 1);
            Console.Write("{0:P1}   ", ((this.ClockspeedCounterKhz / 1000.0) / 1.7897725f));

            // CPU line
            var cpuState = Nes.Debugger.DebugInfo.CpuState;
            Console.SetCursorPosition(3, 2);
            Console.Write("{0:X4}", cpuState.PC);
            Console.SetCursorPosition(10, 2);
            Console.Write("{0:X2}", cpuState.A);
            Console.SetCursorPosition(15, 2);
            Console.Write("{0:X2}", cpuState.X);
            Console.SetCursorPosition(20, 2);
            Console.Write("{0:X2}", cpuState.Y);
            Console.SetCursorPosition(25, 2);
            Console.Write("{0:X2}", cpuState.S);

            Console.SetCursorPosition(28, 2);
            Console.Write("p:{0:X2} pC:{1} pZ:{2} pI:{3} pD:{4} pV:{5} pN:{6}",
                cpuState.P,
                (cpuState.P_Carry ? 1 : 0),
                (cpuState.P_Zero ? 1 : 0),
                (cpuState.P_Interrupt ? 1 : 0),
                (cpuState.P_Decimal ? 1 : 0),
                (cpuState.P_Overflow ? 1 : 0),
                (cpuState.P_Negative ? 1 : 0)
            );

            // PPU line
            Console.SetCursorPosition(9, 3);
            Console.Write("{0:X2}", Nes.Debugger.DebugInfo.PpuCtrl);
            Console.SetCursorPosition(24, 3);
            Console.Write("{0:X2}", Nes.Debugger.DebugInfo.PpuMask);
            Console.SetCursorPosition(41, 3);
            Console.Write("{0:X2}", Nes.Debugger.DebugInfo.PpuStatus);
            Console.SetCursorPosition(57, 3);
            Console.Write("{0:D3}", Nes.Debugger.DebugInfo.PpuScanline);

            // Test mode line
            if (Nes.Debugger.TestMode)
            {
                Console.SetCursorPosition(18, 4);
                Console.Write("{0:X2}", Nes.Debugger.GetTestStatus());

                Console.SetCursorPosition(37, 4);
                var testStr = Nes.Debugger.GetTestTextOutput();
                Console.Write(testStr);
            }

            // Move back to normal write window
            Console.SetCursorPosition(0, OUTPUT_START + 1);
        }

        public void Tick()
        {
            if (AttachedToConsole && Nes.Debugger.StepMode)
            {
                // Wait for key to be pressed
                Console.ReadKey();
            }

            if (Nes.Debugger.StepMode)
            {
                Start();
            }
        }

        public void End()
        {
            if (AttachedToConsole)
            {
                Console.SetCursorPosition(0, 0);
            }
        }

        private void WriteWide(int line, string message)
        {
            Console.SetCursorPosition(0, line);
            Console.Write(message.PadRight(Console.WindowWidth));
        }
    }
}