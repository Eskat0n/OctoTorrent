//
// EngineSettings.cs
//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2006 Alan McGovern
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//

namespace OctoTorrent.Client
{
    using System;
    using System.Net;
    using Encryption;

    /// <summary>
    /// Represents the Settings which need to be passed to the engine
    /// </summary>
    [Serializable]
    public class EngineSettings : ICloneable
    {
        #region Properties

        public EncryptionTypes AllowedEncryption { get; set; }

        public string FastResumePath { get; set; }

        public bool HaveSupressionEnabled { get; set; }

        public int GlobalMaxConnections { get; set; }

        public int GlobalMaxHalfOpenConnections { get; set; }

        public int GlobalMaxDownloadSpeed { get; set; }

        public int GlobalMaxUploadSpeed { get; set; }

        [Obsolete("Use the constructor overload for ClientEngine which takes a port argument." +
                  "Alternatively just use the ChangeEndpoint method at a later stage")]
        public int ListenPort { get; set; }

        public int MaxOpenFiles { get; set; }

        public int MaxReadRate { get; set; }

        public int MaxWriteRate { get; set; }

        public IPEndPoint ReportedAddress { get; set; }

        public bool PreferEncryption { get; set; }

        public string SavePath { get; set; }

        #endregion Properties

        #region Defaults

        private const string DefaultSavePath = "";
        private const int DefaultMaxConnections = 150;
        private const int DefaultMaxDownloadSpeed = 0;
        private const int DefaultMaxUploadSpeed = 0;
        private const int DefaultMaxHalfOpenConnections = 5;
        private const EncryptionTypes DefaultAllowedEncryption = EncryptionTypes.All;
        private const int DefaultListenPort = 52138;

        #endregion

        #region Constructors

        public EngineSettings(string defaultSavePath = DefaultSavePath, int listenPort = DefaultListenPort,
                              int globalMaxConnections = DefaultMaxConnections,
                              int globalHalfOpenConnections = DefaultMaxHalfOpenConnections,
                              int globalMaxDownloadSpeed = DefaultMaxDownloadSpeed,
                              int globalMaxUploadSpeed = DefaultMaxUploadSpeed,
                              EncryptionTypes allowedEncryption = DefaultAllowedEncryption,
                              string fastResumePath = null)
        {
            MaxOpenFiles = 15;
            GlobalMaxConnections = globalMaxConnections;
            GlobalMaxDownloadSpeed = globalMaxDownloadSpeed;
            GlobalMaxUploadSpeed = globalMaxUploadSpeed;
            GlobalMaxHalfOpenConnections = globalHalfOpenConnections;
            ListenPort = listenPort;
            AllowedEncryption = allowedEncryption;
            FastResumePath = fastResumePath;
            SavePath = defaultSavePath;
        }
 
        #endregion

        #region Methods

        object ICloneable.Clone()
        {
            return Clone();
        }

        public EngineSettings Clone()
        {
            return (EngineSettings)MemberwiseClone();
        }

        public override bool Equals(object obj)
        {
            var settings = obj as EngineSettings;
            return settings != null
                       ? GlobalMaxConnections == settings.GlobalMaxConnections &&
                         GlobalMaxDownloadSpeed == settings.GlobalMaxDownloadSpeed &&
                         GlobalMaxHalfOpenConnections == settings.GlobalMaxHalfOpenConnections &&
                         GlobalMaxUploadSpeed == settings.GlobalMaxUploadSpeed &&
                         ListenPort == settings.ListenPort &&
                         AllowedEncryption == settings.AllowedEncryption &&
                         SavePath == settings.SavePath
                       : false;
        }

        public override int GetHashCode()
        {
            return GlobalMaxConnections +
                   GlobalMaxDownloadSpeed +
                   GlobalMaxHalfOpenConnections +
                   GlobalMaxUploadSpeed +
                   ListenPort.GetHashCode() +
                   AllowedEncryption.GetHashCode() +
                   SavePath.GetHashCode();
            
        }

        #endregion Methods
    }
}