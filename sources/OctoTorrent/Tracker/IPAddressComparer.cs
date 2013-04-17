using System;
using System.Collections.Generic;
using System.Text;
using System.Net;

namespace OctoTorrent.Tracker
{
    public interface IPeerComparer
    {
        object GetKey(AnnounceParameters parameters);
    }

    public class IPAddressComparer : IPeerComparer
    {
        public object GetKey(AnnounceParameters parameters)
        {
            return parameters.ClientAddress;
        }
    }
}
