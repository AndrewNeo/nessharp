using System;
using NesSharp.Utils;

namespace NesSharp.PPU
{
    // Pretty much entirely ported from https://github.com/AndreaOrru/LaiNES/blob/master/src/ppu.cpp
    public class NesPpu : IResettable
    {
        protected readonly Nes Nes;
        public readonly NesPpuAddressBus Bus;
        internal readonly NesPpuMemory Memory;
        internal event ImageFrame NewImageBufferFrame;

        public readonly uint[] ImageBuffer;
        private readonly Sprite[] OAM1, OAM2;

        public NesPpu(Nes nes)
        {
            this.Nes = nes;
            this.Bus = new NesPpuAddressBus(nes);
            this.Memory = new NesPpuMemory(nes);

            this.ImageBuffer = new uint[NesConsts.IMAGE_BUFFER_SIZE];
            this.OAM1 = new Sprite[8];
            this.OAM2 = new Sprite[8];

            this.Control = new ControlRegisterValues(0);
            this.Mask = new MaskRegisterValues(0);
            this.Status = new StatusRegisterValues(0);
        }

        public void Update()
        {
            switch (CurrentScanline)
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

            CurrentDot++;
            if (CurrentDot > 340)
            {
                CurrentDot %= 341;
                CurrentScanline++;
                if (CurrentScanline > 261)
                {
                    CurrentScanline = 0;
                    isFrameOdd = !isFrameOdd;
                }
            }
        }

        // Internal state

        private MirrorMode mirrorMode;
        public ushort CurrentScanline { get; private set; }
        public ushort CurrentDot { get; private set; }
        private bool isFrameOdd;
        private byte nameTable, attrTable, backgroundLow, backgroundHigh;
        private byte attrTableShiftLow, attrTableShiftHigh;
        private ushort backgroundShiftLow, backgroundShiftHigh;
        private bool attrTableLatchLow, attrTableLatchHigh;
        private byte fineXScroll;

        private VramAddress currentAddress, temporaryAddress;

        // Convienience flags

        private bool Rendering { get { return Mask.ShowBackground || Mask.ShowSprites; } }
        private int SpriteHeight { get { return Control.SpriteSize ? 16 : 8; } }
        private ushort NametableAddress { get { return (ushort)(0x2000 | (currentAddress.Full & 0xFFF)); } }
        private ushort AttrTableAddress
        {
            get
            {
                return (ushort)(
                    0x23C0 |
                    (currentAddress.NametableSelect << 10) |
                    ((currentAddress.CoarseYScroll / 4) << 3) |
                    (currentAddress.CoarseXScroll / 4)
                );
            }
        }
        private ushort BackgroundAddress
        {
            get
            {
                return (ushort)(
                    (Control.BackgroundPatternTable ? 0x1000 : 0x0) +
                    (nameTable * 16) +
                    currentAddress.FineYScroll
                );
            }
        }

        // Internal registers

        private ControlRegisterValues Control;
        public byte ControlRegister { get { return (byte)Control; } }
        private MaskRegisterValues Mask;
        public byte MaskRegister { get { return (byte)Mask; } }
        private StatusRegisterValues Status;
        public byte StatusRegister { get { return (byte)Status; } }

        // CPU-PPU memory mapping zone

        private byte ExtMemResult;
        private byte ExtMemBuffer;
        private bool ExtMemLatch;
        private byte ExtMemOAMAddress;

        public byte Read(ushort address)
        {
            if (address >= 8)
            {
                throw new Exception("Tried reading out of range from PPU");
            }

            if (address == 2)
            {
                // PPUSTATUS ($2002)
                ExtMemResult = (byte)((ExtMemResult & 0x1F) | StatusRegister);
                Status.InVblank = false;
                ExtMemLatch = false;
            }
            else if (address == 4)
            {
                // OAMDATA ($2004)
                ExtMemResult = Memory.OAMram[ExtMemOAMAddress];
            }
            else if (address == 7)
            {
                // PPUDATA ($2007)
                if (currentAddress.Address <= 0x3EFF)
                {
                    ExtMemResult = ExtMemBuffer;
                    ExtMemBuffer = Bus.ReadByte(currentAddress.Address);
                }
                else
                {
                    ExtMemResult = Bus.ReadByte(currentAddress.Address);
                    ExtMemBuffer = ExtMemResult;
                }

                currentAddress.Address += (ushort)(Control.AddressIncrement ? 32 : 1);
            }

            return ExtMemResult;
        }

