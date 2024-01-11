// Copyright (c) 2014 - 2016 George Kimionis
// See the accompanying file LICENSE for the Software License Aggrement

using ReserveBlockCore.BTC.RPC.Specifications;

namespace ReserveBlockCore.BTC.RPC.Connector
{
    public interface IRpcConnector
    {
        T MakeRequest<T>(RpcMethods method, params object[] parameters);
    }
}