using System;
using System.Diagnostics;
using System.Threading;
using NesSharp.Cart;
using NesSharp.CPU;
using NesSharp.Debugger;
using NesSharp.GUI;
using NesSharp.PPU;
using NesSharp.Utils;

namespace NesSharp
{
    public class Nes : IResettable, IDisposable
    {
        private Thread CpuThread;

        public Nes()
        {
            Thread.CurrentThread.Name = "NES# GUI";

            Cpu = new NesCpu(this);
            Ppu = new NesPpu(this);
            Debugger = new NesDebugger(this);
            Gui = new SdlGui(this);
        }

        public readonly NesCpu Cpu;

        public readonly NesPpu Ppu;

        public NesCart Cart { get; private set; }

        public readonly NesDebugger Debugger;

        public readonly IGUI Gui;

        public bool IsPaused { get; set; }
        public bool IsStartingShutdown { get; set; }

        public void InitGui()
        {
            Gui.Init();
        }

        public void LoadCartFromFile(string filename)
        {
            Cart = NesCartBuilder.LoadFromFile(this, filename);
        }

        public void Start(ushort overrideEntryPoint = 0x0, bool step = false, bool failOnInvalidOpcode = false)
        {
            this.Debugger.StepMode = step;
            this.Debugger.FailOnInvalidOpcode = failOnInvalidOpcode;

            this.Debugger.ConsoleView.Start();

            if (Cart.Header.FileId == null)
            {
                throw new Exception("No cart loaded");
            }

            if (overrideEntryPoint > 0)
            {
                Cpu.Debug_SetEntryPoint(overrideEntryPoint);
            }

            HardReset();

            CpuThread = new Thread(new ThreadStart(Spin));
            CpuThread.Name = "NES# CPU";

            CpuThread.Start();
            Gui.Spin();
        }

        public void SoftReset()
        {
            // Not yet supported
        }

        public void HardReset()
        {
            Cpu.HardReset();
            Ppu.HardReset();
        }

        public void Quit()
        {
            Debugger.Log(NesDebugger.TAG_SYS, "Shutdown requested");
            IsStartingShutdown = true;
        }

        public void Dispose()
        {
            Gui.Dispose();
        }

        private void Spin()
        {
            Debugger.Log(NesDebugger.TAG_SYS, "Starting system clock");
            var clock = Stopwatch.StartNew();

            while (!IsStartingShutdown)
            {
                if (!IsPaused)
                {
                    for (var i = 0; i < NesConsts.CYCLES_PER_FRAME; i++)
                    {
                        if (IsStartingShutdown) { break; }

                        this.Ppu.Update();
                        this.Ppu.Update();
                        this.Ppu.Update();
                        if (!this.Cpu.Update())
                        {
                            if (this.Debugger.FailOnInvalidOpcode)
                            {
                                Debugger.DumpAllMemory();
                                this.Debugger.ExecOpCode(this.Cpu.PublicCpuState.PC, this.Debugger.LastOpcode);
                                throw new Exception("CPU encountered invalid opcode");
                            }

                            Debugger.Log(NesDebugger.TAG_SYS, "CPU encountered invalid opcode");
                        }

                        this.Debugger.ConsoleView.Tick();
                        // this.Debugger.ConsoleView.Update();
                    }
                }

                if (IsStartingShutdown) { break; }

                /*if (clock.ElapsedMilliseconds < NesConsts.FRAME_DELAY_MS)
                {
                    Thread.Sleep((int)(NesConsts.FRAME_DELAY_MS - clock.ElapsedMilliseconds));
                }*/
                // TODO: Technically we should cap this at max clockspeed but we're not there yet

                clock.Restart();
            }

            Debugger.Log(NesDebugger.TAG_SYS, "System clock loop finished after " + this.Debugger.ConsoleView.Ticks + " ticks");
        }
    }
}
