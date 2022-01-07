using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReserveBlockCore.Models
{
    public static class TransactionExtension
    {
        public static string toJson(this Transaction transaction)
        {
            return JsonConvert.SerializeObject(transaction);
        }
    }
}
