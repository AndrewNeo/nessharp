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
            this.Ticks = 0;
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
        public uint Ticks { get; private set; }
        private Stopwatch TickTrackTimer;
        public float ClockspeedCounterKhz { get; private set; }
        private uint LastFrameTickCount;
        private int LastLine { get { return Console.WindowHeight - 2; } }
        private const bool TEST_MODE = true;
        private const int OUTPUT_START = 4 + (TEST_MODE ? 1 : 0);

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

            if (TEST_MODE)
            {
                WriteWide(4, "Memory at 0x6000: XX    XX XX XX    _");
            }

            WriteWide(OUTPUT_START, " === Console output ===");

            Update();
        }

        public void Update()
        {
            if (TickTrackTimer.ElapsedMilliseconds > 1000)
            {
                ClockspeedCounterKhz = ((Ticks - LastFrameTickCount) / (float)TickTrackTimer.ElapsedMilliseconds);
                TickTrackTimer.Restart();
                LastFrameTickCount = Ticks;
            }

            if (!AttachedToConsole) return;

            // Status line
            Console.SetCursorPosition(6, 1);
            Console.Write("{0:D6}", this.Ticks);
            Console.SetCursorPosition(41, 1);
            Console.Write("{0:X2}", Nes.Debugger.LastOpcode);
            Console.SetCursorPosition(58, 1);
            Console.Write("{0:D2}", Nes.Cart.Header.MapperNumber);
            Console.SetCursorPosition(76, 1);
            Console.Write("{0:G4}kHz   ", this.ClockspeedCounterKhz);
            Console.SetCursorPosition(93, 1);
            Console.Write("{0:P1}   ", ((this.ClockspeedCounterKhz / 1000.0) / 1.7897725f));

            // CPU line
            var cpuState = Nes.Cpu.PublicCpuState;
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
            Console.Write("pC:{0} pZ:{1} pI:{2} pD:{3} pB:{4} p1:{5} pV:{6} pN:{7}",
                (cpuState.P & (byte)StatusFlagBytes.Carry) > 0 ? 1 : 0,
                (cpuState.P & (byte)StatusFlagBytes.Zero) > 0 ? 1 : 0,
                (cpuState.P & (byte)StatusFlagBytes.Interrupt) > 0 ? 1 : 0,
                (cpuState.P & (byte)StatusFlagBytes.Decimal) > 0 ? 1 : 0,
                (cpuState.P & (byte)StatusFlagBytes.Break) > 0 ? 1 : 0,
                (cpuState.P & (byte)StatusFlagBytes.Always1) > 0 ? 1 : 0,
                (cpuState.P & (byte)StatusFlagBytes.Overflow) > 0 ? 1 : 0,
                (cpuState.P & (byte)StatusFlagBytes.Negative) > 0 ? 1 : 0
            );

            // PPU line
            Console.SetCursorPosition(9, 3);
            Console.Write("{0:X2}", Nes.Ppu.ControlRegister);
            Console.SetCursorPosition(24, 3);
            Console.Write("{0:X2}", Nes.Ppu.MaskRegister);
            Console.SetCursorPosition(41, 3);
            Console.Write("{0:X2}", Nes.Ppu.StatusRegister);
            Console.SetCursorPosition(57, 3);
            Console.Write("{0:D3}", Nes.Ppu.CurrentScanline);

            // Test mode line
            if (TEST_MODE)
            {
                Console.SetCursorPosition(18, 4);
                Console.Write("{0:X2}    {1:X2} {2:X2} {3:X2}",
                    Nes.Cpu.Bus.ReadByte(0x6000, true),
                    Nes.Cpu.Bus.ReadByte(0x6001, true),
                    Nes.Cpu.Bus.ReadByte(0x6002, true),
                    Nes.Cpu.Bus.ReadByte(0x6003, true)
                );

                Console.SetCursorPosition(33, 4);
                var testStrBytes = new byte[128];
                for (var i = 0; i < testStrBytes.Length; i++)
                {
                    var strb = Nes.Cpu.Bus.ReadByte((ushort)(0x6004 + i));
                    if (strb == 0x00)
                    {
                        break;
                    }
                    testStrBytes[i] = strb;
                }
                var testStr = Encoding.ASCII.GetChars(testStrBytes);
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

            this.Ticks++;

            if (Nes.Debugger.StepMode)
            {
                Start();
            }
        }

        private void WriteWide(int line, string message)
        {
            Console.SetCursorPosition(0, line);
            Console.Write(message.PadRight(Console.WindowWidth));
        }
    }
}