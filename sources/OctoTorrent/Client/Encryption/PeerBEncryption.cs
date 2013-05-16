//
// PeerBEncryption.cs
//
// Authors:
//   Yiduo Wang planetbeing@gmail.com
//   Alan McGovern alan.mcgovern@gmail.com
//
// Copyright (C) 2007 Yiduo Wang
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

using System;
using System.Text;
using OctoTorrent.Common;
using OctoTorrent.Client.Messages;

namespace OctoTorrent.Client.Encryption
{
    /// <summary>
    /// Class to handle message stream encryption for receiving connections
    /// </summary>
    class PeerBEncryption : EncryptedSocket
    {
        private readonly InfoHash[] _possibleSKEYs;
        private byte[] _verifyBytes;

        private readonly AsyncCallback _gotVerificationCallback;
        private readonly AsyncCallback _gotPadCCallback;

        public PeerBEncryption(InfoHash[] possibleSKEYs, EncryptionTypes allowedEncryption)
            : base(allowedEncryption)
        {
            _possibleSKEYs = possibleSKEYs;

            _gotVerificationCallback = GotVerification;
            _gotPadCCallback = gotPadC;
        }

        protected override void doneReceiveY()
        {
            try
            {
                base.doneReceiveY(); // 1 A->B: Diffie Hellman Ya, PadA

                byte[] req1 = Hash(Encoding.ASCII.GetBytes("req1"), S);
                Synchronize(req1, 628); // 3 A->B: HASH('req1', S)
            }
            catch (Exception ex)
            {
                AsyncResult.Complete(ex);
            }
        }


        protected override void DoneSynchronize()
        {
            try
            {
                base.DoneSynchronize();

                _verifyBytes = new byte[20 + VerificationConstant.Length + 4 + 2]; 
                // ... HASH('req2', SKEY) xor HASH('req3', S), ENCRYPT(VC, crypto_provide, len(PadC), PadC, len(IA))

                ReceiveMessage(_verifyBytes, _verifyBytes.Length, _gotVerificationCallback);
            }
            catch (Exception ex)
            {
                AsyncResult.Complete(ex);
            }
        }

        byte[] _b;

        private void GotVerification(IAsyncResult result)
        {
            try
            {
                var torrentHash = new byte[20];

                var myVC = new byte[8];
                var myCP = new byte[4];
                var lenPadC = new byte[2];

                Array.Copy(_verifyBytes, 0, torrentHash, 0, torrentHash.Length); // HASH('req2', SKEY) xor HASH('req3', S)

                if (!MatchSKEY(torrentHash))
                {
                    AsyncResult.Complete(new EncryptionException("No valid SKey found"));
                    return;
                }

                CreateCryptors("keyB", "keyA");

                DoDecrypt(_verifyBytes, 20, 14); // ENCRYPT(VC, ...

                Array.Copy(_verifyBytes, 20, myVC, 0, myVC.Length);
                if (!Toolbox.ByteMatch(myVC, VerificationConstant))
                {
                    AsyncResult.Complete(new EncryptionException("Verification constant was invalid"));
                    return;
                }

                Array.Copy(_verifyBytes, 28, myCP, 0, myCP.Length); // ...crypto_provide ...
                
                // We need to select the crypto *after* we send our response, otherwise the wrong
                // encryption will be used on the response
                _b = myCP;
                Array.Copy(_verifyBytes, 32, lenPadC, 0, lenPadC.Length); // ... len(padC) ...
                PadC = new byte[DeLen(lenPadC) + 2];
                ReceiveMessage(PadC, PadC.Length, _gotPadCCallback); // padC            
            }
            catch (Exception ex)
            {
                AsyncResult.Complete(ex);
            }
        }

        private void gotPadC(IAsyncResult result)
        {
            try
            {
                DoDecrypt(PadC, 0, PadC.Length);

                byte[] lenInitialPayload = new byte[2]; // ... len(IA))
                Array.Copy(PadC, PadC.Length - 2, lenInitialPayload, 0, 2);

                RemoteInitialPayload = new byte[DeLen(lenInitialPayload)]; // ... ENCRYPT(IA)
                ReceiveMessage(RemoteInitialPayload, RemoteInitialPayload.Length, gotInitialPayload);
            }
            catch (Exception ex)
            {
                AsyncResult.Complete(ex);
            }
        }

        private void gotInitialPayload(IAsyncResult result)
        {
            try
            {
                DoDecrypt(RemoteInitialPayload, 0, RemoteInitialPayload.Length); // ... ENCRYPT(IA)
                StepFour();
            }
            catch (Exception ex)
            {
                AsyncResult.Complete(ex);
            }
        }

        private void StepFour()
        {
            try
            {
                byte[] padD = GeneratePad();
                SelectCrypto(_b, false);
                // 4 B->A: ENCRYPT(VC, crypto_select, len(padD), padD)
                byte[] buffer = new byte[VerificationConstant.Length + CryptoSelect.Length + 2 + padD.Length];
                
                int offset = 0;
                offset += Message.Write(buffer, offset, VerificationConstant);
                offset += Message.Write(buffer, offset, CryptoSelect);
                offset += Message.Write(buffer, offset, Len(padD));
                offset += Message.Write(buffer, offset, padD);

                DoEncrypt(buffer, 0, buffer.Length);
                SendMessage(buffer);

                SelectCrypto(_b, true);

                Ready();
            }

            catch (Exception ex)
            {
                AsyncResult.Complete(ex);
            }
        }


        /// <summary>
        /// Matches a torrent based on whether the HASH('req2', SKEY) xor HASH('req3', S) matches, where SKEY is the InfoHash of the torrent
        /// and sets the SKEY to the InfoHash of the matched torrent.
        /// </summary>
        /// <returns>true if a match has been found</returns>
        private bool MatchSKEY(byte[] torrentHash)
        {
            try
            {
                foreach (var infoHash in _possibleSKEYs)
                {
                    byte[] req2 = Hash(Encoding.ASCII.GetBytes("req2"), infoHash.Hash);
                    byte[] req3 = Hash(Encoding.ASCII.GetBytes("req3"), S);
                    
                    bool match = true;
                    for (int j = 0; j < req2.Length && match; j++)
                        match = torrentHash[j] == (req2[j] ^ req3[j]);

                    if (match)
                    {
                        SKEY = infoHash;
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                AsyncResult.Complete(ex);
            }
            return false;
        }
    }
}