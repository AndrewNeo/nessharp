using System;

namespace NesSharp
{
    public class IllegalMemoryAccessException : Exception
    {
        public IllegalMemoryAccessException(AddressBus bus, ushort address, string message = null)
            : base(string.Format("Illegal {0} memory access at address 0x{1:x}" + (message != null ? ": " + message : ""), bus, address))
        {
            this.Address = address;
        }

        public AddressBus Bus { get; private set; }
        public ushort Address { get; private set; }
    }

    public class UnderhandledMemoryException : Exception
    {
        public UnderhandledMemoryException(AddressBus bus, ushort address, string message = null)
          : base(string.Format("Underhandled {0} memory access at address 0x{1:x}" + (message != null ? ": " + message : ""), bus, address))
        {
            this.Address = address;
        }

        public AddressBus Bus { get; private set; }
        public ushort Address { get; private set; }
    }
}