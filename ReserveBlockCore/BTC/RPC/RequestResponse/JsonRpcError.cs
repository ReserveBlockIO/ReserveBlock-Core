// Copyright (c) 2014 - 2016 George Kimionis
// See the accompanying file LICENSE for the Software License Aggrement

using ReserveBlockCore.BTC.RPC.Specifications;
using Newtonsoft.Json;

namespace ReserveBlockCore.BTC.RPC.RequestResponse
{
    public class JsonRpcError
    {
        [JsonProperty(PropertyName = "code")]
        public RpcErrorCode Code { get; set; }

        [JsonProperty(PropertyName = "message")]
        public string Message { get; set; }
    }
}