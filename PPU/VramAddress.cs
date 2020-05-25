using System;
using System.Collections.Specialized;

namespace NesSharp.PPU
{
    struct VramAddress
    {
        private BitVector32 _vector;
        private readonly BitVector32.Section _full;
        private readonly BitVector32.Section _addr;
        private readonly BitVector32.Section _low;
        private readonly BitVector32.Section _high;
        private readonly BitVector32.Section _coarseXScroll;
        private readonly BitVector32.Section _coarseYScroll;
        private readonly BitVector32.Section _nametableSelect;
        private readonly BitVector32.Section _fineYScroll;

        VramAddress(ushort value)
        {
            _vector = new BitVector32(value);

            _full = BitVector32.CreateSection(0x7FFF);
            _addr = BitVector32.CreateSection(0x3FFF);

            _low = BitVector32.CreateSection(0xFF);
            _high = BitVector32.CreateSection(0x7F, _low);

            _coarseXScroll = BitVector32.CreateSection(0x1F);
            _coarseYScroll = BitVector32.CreateSection(0x1F, _coarseXScroll);
            _nametableSelect = BitVector32.CreateSection(0x3, _coarseYScroll);
            _fineYScroll = BitVector32.CreateSection(0x7, _nametableSelect);
        }

        // Bigguns

        public ushort Full
        {
            get { return (ushort)_vector[_full]; }
            set { _vector[_full] = value; }
        }

        public ushort Address
        {
            get { return (ushort)_vector[_addr]; }
            set { _vector[_addr] = value; }
        }

        public byte Low
        {
            get { return (byte)_vector[_low]; }
            set { _vector[_low] = value; }
        }

        public byte High
        {
            get { return (byte)_vector[_high]; }
            set { _vector[_high] = value; }
        }

        // Fields

        public byte CoarseXScroll
        {
            get { return (byte)_vector[_coarseXScroll]; }
            set { _vector[_coarseXScroll] = value; }
        }

        public byte CoarseYScroll
        {
            get { return (byte)_vector[_coarseYScroll]; }
            set { _vector[_coarseYScroll] = value; }
        }

        public byte NametableSelect
        {
            get { return (byte)_vector[_nametableSelect]; }
            set { _vector[_nametableSelect] = value; }
        }

        public byte FineYScroll
        {
            get { return (byte)_vector[_fineYScroll]; }
            set { _vector[_fineYScroll] = value; }
        }
    }
}