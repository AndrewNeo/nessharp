using System;

namespace NesSharp.GUI
{
    public class GuiInterface
    {
        private Nes Nes;

        public GuiInterface(Nes nes)
        {
            this.Nes = nes;
        }

        public void Init()
        {
            this.Nes.Ppu.NewImageBufferFrame += OnNewFrame;
        }

        private void OnNewFrame(uint[] imageBuffer)
        {
            Console.WriteLine("Got new image buffer frame");
        }
    }
}