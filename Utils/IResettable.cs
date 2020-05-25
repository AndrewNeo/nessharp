using System;

namespace NesSharp.Utils
{
    interface IResettable
    {
        void SoftReset();
        void HardReset();
    }
}