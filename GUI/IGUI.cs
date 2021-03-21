using System;

namespace NesSharp.GUI
{
    public interface IGUI : IDisposable {
        void Init();
        void Spin();
    }
}