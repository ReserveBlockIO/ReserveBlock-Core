using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace ReserveBlockCore.Models
{
    internal class Account
    {
        public BigInteger SecretNumber { set; get; }
        public string PrivateKey { set; get; }
        public string PublicKey { set; get; }

    }
}
