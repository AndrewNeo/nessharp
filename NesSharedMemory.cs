using System;

namespace NesSharp
{
    public class NesSharedMemory : IResettable
    {
        // TODO: Move this out into APU and get rid of this class
        public byte[] InternalApuRegisters { get; private set; }

        public void SoftReset()
        {

            InternalApuRegisters[0x15] = 0x0;
        }

        public void HardReset()
        {
            InternalApuRegisters = new byte[18];
        }
    }
}
