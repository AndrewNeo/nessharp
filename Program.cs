using System;

namespace NesSharp
{
    class Program
    {
        static void Main(string[] args)
        {
            Nes nes = new Nes();
            nes.LoadCartFromFile("Super Mario Bros 3 (U) (PRG 1) [h2].nes");
            nes.Start();
        }
    }
}
