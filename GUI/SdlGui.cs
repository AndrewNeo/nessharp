using System;
using static SDL2.SDL;
using NesSharp.Debugger;
using NesSharp.Utils;

namespace NesSharp.GUI
{
    public class SdlGui : IGUI
    {
        private static ushort Width = 500;
        private static ushort Height = 500;

        private Nes Nes;
        private IntPtr window;
        private IntPtr renderer;
        private IntPtr texture;
        private IntPtr background;

        public bool Initialized { get; private set; }
        private bool hasNewFrame = false;

        public SdlGui(Nes nes)
        {
            this.Nes = nes;
        }

        public void Init()
        {
            if (this.Initialized)
            {
                return;
            }

            if (SDL_Init(SDL_INIT_VIDEO) != 0)
            {
                throw new Exception("Couldn't initialize SDL: " + SDL_GetError());
            }

            SDL_SetHint(SDL_HINT_RENDER_SCALE_QUALITY, "linear");
            this.window = SDL_CreateWindow("NES#", SDL_WINDOWPOS_CENTERED, SDL_WINDOWPOS_CENTERED, Width, Height, 0);
            this.renderer = SDL_CreateRenderer(this.window, -1, SDL_RendererFlags.SDL_RENDERER_ACCELERATED | SDL_RendererFlags.SDL_RENDERER_PRESENTVSYNC);
            SDL_RenderSetLogicalSize(renderer, Width, Height);
            this.texture = SDL_CreateTexture(renderer, SDL_PIXELFORMAT_ARGB8888, (int)SDL_TextureAccess.SDL_TEXTUREACCESS_STREAMING, Width, Height);
            // var backSurface = IMG_Load("");
            // this.background = SDL_CreateTextureFromSurface(renderer, backSurface);
            // SDL_SetTextureColorMod(background, 60, 60, 60);
            // SDL_FreeSurface(backSurface);

            this.Nes.Ppu.NewImageBufferFrame += OnNewFrame;

            this.Initialized = true;
        }

        private void Render()
        {
            SDL_RenderClear(this.renderer);
            SDL_RenderCopy(renderer, texture, IntPtr.Zero, IntPtr.Zero);

            if (Nes.IsPaused)
            {
                // Show pause screen
            }

            SDL_RenderPresent(renderer);
        }

        public void Spin()
        {
            if (!this.Initialized)
            {
                return;
            }

            SDL_Event e = new SDL_Event();
            uint frameStart, frameTime;

            while (!Nes.IsStartingShutdown)
            {
                frameStart = SDL_GetTicks();

                while (SDL_PollEvent(out e) > 0)
                {
                    switch (e.type)
                    {
                        case SDL_EventType.SDL_QUIT:
                            Nes.Quit();
                            return;
                        case SDL_EventType.SDL_KEYDOWN:
                            Nes.Debugger.Log(NesDebugger.TAG_GUI, "Got key {0}", e.key);
                            break;
                    }
                }

                if (hasNewFrame)
                {
                    // This isn't working right now
                    // UpdateTexture();
                    hasNewFrame = false;
                }

                Render();

                frameTime = SDL_GetTicks() - frameStart;
                if (frameTime < NesConsts.FRAME_DELAY_MS)
                {
                    // SDL_Delay(NesConsts.FRAME_DELAY_MS - frameTime);
                    System.Threading.Thread.Sleep((int)(NesConsts.FRAME_DELAY_MS - frameTime));
                    Nes.Debugger.ConsoleView.Update();
                }
            }
        }

        public void Dispose()
        {
            SDL_DestroyTexture(this.texture);
            this.texture = IntPtr.Zero;
            SDL_DestroyRenderer(this.renderer);
            this.renderer = IntPtr.Zero;
            SDL_DestroyWindow(this.window);
            this.window = IntPtr.Zero;
        }

        private void OnNewFrame()
        {
            // This is coming from the PPU thread so we need to render it in ours
            hasNewFrame = true;
        }

        private unsafe void UpdateTexture()
        {
            fixed (uint* buf = Nes.Ppu.ImageBuffer)
            {
                SDL_UpdateTexture(this.texture, IntPtr.Zero, (IntPtr)buf, Width * sizeof(uint));
            }
        }
    }
}