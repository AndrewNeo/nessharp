using System;

namespace NesSharp.CPU
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    class OpcodeAttribute : Attribute
    {
        public OpcodeAttribute(
            byte opcode,
            string name,
            byte cost,
            AddressingMode mode = AddressingMode.Implicit
        )
        {
            this.Opcode = opcode;
            this.Name = name;
            this.Mode = mode;
            this.Cost = cost;
        }

        public byte Opcode { get; private set; }
        public string Name { get; private set; }
        public AddressingMode Mode { get; private set; }
        public byte Cost { get; private set; }
    }
}