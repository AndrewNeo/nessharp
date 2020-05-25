using System;

namespace NesSharp
{
    public class Nes
    {
        public Nes()
        {
            Cpu = new NesCpu(this);
            Ppu = new NesPpu(this);
            SharedMem = new NesSharedMemory();
            CpuMem = new NesCpuMemory(this);
            PpuMem = new NesPpuMemory(this);
            Debugger = new NesDebugger(this);
        }

        public NesCpu Cpu { get; private set; }

        public NesPpu Ppu { get; private set; }

        public NesSharedMemory SharedMem { get; private set; }

        public NesCpuMemory CpuMem { get; private set; }

        public NesPpuMemory PpuMem { get; private set; }

        public NesCart Cart { get; private set; }

        public NesDebugger Debugger { get; private set; }

        public void LoadCartFromFile(string filename)
        {
            this.Cart = NesCartBuilder.LoadFromFile(this, filename);
        }

        public void Start()
        {
            if (Cart.Header.FileId == null)
            {
                throw new Exception("No cart loaded");
            }

            this.SharedMem.HardReset();
            this.Cpu.HardReset();
            this.Ppu.HardReset();

            // Temporarily just run this far for now
            var i = 0;
            while (i < 50000)
            {
                if (!Clock())
                {
                    break;
                }
                i++;
            }
        }

        private bool Clock()
        {
            this.Ppu.Update();
            this.Ppu.Update();
            this.Ppu.Update();
            return this.Cpu.Update();
        }
    }
}
