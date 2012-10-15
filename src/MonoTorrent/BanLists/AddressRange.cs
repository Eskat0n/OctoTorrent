namespace MonoTorrent.Client
{
    using System;
    using System.Net;

    public struct AddressRange
    {
        public int Start;
        public int End;

        public AddressRange(int start, int end)
        {
            Start = start;
            End = end;
        }

        public AddressRange(IPAddress start, IPAddress end)
        {
            Start = (IPAddress.NetworkToHostOrder(BitConverter.ToInt32(start.GetAddressBytes(), 0)));
            End = (IPAddress.NetworkToHostOrder(BitConverter.ToInt32(end.GetAddressBytes(), 0)));
        }

        public bool Contains(int value)
        {
            return value >= Start && value <= End;
        }

        public bool Contains(AddressRange range)
        {
            return range.Start >= Start && range.End <= End;
        }

        public override string ToString()
        {
            return string.Format("{0},{1}", Start, End);
        }
    }
}