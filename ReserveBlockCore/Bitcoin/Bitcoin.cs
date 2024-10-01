using NBitcoin;
using ReserveBlockCore.Bitcoin.ElectrumX;
using ReserveBlockCore.Bitcoin.Integrations;
using ReserveBlockCore.Bitcoin.Models;
using ReserveBlockCore.Bitcoin.Services;
using ReserveBlockCore.Bitcoin.Utilities;
using ReserveBlockCore.Commands;
using ReserveBlockCore.Data;
using ReserveBlockCore.Models;
using ReserveBlockCore.Services;
using ReserveBlockCore.Utilities;
using Spectre.Console;
using System;
using System.Net;

namespace ReserveBlockCore.Bitcoin
{
   
    public class Bitcoin
    {
        static bool exit = false;
        static SemaphoreSlim BalanceCheckLock = new SemaphoreSlim(1, 1);
        private enum CommandResult
        {
            MainMenu,
            BtcMenu,
            Nothing
        }
        public static async Task StartBitcoinProgram()
        {
            await BitcoinMenu();
            exit = false;

            Explorers.PopulateExplorerDictionary();

            _ = NodeFinder.GetNode(); // Get Node for later use now. This is to save time.
            _ = ElectrumXRun();
            _ = AccountCheck();

            while (!exit)
            {
                var command = Console.ReadLine();
                if (!string.IsNullOrEmpty(command))
                {
                    var result = await ProcessCommand(command);
                    if (result == CommandResult.BtcMenu)
                        await BitcoinMenu();
                    if (result == CommandResult.MainMenu)
                        exit = true;

                }
            }
            Console.WriteLine("Return you to main menu in 5 seconds...");
            await Task.Delay(5000);
            StartupService.MainMenu();
        }

        private static async Task<CommandResult> ProcessCommand(string command)
        {
            CommandResult result = CommandResult.Nothing;

            switch (command.ToLower())
            {
                case "/btc":
                    result = CommandResult.BtcMenu;
                    break;
                case "/menu":
                    result = CommandResult.MainMenu;
                    break;
                case "1": //create address
                    BitcoinCommand.CreateAddress();
                    break;
                case "2": //show address(es)
                    await BitcoinCommand.ShowBitcoinAccounts();
                    break;
                case "3": //send transactions
                    await BitcoinCommand.SendBitcoinTransaction();
                    break;
                case "4": //Tokenize
                    await BitcoinCommand.TokenizeBitcoin();
                    break;
                case "4a": //Tokenize Generate Deposit Address
                    await BitcoinCommand.GenerateBTCTokenAddress();
                    break;
                case "4b": //Tokenize Transfer
                    Console.WriteLine("No vBTC Tokens to transfer at this time");
                    break;
                case "5": //Import address
                    await BitcoinCommand.ImportAddress();
                    break;
                case "6": //Bitcoin ADNR
                    Globals.StopConsoleOutput = true;
                    await BitcoinCommand.CreateDnr();
                    Globals.StopConsoleOutput = false;
                    break;
                case "7": //Reset Accounts
                    await BitcoinCommand.ResetAccounts();
                    break;
                case "8": //Exit
                    result = CommandResult.MainMenu;
                    break;
                case "/printkeys":
                    BitcoinCommand.PrintKeys();
                    break;
                case "/printutxos":
                    await BitcoinCommand.PrintUTXOs();
                    break;
            }

            return result;
        }

