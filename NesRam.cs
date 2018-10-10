using System;
using System.Collections;
using System.Collections.Generic;

namespace NesSharp
{
    class NesRam : IResettable
    {
        public NesRam(Nes nes)
        {
            this.Nes = nes;
            Reset();
        }

        private Nes Nes { get; set; }

        public byte ReadByte(ushort i)
        {
            var (mem, offset) = MapMemory(i);
            return mem[i - offset];
        }

        public ushort ReadUShort(ushort i)
        {
            var (mem, offset) = MapMemory(i);
            return BitConverter.ToUInt16(mem, i - offset);
        }

        public void Write(ushort i, byte d)
        {
            var (mem, offset) = MapMemory(i);
            mem[i - offset] = d;
        }

        public void Write(ushort i, ushort d)
        {
            var (mem, offset) = MapMemory(i);
            var rb = BitConverter.GetBytes(d);
            var osa = i - offset;
            mem[osa] = rb[0];
            mem[osa + 1] = rb[1];
        }

        private byte[] BidirectionalIO { get; set; }
        private byte[] WorkingRam { get; set; }
        private byte[] InternalPpuRegisters { get; set; }
        private byte[] InternalApuRegisters { get; set; }

        public void Reset()
        {
            BidirectionalIO = new byte[2];
            WorkingRam = new byte[2 * 1024];
            InternalPpuRegisters = new byte[8];
            InternalApuRegisters = new byte[18];
        }

        private (byte[], ushort) MapMemory(ushort pos)
        {
            if (pos >= 0x0000 && pos <= 0x0001)
            {
                return (BidirectionalIO, 0);
            }
            else if (pos >= 0x0002 && pos <= 0x07FF)
            {
                return (WorkingRam, 0);
            }
            else if (pos >= 0x0800 && pos <= 0x1FFF)
            {
                return (WorkingRam, 0x0800);
            }
            else if (pos >= 0x2000 && pos <= 0x2007)
            {
                return (InternalPpuRegisters, 0x2000);
            }
            else if (pos >= 0x2008 && pos <= 0x3FFF)
            { // mirrored? how exactly. divide to find range somehow?
                return (InternalPpuRegisters, 0x2008);
            }
            else if (pos >= 0x4018 && pos <= 0x5FFF)
            {
                return (Nes.Cart.ExpansionArea, 0x4018);
            }
            else if (pos >= 0x6000 && pos <= 0x7FFF)
            {
                return (Nes.Cart.Sram, 0x6000);
            }
            else if (pos >= 0x8000 && pos <= 0xFFFF)
            {
                return (Nes.Cart.Rom, 0x8000);
            }

            return (new byte[0], 0);
        }
    }
}