        public void Write(ushort address, byte value)
        {
            if (address >= 8)
            {
                throw new Exception("Tried writing out of range from PPU");
            }

            ExtMemResult = value;

            if (address == 0)
            {
                // PPUCTRL ($2000)
                Control = (ControlRegisterValues)value;
                temporaryAddress.NametableSelect = Control.Nametable;
            }
            else if (address == 1)
            {
                // PPUMASK ($2001)
                Mask = (MaskRegisterValues)value;
            }
            else if (address == 3)
            {
                // OAMADDR ($2003)
                ExtMemOAMAddress = value;
            }
            else if (address == 4)
            {
                // OAMDATA ($2004)
                Memory.OAMram[ExtMemOAMAddress++] = value;
            }
            else if (address == 5)
            {
                // PPUSCROLL ($2005)
                if (!ExtMemLatch)
                {
                    fineXScroll = (byte)(value & 7);
                    temporaryAddress.CoarseXScroll = (byte)(value >> 3);
                }
                else
                {
                    temporaryAddress.FineYScroll = (byte)(value & 7);
                    temporaryAddress.CoarseYScroll = (byte)(value >> 3);
                }
            }
            else if (address == 6)
            {
                // PPUADDR ($2006)
                if (!ExtMemLatch)
                {
                    temporaryAddress.High = (byte)(value & 0x3F);
                }
                else
                {
                    temporaryAddress.Low = value;
                    currentAddress.Full = temporaryAddress.Full;
                }

                ExtMemLatch = !ExtMemLatch;
            }
            else if (address == 7)
            {
                // PPUDATA ($2007)
                Bus.Write(currentAddress.Address, value);
                currentAddress.Address += (ushort)(Control.AddressIncrement ? 32 : 1);
            }
        }

        // Mirroring

        public MirrorMode MirrorMode { get; set; }

        // Rendering

        private void HorizontalScroll()
        {
            if (!Rendering)
            {
                return;
            }

            if (currentAddress.CoarseXScroll == 31)
            {
                currentAddress.Full ^= 0x41F;
            }
            else
            {
                currentAddress.CoarseXScroll++;
            }
        }

        private void VerticalScroll()
        {
            if (!Rendering)
            {
                return;
            }

            if (currentAddress.FineYScroll < 7)
            {
                currentAddress.FineYScroll++;
            }
            else
            {
                currentAddress.FineYScroll = 0;
                if (currentAddress.CoarseYScroll == 31)
                {
                    currentAddress.CoarseYScroll = 0;
                }
                else if (currentAddress.CoarseYScroll == 29)
                {
                    currentAddress.CoarseYScroll = 0;
                    currentAddress.NametableSelect ^= 0x2;
                }
                else
                {
                    currentAddress.CoarseYScroll++;
                }
            }
        }

        private void HorizontalUpdate()
        {
            if (!Rendering)
            {
                return;
            }

            currentAddress.Full = (ushort)((currentAddress.Full & ~0x041F) | (temporaryAddress.Full & 0x041F));
        }

        private void VerticalUpdate()
        {
            if (!Rendering)
            {
                return;
            }

            currentAddress.Full = (ushort)((currentAddress.Full & ~0x7BE0) | (temporaryAddress.Full & 0x7BE0));
        }

        private void ReloadShiftRegister()
        {
            backgroundShiftLow = (ushort)((backgroundShiftLow & 0xFF00) | backgroundLow);
            backgroundShiftHigh = (ushort)((backgroundShiftHigh & 0xFF00) | backgroundHigh);

            attrTableLatchLow = (attrTable & 1) > 0;
            attrTableLatchHigh = (attrTable & 2) > 0;
        }

        private void ClearOAM2()
        {
            for (byte i = 0; i < 8; i++)
            {
                OAM2[i].Clear();
            }
        }