        public static async Task ElectrumXRun()
        {
            while(true)
            {
                bool electrumServerFound = false;
                while (!electrumServerFound)
                {
                    var electrumServer = Globals.ClientSettings.Where(x => x.FailCount < 10).OrderBy(x => x.Count).FirstOrDefault();
                    if (electrumServer != null)
                    {
                        try
                        {
                            var client = new Client(electrumServer.Host, electrumServer.Port, true);
                            var serverVersion = await client.GetServerVersion();

                            if (serverVersion == null)
                                throw new Exception("Bad server response or no connection.");

                            Globals.ElectrumXConnected = true;
                            Globals.ElectrumXLastCommunication = DateTime.Now;
                            electrumServerFound = true;
                        }
                        catch (Exception ex)
                        {
                            electrumServer.FailCount++;
                            electrumServer.Count++;
                        }
                    }
                    else
                    {
                        Globals.ElectrumXConnected = false;
                    }
                }

                if(Globals.ElectrumXLastCommunication < DateTime.Now.AddMinutes(-10))
                {
                    //reset counts
                    foreach(var elec in Globals.ClientSettings)
                    {
                        elec.FailCount = 0;
                    }
                }
                
                await Task.Delay(new TimeSpan(0,1,0));
            }   
        }

        public static async Task AccountCheck()
        {
            if(Globals.BTCAccountCheckRunning) return;

            while(!exit)
            {
                Globals.BTCAccountCheckRunning = true;
                var delay = Task.Delay(new TimeSpan(0,2,0));
                await BalanceCheckLock.WaitAsync();

                bool electrumServerFound = false;
                Client client = null;
                while(!electrumServerFound)
                {
                    var electrumServer = Globals.ClientSettings.Where(x => x.FailCount < 10).OrderBy(x => x.Count).FirstOrDefault();
                    if (electrumServer != null)
                    {
                        try
                        {
                            client = new Client(electrumServer.Host, electrumServer.Port, true);
                            var serverVersion = await client.GetServerVersion();

                            if (serverVersion == null)
                                throw new Exception("Bad server response or no connection.");

                            if(serverVersion.ProtocolVersion.Major != 1 && serverVersion.ProtocolVersion.Minor < 4)
                                throw new Exception("Bad version.");

                            electrumServerFound = true;
                            electrumServer.Count++;
                        }
                        catch (Exception ex)
                        {
                            //TODO: ADD LOGS
                            electrumServer.FailCount++;
                            electrumServer.Count++;
                            await Task.Delay(1000);
                        }

                    }
                    else
                    {
                        //no servers found
                        await Task.Delay(60000);
                    }
                    //TODO: ADD LOGS
                    await Task.Delay(1000);
                }
                
                
                try
                {
                    Globals.BTCSyncing = true;
                    if (client == null)
                        throw new Exception("ElectrumX client was null");

                    var addressList = BitcoinAccount.GetBitcoin()?.FindAll().ToList();

                    if (addressList?.Count != 0)
                    {
                        foreach (var address in addressList)
                        {
                            bool checkForNewUnspent = false;
                            bool unconfirmedFound = false;
                            var balance = await client.GetBalance(address.Address, false);
                            var btcAccount = BitcoinAccount.GetBitcoin()?.FindOne(x => x.Address == address.Address);
                            if (btcAccount != null)
                            {
                                if(btcAccount.Balance != (balance.Confirmed / 100_000_000M))
                                    checkForNewUnspent = true;

                                if ((balance.Unconfirmed / 100_000_000M) > 0.0M)
                                    unconfirmedFound = true;

                                btcAccount.Balance = balance.Confirmed / 100_000_000M;
                                BitcoinAccount.GetBitcoin()?.UpdateSafe(btcAccount);
                            }
                            
                            if(checkForNewUnspent)
                            {
                                var transactions = await client.GetListUnspent(address.Address, false);
                                if (transactions?.Count > 0)
                                {
                                    var walletUtxoList = BitcoinUTXO.GetUTXOs(address.Address);
                                    if (walletUtxoList != null)
                                    {
                                        var utxoList = transactions;
                                        if (utxoList?.Count > 0)
                                        {
                                            foreach (var item in utxoList)
                                            {
                                                var nUTXO = new BitcoinUTXO
                                                {
                                                    Address = address.Address,
                                                    IsUsed = false,
                                                    TxId = item.TxHash,
                                                    Value = (long)item.Value,
                                                    Vout = (int)item.TxPos
                                                };

                                                BitcoinUTXO.SaveBitcoinUTXO(nUTXO, true);
                                            }
                                        }
                                    }
                                    else
                                    {
                                        if (transactions?.Count > 0)
                                        {
                                            foreach (var item in transactions)
                                            {
                                                var nUTXO = new BitcoinUTXO
                                                {
                                                    Address = address.Address,
                                                    IsUsed = false,
                                                    TxId = item.TxHash,
                                                    Value = (long)item.Value,
                                                    Vout = (int)item.TxPos
                                                };

                                                BitcoinUTXO.SaveBitcoinUTXO(nUTXO, true);
                                            }
                                        }
                                    }
                                }
                            }

                            if(checkForNewUnspent || unconfirmedFound)
                            {
                                //Also check for new TXs
                                var transactionList = BitcoinTransaction.GetTXs(address.Address);
                                if(transactionList?.Count > 0)
                                {
                                    var history = await client.GetHistory(address.Address);
                                    if(history?.Count > 0)
                                    {
                                        foreach(var bTx in history)
                                        {
                                            var localTx = transactionList.Where(x => x.Hash == bTx.TxHash).FirstOrDefault();
                                            if (localTx != null)
                                            {
                                                //confirmed
                                                if (bTx.Height != 0 && !localTx.IsConfirmed && localTx.ConfirmedHeight == 0)
                                                {
                                                    localTx.IsConfirmed = true;
                                                    localTx.ConfirmedHeight = bTx.Height;
                                                    BitcoinTransaction.UpdateBitcoinTX(localTx);
                                                }
                                            }
                                            else
                                            {
                                                //confirmed
                                                if (bTx.Height != 0)
                                                {
                                                    var rawTx = await client.GetRawTx(bTx.TxHash);
                                                    var tx = NBitcoin.Transaction.Parse(rawTx.RawTx, Globals.BTCNetwork);
                                                    var bitcoinAddress = BitcoinAddress.Create(address.Address, Globals.BTCNetwork);
                                                    List<BitcoinAddress> outgoingAddrs = new List<BitcoinAddress>();
                                                    foreach (var input in tx.Inputs)
                                                    {
                                                        var result = await InputUtility.GetAddressFromInput(client, input);
                                                        if (result != null)
                                                            outgoingAddrs.Add(result);
                                                    }

                                                    bool isOutgoing = outgoingAddrs.Any(inputAddress => inputAddress == bitcoinAddress);

                                                    var fromAddress = "";
                                                    var toAddress = "";
                                                    var amount = 0.0M;
                                                    foreach (var output in tx.Outputs)
                                                    {
                                                        var outputAddress = output.ScriptPubKey.GetDestinationAddress(Globals.BTCNetwork);
                                                        

                                                        // Heuristic: if the address is not the sender's address, it's likely a recipient
                                                        if (outputAddress == bitcoinAddress && isOutgoing)
                                                        {
                                                            fromAddress = address.Address;
                                                            toAddress = outputAddress.ToString();
                                                        }
                                                        if (outputAddress != bitcoinAddress && isOutgoing)
                                                        {
                                                            amount = output.Value.ToUnit(MoneyUnit.BTC);
                                                        }

                                                        if (outputAddress != bitcoinAddress && !isOutgoing)
                                                        {
                                                            fromAddress = outputAddress.ToString();
                                                            toAddress = address.Address;
                                                        }
                                                        if (outputAddress == bitcoinAddress && !isOutgoing)
                                                        {
                                                            amount = output.Value.ToUnit(MoneyUnit.BTC);
                                                        }

                                                    }

                                                    var totalInputAmount = await InputUtility.CalculateTotalInputAmount(client, tx);
                                                    var totalOutputAmount = tx.Outputs.Sum(o => o.Value);
                                                    var fee = totalInputAmount - totalOutputAmount;
                                                    var timestampUnix = TimeUtil.GetTime();
                                                    var blockHeader = await client.GetBlockHeaderHex(bTx.Height);

                                                    if (blockHeader != null)
                                                    {
                                                        var timestampHex = blockHeader.Hex.Substring(136, 8);

                                                        // Reverse the byte order (little-endian to big-endian)
                                                        var reversedTimestampHex = string.Join("", Enumerable.Range(0, 4).Select(i => timestampHex.Substring(i * 2, 2)).Reverse());

                                                        // Convert the reversed hex string to Unix timestamp
                                                        timestampUnix = TimeUtil.GetTime(reversedTimestampHex);
                                                    }

                                                    var nTx = new BitcoinTransaction {
                                                    Amount = amount,
                                                    BitcoinUTXOs = new List<BitcoinUTXO>(),
                                                    Fee = fee.ToUnit(MoneyUnit.BTC),
                                                    FeeRate = 0,
                                                    FromAddress = fromAddress,
                                                    ToAddress = toAddress,
                                                    Hash = bTx.TxHash,
                                                    IsConfirmed = true,
                                                    ConfirmedHeight = bTx.Height,
                                                    Signature = rawTx.RawTx.ToString(),
                                                    Timestamp = timestampUnix,
                                                    TransactionType = isOutgoing ? BTCTransactionType.Send : BTCTransactionType.Receive,
                                                    };

                                                    BitcoinTransaction.SaveBitcoinTX(nTx);
                                                }
                                                else
                                                {
                                                    var rawTx = await client.GetRawTx(bTx.TxHash);
                                                    var tx = NBitcoin.Transaction.Parse(rawTx.RawTx, Globals.BTCNetwork);
                                                    var bitcoinAddress = BitcoinAddress.Create(address.Address, Globals.BTCNetwork);
                                                    List<BitcoinAddress> outgoingAddrs = new List<BitcoinAddress>();
                                                    foreach (var input in tx.Inputs)
                                                    {
                                                        var result = await InputUtility.GetAddressFromInput(client, input);
                                                        if (result != null)
                                                            outgoingAddrs.Add(result);
                                                    }

                                                    bool isOutgoing = outgoingAddrs.Any(inputAddress => inputAddress == bitcoinAddress);

                                                    var fromAddress = "";
                                                    var toAddress = "";
                                                    var amount = 0.0M;
                                                    foreach (var output in tx.Outputs)
                                                    {
                                                        var outputAddress = output.ScriptPubKey.GetDestinationAddress(Globals.BTCNetwork);


                                                        // Heuristic: if the address is not the sender's address, it's likely a recipient
                                                        if (outputAddress == bitcoinAddress && isOutgoing)
                                                        {
                                                            fromAddress = address.Address;
                                                            toAddress = outputAddress.ToString();
                                                        }
                                                        if (outputAddress != bitcoinAddress && isOutgoing)
                                                        {
                                                            amount = output.Value.ToUnit(MoneyUnit.BTC);
                                                        }

                                                        if (outputAddress != bitcoinAddress && !isOutgoing)
                                                        {
                                                            fromAddress = outputAddress.ToString();
                                                            toAddress = address.Address;
                                                        }
                                                        if (outputAddress == bitcoinAddress && !isOutgoing)
                                                        {
                                                            amount = output.Value.ToUnit(MoneyUnit.BTC);
                                                        }

                                                    }

                                                    var totalInputAmount = await InputUtility.CalculateTotalInputAmount(client, tx);
                                                    var totalOutputAmount = tx.Outputs.Sum(o => o.Value);
                                                    var fee = totalInputAmount - totalOutputAmount;

                                                    var timestampUnix = TimeUtil.GetTime();
                                                    var blockHeader = await client.GetBlockHeaderHex(bTx.Height);

                                                    if (blockHeader != null)
                                                    {
                                                        var timestampHex = blockHeader.Hex.Substring(136, 8);

                                                        // Reverse the byte order (little-endian to big-endian)
                                                        var reversedTimestampHex = string.Join("", Enumerable.Range(0, 4).Select(i => timestampHex.Substring(i * 2, 2)).Reverse());

                                                        // Convert the reversed hex string to Unix timestamp
                                                        timestampUnix = TimeUtil.GetTime(reversedTimestampHex);
                                                    }

                                                    var nTx = new BitcoinTransaction
                                                    {
                                                        Amount = amount,
                                                        BitcoinUTXOs = new List<BitcoinUTXO>(),
                                                        Fee = fee.ToUnit(MoneyUnit.BTC),
                                                        FeeRate = 0,
                                                        FromAddress = fromAddress,
                                                        ToAddress = toAddress,
                                                        Hash = bTx.TxHash,
                                                        IsConfirmed = false,
                                                        Signature = rawTx.RawTx.ToString(),
                                                        Timestamp = timestampUnix,
                                                        TransactionType = isOutgoing ? BTCTransactionType.Send : BTCTransactionType.Receive,
                                                    };

                                                    BitcoinTransaction.SaveBitcoinTX(nTx);
                                                }
                                            }
                                        }
                                    }
                                }
                                else
                                {
                                    await AddressSyncService.GetTxHistory(client, address.Address);
                                }
                            }
                            
                            await Task.Delay(1500);
                        }
                    }

                    //Tokenized Address Check
                    var tokenizeList = await TokenizedBitcoin.GetTokenizedList();
                    if(tokenizeList != null && tokenizeList?.Count() != 0)
                    {
                        foreach (var address in tokenizeList)
                        {
                            if(address.DepositAddress != null)
                            {
                                bool checkForNewUnspent = false;
                                bool unconfirmedFound= false;
                                var balance = await client.GetBalance(address.DepositAddress, false);

                                var btcAccount = BitcoinAccount.GetBitcoin()?.FindOne(x => x.Address == address.DepositAddress);

                                if (btcAccount != null)
                                {
                                    if (btcAccount.Balance != (balance.Confirmed / 100_000_000M))
                                        checkForNewUnspent = true;

                                    btcAccount.Balance = balance.Confirmed / 100_000_000M;
                                    BitcoinAccount.GetBitcoin()?.UpdateSafe(btcAccount);
                                }
                                if (address.Balance != (balance.Confirmed / 100_000_000M))
                                    checkForNewUnspent = true;

                                if ((balance.Unconfirmed / 100_000_000M) > 0.0M)
                                    unconfirmedFound = true;

                                await TokenizedBitcoin.UpdateBalance(address.DepositAddress, balance.Confirmed / 100_000_000M, address.RBXAddress);

                                if(checkForNewUnspent)
                                {
                                    var transactions = await client.GetListUnspent(address.DepositAddress, false);
                                    if (transactions?.Count > 0)
                                    {
                                        var walletUtxoList = BitcoinUTXO.GetUTXOs(address.DepositAddress);
                                        if (walletUtxoList != null)
                                        {
                                            var utxoList = transactions;
                                            if (utxoList?.Count > 0)
                                            {
                                                foreach (var item in utxoList)
                                                {
                                                    var nUTXO = new BitcoinUTXO
                                                    {
                                                        Address = address.DepositAddress,
                                                        IsUsed = false,
                                                        TxId = item.TxHash,
                                                        Value = (long)item.Value,
                                                        Vout = (int)item.TxPos
                                                    };

                                                    BitcoinUTXO.SaveBitcoinUTXO(nUTXO, true);
                                                }
                                            }
                                        }
                                        else
                                        {
                                            if (transactions?.Count > 0)
                                            {
                                                foreach (var item in transactions)
                                                {
                                                    var nUTXO = new BitcoinUTXO
                                                    {
                                                        Address = address.DepositAddress,
                                                        IsUsed = false,
                                                        TxId = item.TxHash,
                                                        Value = (long)item.Value,
                                                        Vout = (int)item.TxPos
                                                    };

                                                    BitcoinUTXO.SaveBitcoinUTXO(nUTXO, true);
                                                }
                                            }
                                        }
                                    }
                                }

                                if (checkForNewUnspent || unconfirmedFound)
                                {
                                    //Also check for new TXs
                                    var transactionList = BitcoinTransaction.GetTXs(address.DepositAddress);
                                    if (transactionList?.Count > 0)
                                    {
                                        var history = await client.GetHistory(address.DepositAddress);
                                        if (history?.Count > 0)
                                        {
                                            foreach (var bTx in history)
                                            {
                                                var localTx = transactionList.Where(x => x.Hash == bTx.TxHash).FirstOrDefault();
                                                if (localTx != null)
                                                {
                                                    //confirmed
                                                    if (bTx.Height != 0 && !localTx.IsConfirmed && localTx.ConfirmedHeight == 0)
                                                    {
                                                        localTx.IsConfirmed = true;
                                                        localTx.ConfirmedHeight = bTx.Height;
                                                        BitcoinTransaction.UpdateBitcoinTX(localTx);
                                                    }
                                                }
                                                else
                                                {
                                                    //confirmed
                                                    if (bTx.Height != 0)
                                                    {
                                                        var rawTx = await client.GetRawTx(bTx.TxHash);
                                                        var tx = NBitcoin.Transaction.Parse(rawTx.RawTx, Globals.BTCNetwork);
                                                        var bitcoinAddress = BitcoinAddress.Create(address.DepositAddress, Globals.BTCNetwork);
                                                        List<BitcoinAddress> outgoingAddrs = new List<BitcoinAddress>();
                                                        foreach (var input in tx.Inputs)
                                                        {
                                                            var result = await InputUtility.GetAddressFromInput(client, input);
                                                            if (result != null)
                                                                outgoingAddrs.Add(result);
                                                        }

                                                        bool isOutgoing = outgoingAddrs.Any(inputAddress => inputAddress == bitcoinAddress);

                                                        var fromAddress = "";
                                                        var toAddress = "";
                                                        var amount = 0.0M;
                                                        foreach (var output in tx.Outputs)
                                                        {
                                                            var outputAddress = output.ScriptPubKey.GetDestinationAddress(Globals.BTCNetwork);


                                                            // Heuristic: if the address is not the sender's address, it's likely a recipient
                                                            if (outputAddress == bitcoinAddress && isOutgoing)
                                                            {
                                                                fromAddress = address.DepositAddress;
                                                                toAddress = outputAddress.ToString();
                                                            }
                                                            if (outputAddress != bitcoinAddress && isOutgoing)
                                                            {
                                                                amount = output.Value.ToUnit(MoneyUnit.BTC);
                                                            }

                                                            if (outputAddress != bitcoinAddress && !isOutgoing)
                                                            {
                                                                fromAddress = outputAddress.ToString();
                                                                toAddress = address.DepositAddress;
                                                            }
                                                            if (outputAddress == bitcoinAddress && !isOutgoing)
                                                            {
                                                                amount = output.Value.ToUnit(MoneyUnit.BTC);
                                                            }

                                                        }

                                                        var totalInputAmount = await InputUtility.CalculateTotalInputAmount(client, tx);
                                                        var totalOutputAmount = tx.Outputs.Sum(o => o.Value);
                                                        var fee = totalInputAmount - totalOutputAmount;

                                                        var timestampUnix = TimeUtil.GetTime();
                                                        var blockHeader = await client.GetBlockHeaderHex(bTx.Height);

                                                        if (blockHeader != null)
                                                        {
                                                            var timestampHex = blockHeader.Hex.Substring(136, 8);

                                                            // Reverse the byte order (little-endian to big-endian)
                                                            var reversedTimestampHex = string.Join("", Enumerable.Range(0, 4).Select(i => timestampHex.Substring(i * 2, 2)).Reverse());

                                                            // Convert the reversed hex string to Unix timestamp
                                                            timestampUnix = TimeUtil.GetTime(reversedTimestampHex);
                                                        }

                                                        var nTx = new BitcoinTransaction
                                                        {
                                                            Amount = amount,
                                                            BitcoinUTXOs = new List<BitcoinUTXO>(),
                                                            Fee = fee.ToUnit(MoneyUnit.BTC),
                                                            FeeRate = 0,
                                                            FromAddress = fromAddress,
                                                            ToAddress = toAddress,
                                                            Hash = bTx.TxHash,
                                                            IsConfirmed = true,
                                                            ConfirmedHeight = bTx.Height,
                                                            Signature = rawTx.RawTx.ToString(),
                                                            Timestamp = timestampUnix,
                                                            TransactionType = isOutgoing ? BTCTransactionType.Send : BTCTransactionType.Receive,
                                                        };

                                                        BitcoinTransaction.SaveBitcoinTX(nTx);
                                                    }
                                                    else
                                                    {
                                                        var rawTx = await client.GetRawTx(bTx.TxHash);
                                                        var tx = NBitcoin.Transaction.Parse(rawTx.RawTx, Globals.BTCNetwork);
                                                        var bitcoinAddress = BitcoinAddress.Create(address.DepositAddress, Globals.BTCNetwork);
                                                        List<BitcoinAddress> outgoingAddrs = new List<BitcoinAddress>();
                                                        foreach (var input in tx.Inputs)
                                                        {
                                                            var result = await InputUtility.GetAddressFromInput(client, input);
                                                            if (result != null)
                                                                outgoingAddrs.Add(result);
                                                        }

                                                        bool isOutgoing = outgoingAddrs.Any(inputAddress => inputAddress == bitcoinAddress);

                                                        var fromAddress = "";
                                                        var toAddress = "";
                                                        var amount = 0.0M;
                                                        foreach (var output in tx.Outputs)
                                                        {
                                                            var outputAddress = output.ScriptPubKey.GetDestinationAddress(Globals.BTCNetwork);


                                                            // Heuristic: if the address is not the sender's address, it's likely a recipient
                                                            if (outputAddress == bitcoinAddress && isOutgoing)
                                                            {
                                                                fromAddress = address.DepositAddress;
                                                                toAddress = outputAddress.ToString();
                                                            }
                                                            if (outputAddress != bitcoinAddress && isOutgoing)
                                                            {
                                                                amount = output.Value.ToUnit(MoneyUnit.BTC);
                                                            }

                                                            if (outputAddress != bitcoinAddress && !isOutgoing)
                                                            {
                                                                fromAddress = outputAddress.ToString();
                                                                toAddress = address.DepositAddress;
                                                            }
                                                            if (outputAddress == bitcoinAddress && !isOutgoing)
                                                            {
                                                                amount = output.Value.ToUnit(MoneyUnit.BTC);
                                                            }

                                                        }

                                                        var totalInputAmount = await InputUtility.CalculateTotalInputAmount(client, tx);
                                                        var totalOutputAmount = tx.Outputs.Sum(o => o.Value);
                                                        var fee = totalInputAmount - totalOutputAmount;

                                                        var timestampUnix = TimeUtil.GetTime();
                                                        var blockHeader = await client.GetBlockHeaderHex(bTx.Height);

                                                        if (blockHeader != null)
                                                        {
                                                            var timestampHex = blockHeader.Hex.Substring(136, 8);

                                                            // Reverse the byte order (little-endian to big-endian)
                                                            var reversedTimestampHex = string.Join("", Enumerable.Range(0, 4).Select(i => timestampHex.Substring(i * 2, 2)).Reverse());

                                                            // Convert the reversed hex string to Unix timestamp
                                                            timestampUnix = TimeUtil.GetTime(reversedTimestampHex);
                                                        }

                                                        var nTx = new BitcoinTransaction
                                                        {
                                                            Amount = amount,
                                                            BitcoinUTXOs = new List<BitcoinUTXO>(),
                                                            Fee = fee.ToUnit(MoneyUnit.BTC),
                                                            FeeRate = 0,
                                                            FromAddress = fromAddress,
                                                            ToAddress = toAddress,
                                                            Hash = bTx.TxHash,
                                                            IsConfirmed = false,
                                                            Signature = rawTx.RawTx.ToString(),
                                                            Timestamp = timestampUnix,
                                                            TransactionType = isOutgoing ? BTCTransactionType.Send : BTCTransactionType.Receive,
                                                        };

                                                        BitcoinTransaction.SaveBitcoinTX(nTx);
                                                    }
                                                }
                                            }
                                        }
                                    }
                                    else
                                    {

                                    }
                                }

                                var scState = SmartContractStateTrei.GetSmartContractState(address.SmartContractUID);
                                var postAuditTknz = await TokenizedBitcoin.GetTokenizedBitcoin(address.SmartContractUID);
                                if(scState != null && postAuditTknz != null)
                                {
                                    if (scState.SCStateTreiTokenizationTXes != null && scState.SCStateTreiTokenizationTXes.Any())
                                    {
                                        var balanceList = scState.SCStateTreiTokenizationTXes.ToList();
                                        if (balanceList.Any())
                                        {
                                            var stateBalance = balanceList.Sum(x => x.Amount);
                                            var totalBalance = postAuditTknz.Balance;
                                            if (stateBalance > totalBalance)
                                            {
                                                if(postAuditTknz.DepositAddress != null)
                                                    await TokenizedBitcoin.FlagInsolvent(postAuditTknz.DepositAddress);
                                            }
                                        }
                                    }
                                }

                                await Task.Delay(1500);
                            }
                            
                        }
                    }
                }
                catch (Exception ex)
                {
                    ErrorLogUtility.LogError($"Error Running BTC Account Check. Error: {ex}", "Bitcoin.AccountCheck()");
                }
                finally
                {
                    Globals.BTCAccountLastCheckedDate = DateTime.Now;
                    BalanceCheckLock.Release();
                    Globals.BTCSyncing = false;
                    if (client != null)
                        client.Dispose();
                }

                await delay;
            }

            Globals.BTCAccountCheckRunning = false;
        }

