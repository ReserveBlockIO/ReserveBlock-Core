// Copyright (c) 2014 - 2016 George Kimionis
// See the accompanying file LICENSE for the Software License Aggrement

using ReserveBlockCore.BTC.CoinParameters.Base;
using ReserveBlockCore.BTC.Services.RpcServices.RpcExtenderService;
using ReserveBlockCore.BTC.Services.RpcServices.RpcService;

namespace ReserveBlockCore.BTC.Services.Coins.Base
{
    public interface ICoinService : IRpcService, IRpcExtenderService, ICoinParameters
    {
    }
}