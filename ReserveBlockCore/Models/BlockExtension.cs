using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace ReserveBlockCore.Models
{
    public static class BlockExtension
    {
        private static string toJson(this Block block)
        {
            return JsonConvert.SerializeObject(block);
        }
    }
}