        public static async Task TransferCoinAudit(string scUID)
        {
            var tknzBtc = await TokenizedBitcoin.GetTokenizedBitcoin(scUID);

            if (tknzBtc?.DepositAddress != null)
            {
                await Explorers.GetAddressInfo(tknzBtc.DepositAddress, tknzBtc.RBXAddress, true);
                await Task.Delay(5000);
            }
        }

        public static async Task BitcoinMenu()
        {
            Console.Clear();
            Console.SetCursorPosition(Console.CursorLeft, Console.CursorTop);

            if (Globals.IsTestNet != true)
            {
                AnsiConsole.Write(
                new FigletText("Bitcoin + VerifiedX")
                .LeftJustified()
                .Color(new Color(247, 147, 26)));
            }
            else
            {
                AnsiConsole.Write(
                new FigletText("Bitcoin + VerifiedX - TestNet")
                .LeftJustified()
                .Color(new Color(50, 146, 57)));
            }

            if (Globals.IsTestNet != true)
            {
                Console.WriteLine("Bitcoin + VerifiedX Network - Mainnet");
            }
            else
            {
                Console.WriteLine("Bitcoin + VerifiedX Network - **TestNet**");
            }

            Console.WriteLine("|========================================|");
            Console.WriteLine("| 1. Create Address                      |");
            Console.WriteLine("| 2. Show Address(es)                    |");
            Console.WriteLine("| 3. Send Transaction                    |");
            Console.WriteLine("| 4. Tokenize Bitcoin                    |");
            Console.WriteLine("| 4a. Generate vBTC Deposit Address      |");
            Console.WriteLine("| 4b. Transfer vBTC                      |");
            Console.WriteLine("| 5. Import Address                      |");
            Console.WriteLine("| 6. Bitcoin ADNR Register               |");
            Console.WriteLine("| 7. Reset/Resync Accounts               |");
            Console.WriteLine("| 8. Exit Bitcoin Wallet                 |");
            Console.WriteLine("|========================================|");
            Console.WriteLine("|type /btc to come back to the main area |");
            Console.WriteLine("|type /menu to go back to RBX Wallet     |");
            Console.WriteLine("|========================================|");
        }

        public enum BitcoinAddressFormat
        {
            SegwitP2SH = 0,
            Segwit = 1,
            Taproot = 2
        }

    }
}
