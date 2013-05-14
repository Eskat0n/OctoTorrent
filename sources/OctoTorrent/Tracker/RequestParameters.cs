namespace OctoTorrent.Tracker
{
    using System;
    using System.Collections.Specialized;
    using BEncoding;
    using System.Net;

    public abstract class RequestParameters : EventArgs
    {
        protected internal const string FailureKey = "failure reason";
        protected internal const string WarningKey = "warning message";

        private IPAddress _remoteAddress;
        private readonly NameValueCollection _parameters;
        private readonly BEncodedDictionary _response;

        public abstract bool IsValid { get; }
        
        public NameValueCollection Parameters
        {
            get { return _parameters; }
        }

        public BEncodedDictionary Response
        {
            get { return _response; }
        }

        public IPAddress RemoteAddress
        {
            get { return _remoteAddress; }
            protected set { _remoteAddress = value; }
        }

        protected RequestParameters(NameValueCollection parameters, IPAddress address)
        {
            _parameters = parameters;
            _remoteAddress = address;
            _response = new BEncodedDictionary();
        }
    }
}
