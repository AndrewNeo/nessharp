using System;
using NesSharp.Utils;

namespace NesSharp.PPU
{
    public class NesPpu : IResettable
    {
        protected Nes Nes;
        private NesPpuAddressBus Bus;
        public NesPpuMemory Memory { get; private set; }
        private uint[] imageBuffer;
        private ushort currentScanline;
        private ushort currentDot;
        private bool isFrameOdd;

        public NesPpu(Nes nes)
        {
            this.Nes = nes;
            this.Bus = new NesPpuAddressBus(nes);
            this.Memory = new NesPpuMemory();
        }

        public void Update()
        {
            switch (currentScanline)
            {
                case ushort n when (n >= 0 && n <= 239):
                    RenderScanline(ScanlineBand.Visible);
                    break;
                case 240:
                    RenderScanline(ScanlineBand.AfterVisible);
                    break;
                case 241:
                    RenderScanline(ScanlineBand.NMI);
                    break;
                case 261:
                    RenderScanline(ScanlineBand.PreNext);
                    break;
            }

            currentDot++;
            if (currentDot > 340)
            {
                currentDot %= 341;
                currentScanline++;
                if (currentScanline > 261)
                {
                    currentScanline = 0;
                    isFrameOdd = !isFrameOdd;
                }
            }
        }

        // Internal registers

        public byte[] Registers { get; private set; }

        private byte ControlRegister
        {
            get { return Registers[0]; }
            set { Registers[0] = value; }
        }

        private ControlRegisterValues Control
        {
            get
            {
                return new ControlRegisterValues(Registers[0]);
            }
        }

        private byte MaskRegister
        {
            get { return Registers[1]; }
            set { Registers[1] = value; }
        }

        private MaskRegisterValues Mask
        {
            get
            {
                return new MaskRegisterValues(Registers[2]);
            }
        }

        private byte StatusRegister { get { return Registers[2]; } }

        public byte Read(ushort address)
        {
            if (address > 8)
            {
                throw new Exception("Tried reading out of range from PPU");
            }

            return Registers[address];
        }

        public void Write(ushort address, byte value)
        {
            if (address > 8)
            {
                throw new Exception("Tried reading out of range from PPU");
            }

            Registers[address] = value;
        }

        // Convienience flags

        private bool Rendering { get { return Mask.ShowBackground || Mask.ShowSprites; } }
        private int SpriteHeight { get { return Control.SpriteSize ? 16 : 8; } }

        private void RenderScanline(ScanlineBand band)
        {
            ushort addr;

            if (band == ScanlineBand.NMI && currentDot == 1)
            {

            }
            else if (band == ScanlineBand.AfterVisible && currentDot == 0)
            {
                // Draw frame buffer to GUI
            }
            else if (band == ScanlineBand.Visible || band == ScanlineBand.PreNext)
            {
                // Handle spires
                switch (currentDot)
                {

                }

                // Handle background
                switch (currentDot)
                {

                }

                // Trigger scanline in mapper
                if (currentDot == 260 && Rendering)
                {
                    this.Nes.Cart.Mapper.Scanline();
                }
            }
        }

        public void SoftReset()
        {
            Registers[0] = 0x0;
            Registers[1] = 0x0;
            Registers[2] = 0x0;

            Memory.SoftReset();
        }

        public void HardReset()
        {
            Registers = new byte[8];

            Memory.HardReset();

            isFrameOdd = false;
            currentScanline = 0;
            currentDot = 0;

            imageBuffer = new uint[NesConsts.IMAGE_BUFFER_SIZE];
        }
    }

    enum ScanlineBand
    {
        Visible,
        AfterVisible,
        NMI,
        PreNext
    }

    ref struct ControlRegisterValues
    {
        public ControlRegisterValues(byte register)
        {
            var fb = FastBits.Get(register);
            Nametable = (byte)(register & 0x3);
            AddressIncrement = fb[2];
            SpritePatternTable = fb[3];
            BackgroundPatternTable = fb[4];
            SpriteSize = fb[5];
            PpuSlaveSelect = fb[6];
            EnableNMI = fb[7];
        }

        public byte Nametable;
        public bool AddressIncrement;
        public bool SpritePatternTable;
        public bool BackgroundPatternTable;
        public bool SpriteSize;
        public bool PpuSlaveSelect;
        public bool EnableNMI;
    }

    ref struct MaskRegisterValues
    {
        public MaskRegisterValues(byte register)
        {
            var fb = FastBits.Get(register);
            Greyscale = fb[0];
            ShowBackgroundLeft = fb[1];
            ShowSpritesLeft = fb[2];
            ShowBackground = fb[3];
            ShowSprites = fb[4];
            BurstRed = fb[5];
            BurstGreen = fb[6];
            BurstBlue = fb[7];
        }

        public bool Greyscale;
        public bool ShowBackgroundLeft;
        public bool ShowSpritesLeft;
        public bool ShowBackground;
        public bool ShowSprites;
        public bool BurstRed;
        public bool BurstGreen;
        public bool BurstBlue;
    }

    ref struct StatusRegisterValues
    {
        public StatusRegisterValues(byte register)
        {
            var fb = FastBits.Get(register);
            SpriteOverflow = fb[5];
            Sprite0Hit = fb[6];
            InVblank = fb[7];
        }

        public bool SpriteOverflow;
        public bool Sprite0Hit;
        public bool InVblank;
    }
}