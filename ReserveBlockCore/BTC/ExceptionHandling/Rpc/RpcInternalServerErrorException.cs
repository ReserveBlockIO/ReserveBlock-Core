// Copyright (c) 2014 - 2016 George Kimionis
// See the accompanying file LICENSE for the Software License Aggrement

using System;
using System.Runtime.Serialization;
using ReserveBlockCore.BTC.RPC.Specifications;

namespace ReserveBlockCore.BTC.ExceptionHandling.Rpc
{
    [Serializable]
    public class RpcInternalServerErrorException : Exception
    {
        public RpcInternalServerErrorException()
        {
        }

        public RpcInternalServerErrorException(string customMessage) : base(customMessage)
        {
        }

        public RpcInternalServerErrorException(string customMessage, Exception exception) : base(customMessage, exception)
        {
        }

        public RpcErrorCode? RpcErrorCode { get; set; }

        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            if (info == null)
            {
                throw new ArgumentNullException("info");
            }

            info.AddValue("RpcErrorCode", RpcErrorCode, typeof(RpcErrorCode));
            base.GetObjectData(info, context);
        }
    }
}