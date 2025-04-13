using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cryptography
{
    public class EncryptedPayload
    {
        public string EncryptedKey { get; set; }
        public string EncryptedMessage { get; set; }
        public string IV { get; set; }
        public string Signature { get; set; }
        public string SenderPublicKey { get; set; }
    }
}