        private void PrepareSprites()
        {
            byte n = 0;
            for (byte i = 0; i < 64; i++)
            {
                var line = (CurrentScanline == 261 ? -1 : CurrentScanline) - Memory.OAMram[i * 4 + 0];

                // Copy sprites in scanline to OAM2
                if (line >= 0 && line < SpriteHeight)
                {
                    OAM2[n].ID = i;
                    OAM2[n].Y = Memory.OAMram[i * 4 + 0];
                    OAM2[n].Tile = Memory.OAMram[i * 4 + 1];
                    OAM2[n].Attribute = Memory.OAMram[i * 4 + 2];
                    OAM2[n].X = Memory.OAMram[i * 4 + 3];

                    n++;
                    if (n >= 8)
                    {
                        Status.SpriteOverflow = true;
                        break;
                    }
                }
            }
        }

        private void LoadSprites()
        {
            ushort address;
            for (byte i = 0; i < 8; i++)
            {
                // Copy sprite from OAM2 to OAM1
                OAM1[i] = OAM2[i];

                // Handle address modes based on sprite height
                if (SpriteHeight == 16)
                {
                    address = (ushort)(((OAM1[i].Tile & 1) * 0x1000) + ((OAM1[i].Tile & ~1) * 16));
                }
                else
                {
                    address = (ushort)((Control.SpritePatternTable ? 0x1000 : 0x0) + (OAM1[1].Tile & 16));
                }

                ushort spriteY = (ushort)((CurrentScanline - OAM1[i].Y) % SpriteHeight);

                if ((OAM1[i].Attribute & 0x80) > 0)
                {
                    spriteY ^= (ushort)(SpriteHeight - 1);
                }

                address += (ushort)(spriteY + (spriteY & 8));

                OAM1[i].DataLow = Bus.ReadByte(address);
                OAM1[i].DataHigh = Bus.ReadByte((ushort)(address + 8));
            }
        }

        private ushort NthBit(ushort b, int pos)
        {
            return (byte)((b >> pos) & 1);
        }

        private void DrawPixel()
        {
            byte palette = 0;
            byte objectPalette = 0;
            bool objectPriority = false;
            int x = CurrentDot - 2;

            if (CurrentScanline < 240 && x >= 0 && x < 256)
            {
                // Render background
                if (Mask.ShowBackground && !(!Mask.ShowBackgroundLeft && x < 8))
                {
                    palette = (byte)(
                        (NthBit(backgroundShiftHigh, 15 - fineXScroll) << 1) |
                        NthBit(backgroundShiftLow, 15 - fineXScroll)
                    );

                    if (palette > 0)
                    {
                        palette |= (byte)((
                            (NthBit(attrTableShiftHigh, 7 - fineXScroll) << 1) |
                            NthBit(attrTableShiftLow, 7 - fineXScroll)
                        ) << 2);
                    }
                }

                // Render sprites
                if (Mask.ShowSprites && !(!Mask.ShowSpritesLeft && x < 8))
                {
                    for (byte i = 7; i >= 0; i--)
                    {
                        if (OAM1[i].ID == 64)
                        {
                            // Unset sprite
                            continue;
                        }

                        int spriteX = x - OAM1[i].X;
                        if (spriteX >= 8)
                        {
                            // Out of rendering range
                            continue;
                        }

                        if ((OAM1[i].Attribute & 0x40) > 0)
                        {
                            // Horizontal flip
                            spriteX ^= 7;
                        }

                        byte spritePalette = (byte)(
                            (NthBit(OAM1[i].DataHigh, 7 - spriteX) << 1) |
                            NthBit(OAM1[i].DataLow, 7 - spriteX)
                        );
                        if (spritePalette == 0)
                        {
                            // Transparent
                            continue;
                        }

                        if (OAM1[i].ID == 0 && palette > 0 && x != 255)
                        {
                            // Sprite hit
                            Status.Sprite0Hit = true;
                        }

                        spritePalette |= (byte)((OAM1[i].Attribute & 0x3) << 2);
                        objectPalette = (byte)(spritePalette + 16);
                        objectPriority = (OAM1[i].Attribute & 0x20) > 0;
                    }

                    // Evaluate priority
                    if (objectPalette > 0 && (palette == 0 || !objectPriority))
                    {
                        palette = objectPalette;
                    }

                    byte color = Bus.ReadByte((ushort)(0x3F00 + (Rendering ? palette : 0)));
                    ImageBuffer[CurrentScanline * 256 + x] = PpuConsts.NES_RGB[color];
                }
            }

            // Do background shifts
            backgroundLow <<= 1;
            backgroundHigh <<= 1;
            attrTableShiftLow = (byte)((attrTableShiftLow << 1) | (attrTableLatchLow ? 1 : 0));
            attrTableShiftHigh = (byte)((attrTableShiftHigh << 1) | (attrTableLatchHigh ? 1 : 0));
        }

