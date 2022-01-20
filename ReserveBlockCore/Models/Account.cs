using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using ReserveBlockCore.Data;
using ReserveBlockCore.EllipticCurve;

namespace ReserveBlockCore.Models
{
    internal class Account
    {
        public string PrivateKey { set; get; }
        public string PublicKey { set; get; }
        public string Address { get; set; }
        public decimal Balance { get; set; }

        public Account Build()
        {
            var account = new Account();
            account = AccountData.CreateNewAccount();
            return account;
        }

        public Account Restore(string privKey)
        {
            Account account = AccountData.RestoreAccount(privKey);
            return account;
        }
    }


}
