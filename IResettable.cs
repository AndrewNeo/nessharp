using System;

namespace NesSharp
{
    interface IResettable
    {
        void SoftReset();
        void HardReset();
    }
}