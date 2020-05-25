using System;
using NesSharp.Memory;

namespace NesSharp.Cart
{
    [AttributeUsage(AttributeTargets.Class)]
    public class MapperAttribute : Attribute
    {
        public MapperAttribute(ushort mapperNumber)
        {
            this.MapperNumber = mapperNumber;
        }

        public ushort MapperNumber { get; private set; }
    }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public class MemoryMapAttribute : Attribute
    {
        public MemoryMapAttribute(AddressBus bus, ushort start, ushort end)
        {

        }

        public AddressBus Bus { get; private set; }
        public ushort Start { get; private set; }
        public ushort End { get; private set; }
    }
}
