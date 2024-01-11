// Copyright (c) 2014 - 2016 George Kimionis
// See the accompanying file LICENSE for the Software License Aggrement

using ReserveBlockCore.BTC.CoinParameters.Bitcoin;
using ReserveBlockCore.BTC.Services.Coins.Base;

namespace ReserveBlockCore.BTC.Services.Coins.Bitcoin
{
    public interface IBitcoinService : ICoinService, IBitcoinConstants
    {
    }
}