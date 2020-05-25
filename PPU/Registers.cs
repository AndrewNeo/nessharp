using System;
using NesSharp.Utils;

namespace NesSharp.PPU
{
    struct ControlRegisterValues
    {
        public ControlRegisterValues(byte[] registers, byte index)
        {
            this.registers = registers;
            this.index = index;
        }

        private readonly byte[] registers;
        private readonly byte index;

        public byte Nametable
        {
            get
            {
                return (byte)(registers[index] & 0x3);
            }
            set
            {
                registers[index] = (byte)(registers[index] | (value & 0x3));
            }
        }

        public bool AddressIncrement
        {
            get
            {
                return FastBits.Get(registers[index])[2];
            }
            set
            {
                FastWrite(2, value);
            }
        }

        public bool SpritePatternTable
        {
            get
            {
                return FastBits.Get(registers[index])[3];
            }
            set
            {
                FastWrite(3, value);
            }
        }

        public bool BackgroundPatternTable
        {
            get
            {
                return FastBits.Get(registers[index])[4];
            }
            set
            {
                FastWrite(4, value);
            }
        }

        public bool SpriteSize
        {
            get
            {
                return FastBits.Get(registers[index])[5];
            }
            set
            {
                FastWrite(5, value);
            }
        }

        public bool PpuSlaveSelect
        {
            get
            {
                return FastBits.Get(registers[index])[6];
            }
            set
            {
                FastWrite(6, value);
            }
        }

        public bool EnableNMI
        {
            get
            {
                return FastBits.Get(registers[index])[7];
            }
            set
            {
                FastWrite(7, value);
            }
        }

        private void FastWrite(byte i, bool value)
        {
            var updated = FastBits.Get(registers[index]);
            updated[i] = value;
            registers[index] = FastBits.Write(updated);
        }
    }

    struct MaskRegisterValues
    {
        public MaskRegisterValues(byte[] registers, byte index)
        {
            this.registers = registers;
            this.index = index;
        }

        private readonly byte[] registers;
        private readonly byte index;

        public bool Greyscale
        {
            get
            {
                return FastBits.Get(registers[index])[0];
            }
            set
            {
                FastWrite(0, value);
            }
        }

        public bool ShowBackgroundLeft
        {
            get
            {
                return FastBits.Get(registers[index])[1];
            }
            set
            {
                FastWrite(1, value);
            }
        }

        public bool ShowSpritesLeft
        {
            get
            {
                return FastBits.Get(registers[index])[2];
            }
            set
            {
                FastWrite(2, value);
            }
        }

        public bool ShowBackground
        {
            get
            {
                return FastBits.Get(registers[index])[3];
            }
            set
            {
                FastWrite(3, value);
            }
        }

        public bool ShowSprites
        {
            get
            {
                return FastBits.Get(registers[index])[4];
            }
            set
            {
                FastWrite(4, value);
            }
        }

        public bool BurstRed
        {
            get
            {
                return FastBits.Get(registers[index])[5];
            }
            set
            {
                FastWrite(5, value);
            }
        }

        public bool BurstGreen
        {
            get
            {
                return FastBits.Get(registers[index])[6];
            }
            set
            {
                FastWrite(6, value);
            }
        }

        public bool BurstBlue
        {
            get
            {
                return FastBits.Get(registers[index])[7];
            }
            set
            {
                FastWrite(7, value);
            }
        }

        private void FastWrite(byte i, bool value)
        {
            var updated = FastBits.Get(registers[index]);
            updated[i] = value;
            registers[index] = FastBits.Write(updated);
        }
    }

    struct StatusRegisterValues
    {
        public StatusRegisterValues(byte[] registers, byte index)
        {
            this.registers = registers;
            this.index = index;
        }

        private readonly byte[] registers;
        private readonly byte index;

        public bool SpriteOverflow
        {
            get
            {
                return FastBits.Get(registers[index])[5];
            }
            set
            {
                FastWrite(5, value);
            }
        }

        public bool Sprite0Hit
        {
            get
            {
                return FastBits.Get(registers[index])[6];
            }
            set
            {
                FastWrite(6, value);
            }
        }

        public bool InVblank
        {
            get
            {
                return FastBits.Get(registers[index])[7];
            }
            set
            {
                FastWrite(7, value);
            }
        }

        private void FastWrite(byte i, bool value)
        {
            var updated = FastBits.Get(registers[index]);
            updated[i] = value;
            registers[index] = FastBits.Write(updated);
        }
    }
}