using System;

namespace NesSharp
{
    class Nes
    {
        public Nes()
        {
            Cpu = new NesCpu(this);
            Ram = new NesRam(this);
        }

        public event Action Clock;

        public NesCpu Cpu { get; protected set; }

        public NesRam Ram { get; protected set; }

        public NesCart Cart { get; protected set; }
    }
}
