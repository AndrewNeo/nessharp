using System;

namespace NesSharp
{
    public class NesCart
    {
        public byte[] Rom { get; set; }
        public byte[] Sram { get; set; }
        public byte[] ExpansionArea { get; set; }
    }
}