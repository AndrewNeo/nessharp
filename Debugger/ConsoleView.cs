using System;
using System.Diagnostics;
using System.IO;

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
        private int LastLine { get { return Console.WindowHeight - 2; } }

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
            WriteWide(1, "Tick: xxxxxx    Last executed command: 0xXX    Memory at 0x6000: 0xXXXX    Mapper ID: XX    PC: XXXX    FPS: XXX");
            WriteWide(2, " === Console output ===");

            Update();
        }

        public void Update()
        {
            if (TickTrackTimer.ElapsedMilliseconds > 1000)
            {
                FpsCounter = (uint)((Ticks - LastFrameTickCount) / (TickTrackTimer.ElapsedMilliseconds / 1000));
                TickTrackTimer.Restart();
                LastFrameTickCount = Ticks;
            }

            if (!AttachedToConsole) return;

            Console.SetCursorPosition(6, 1);
            Console.Write("{0:D6}", this.Ticks);
            Console.SetCursorPosition(41, 1);
            Console.Write("{0:X2}", Nes.Debugger.LastOpcode);
            Console.SetCursorPosition(67, 1);
            Console.Write("{0:X4}", Nes.Cpu.Bus.ReadByte(0x6000, true));
            Console.SetCursorPosition(86, 1);
            Console.Write("{0:D2}", Nes.Cart.Header.MapperNumber);
            Console.SetCursorPosition(96, 1);
            Console.Write("{0:X4}", Nes.Cpu.PublicCpuState.PC);
            Console.SetCursorPosition(109, 1);
            Console.Write("{0:D3}", this.FpsCounter);

            // Move back to normal write window
            Console.SetCursorPosition(0, 3);
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
            else
            {
                Update();
            }
        }

        public uint FpsCounter { get; private set; }
        private uint LastFrameTickCount;

        public void Log(string component, string message, params object[] parm)
        {
            if (AttachedToConsole && !Nes.Debugger.StepMode)
            {
                Console.SetCursorPosition(0, 3);
            }

            Console.Write($"DEBUGGER [{component}]: ");
            Console.WriteLine(message, parm);
        }

        private void WriteWide(int line, string message)
        {
            Console.SetCursorPosition(0, line);
            Console.Write(message.PadRight(Console.WindowWidth));
        }
    }
}