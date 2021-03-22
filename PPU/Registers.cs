using System;
using NesSharp.Utils;

namespace NesSharp.PPU
{
    public struct ControlRegisterValues
    {
        public ControlRegisterValues(byte value)
        {
            this.registerValue = value;
        }

        private byte registerValue;

        public byte Nametable
        {
            get
            {
                return (byte)(registerValue & 0x3);
            }
            set
            {
                registerValue = (byte)(registerValue | (value & 0x3));
            }
        }

        public bool AddressIncrement
        {
            get
            {
                return FastRead(2);
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
                return FastRead(3);
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
                return FastRead(4);
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
                return FastRead(5);
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
                return FastRead(6);
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
                return FastRead(7);
            }
            set
            {
                FastWrite(7, value);
            }
        }

        private bool FastRead(byte i)
        {
            return FastBits.Get(registerValue)[7 - i];
        }

        private void FastWrite(byte i, bool value)
        {
            var updated = FastBits.Get(registerValue);
            updated[7 - i] = value;
            registerValue = FastBits.Write(updated);
        }

        public static implicit operator byte(ControlRegisterValues r) => r.registerValue;
        public static explicit operator ControlRegisterValues(byte v) => new ControlRegisterValues(v);
    }

    public struct MaskRegisterValues
    {
        public MaskRegisterValues(byte value)
        {
            this.registerValue = value;
        }

        private byte registerValue;

        public bool Greyscale
        {
            get
            {
                return FastRead(0);
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
                return FastRead(1);
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
                return FastRead(2);
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
                return FastRead(3);
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
                return FastRead(4);
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
                return FastRead(5);
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
                return FastRead(6);
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
                return FastRead(7);
            }
            set
            {
                FastWrite(7, value);
            }
        }

        private bool FastRead(byte i)
        {
            return FastBits.Get(registerValue)[i];
        }

        private void FastWrite(byte i, bool value)
        {
            var updated = FastBits.Get(registerValue);
            updated[i] = value;
            registerValue = FastBits.Write(updated);
        }

        public static implicit operator byte(MaskRegisterValues r) => r.registerValue;
        public static explicit operator MaskRegisterValues(byte v) => new MaskRegisterValues(v);
    }

    public struct StatusRegisterValues
    {
        public StatusRegisterValues(byte value)
        {
            this.registerValue = value;
        }

        private byte registerValue;

        public bool SpriteOverflow
        {
            get
            {
                return FastRead(5);
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
                return FastRead(6);
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
                return FastRead(7);
            }
            set
            {
                FastWrite(7, value);
            }
        }

        private bool FastRead(byte i)
        {
            return FastBits.Get(registerValue)[i];
        }

        private void FastWrite(byte i, bool value)
        {
            var updated = FastBits.Get(registerValue);
            updated[i] = value;
            registerValue = FastBits.Write(updated);
        }

        public static implicit operator byte(StatusRegisterValues r) => r.registerValue;
        public static explicit operator StatusRegisterValues(byte v) => new StatusRegisterValues(v);
    }
}