        private void RenderScanline(ScanlineBand band)
        {
            ushort addr = 0x00;

            if (band == ScanlineBand.NMI && CurrentDot == 1)
            {
                Status.InVblank = true;
                if (Control.EnableNMI)
                {
                    Nes.Cpu.ActiveNmi = true;
                }
            }
            else if (band == ScanlineBand.AfterVisible && CurrentDot == 0)
            {
                if (NewImageBufferFrame != null)
                {
                    NewImageBufferFrame.Invoke();
                }
            }
            else if (band == ScanlineBand.Visible || band == ScanlineBand.PreNext)
            {
                // Handle sprites
                switch (CurrentDot)
                {
                    case 1:
                        ClearOAM2();
                        if (band == ScanlineBand.PreNext)
                        {
                            Status.SpriteOverflow = false;
                            Status.Sprite0Hit = false;
                        }
                        break;
                    case 257:
                        PrepareSprites();
                        break;
                    case 321:
                        LoadSprites();
                        break;
                }

                // Handle background
                switch (CurrentDot)
                {
                    case 1:
                        addr = NametableAddress;
                        if (band == ScanlineBand.PreNext)
                        {
                            Status.InVblank = false;
                            break;
                        }
                        break;
                    case ushort n when ((n >= 2 && n <= 255) || ((n >= 322 && n <= 337))):
                        DrawPixel();
                        switch (CurrentDot)
                        {
                            // Nametable
                            case 1:
                                addr = NametableAddress;
                                ReloadShiftRegister();
                                break;
                            case 2:
                                nameTable = Bus.ReadByte(addr);
                                break;
                            // Attribute
                            case 3:
                                addr = AttrTableAddress;
                                break;
                            case 4:
                                attrTable = Bus.ReadByte(addr);
                                if ((currentAddress.CoarseYScroll & 2) > 0)
                                {
                                    attrTable >>= 4;
                                }
                                if ((currentAddress.CoarseXScroll & 2) > 0)
                                {
                                    attrTable >>= 2;
                                }
                                break;
                            // Background (L)
                            case 5:
                                addr = BackgroundAddress;
                                break;
                            case 6:
                                backgroundLow = Bus.ReadByte(addr);
                                break;
                            // Background (H)
                            case 7:
                                addr += 8;
                                break;
                            case 8:
                                backgroundHigh = Bus.ReadByte(addr);
                                HorizontalScroll();
                                break;

                        }
                        break;
                    // Vertical scroll handling
                    case 256:
                        DrawPixel();
                        backgroundHigh = Bus.ReadByte(addr);
                        VerticalScroll();
                        break;
                    // Update horizontal position
                    case 257:
                        DrawPixel();
                        ReloadShiftRegister();
                        HorizontalUpdate();
                        break;
                    // Update vertical position
                    case ushort n when (n >= 280 && n <= 304):
                        if (band == ScanlineBand.PreNext)
                        {
                            VerticalUpdate();
                        }
                        break;
                    case 231:
                    case 339:
                        addr = NametableAddress;
                        break;
                    case 338:
                        nameTable = Bus.ReadByte(addr);
                        break;
                    case 340:
                        nameTable = Bus.ReadByte(addr);
                        if (band == ScanlineBand.PreNext && Rendering && isFrameOdd)
                        {
                            CurrentDot++;
                        }
                        break;
                }

                // Trigger scanline in mapper
                if (CurrentDot == 260 && Rendering)
                {
                    this.Nes.Cart.Mapper.Scanline();
                }
            }
        }

        public void SoftReset()
        {
            Control = new ControlRegisterValues(0);
            Mask = new MaskRegisterValues(0);
            Status = new StatusRegisterValues(0);

            isFrameOdd = false;

            Memory.SoftReset();
        }

        public void HardReset()
        {
            Control = new ControlRegisterValues(0);
            Mask = new MaskRegisterValues(0);
            Status = new StatusRegisterValues(0);

            FastBits.Clear(ImageBuffer);

            isFrameOdd = false;
            CurrentScanline = 0;
            CurrentDot = 0;

            Memory.HardReset();
        }
    }
}