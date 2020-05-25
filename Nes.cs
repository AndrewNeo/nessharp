using System;
using NesSharp.Cart;
using NesSharp.CPU;
using NesSharp.Memory;
using NesSharp.PPU;

namespace NesSharp
{
    public class Nes
    {
        public Nes()
        {
            Cpu = new NesCpu(this);
            Ppu = new NesPpu(this);
            Debugger = new NesDebugger(this);
        }

        public NesCpu Cpu { get; private set; }

        public NesPpu Ppu { get; private set; }

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
