using System;

namespace NesSharp
{
    class Nes
    {
        public Nes()
        {
            Cpu = new NesCpu(this);
            Ram = new NesRam(this);
            Cart = new NesCart();
        }

        public event Action Clock;

        public NesCpu Cpu;

        public NesRam Ram;

        public NesCart Cart;
    }
}
