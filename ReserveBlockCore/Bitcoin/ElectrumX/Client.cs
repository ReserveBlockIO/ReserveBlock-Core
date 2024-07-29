using NBitcoin;
using Newtonsoft.Json;
using ReserveBlockCore.Bitcoin.ElectrumX.Request;
using ReserveBlockCore.Bitcoin.ElectrumX.Response;
using ReserveBlockCore.Bitcoin.ElectrumX.Results;
using System.Drawing;
using System.Net.Security;
using System.Net.Sockets;
using System.Text;

namespace ReserveBlockCore.Bitcoin.ElectrumX
{
    public class Client : IDisposable
    {
        #region Variables
        public event Action<Client, int, string> OnError;
        protected readonly string Host;
        protected readonly int Port;
        protected readonly bool UseSsl;
        protected TcpClient TcpClient;
        protected SslStream SslStream;
        protected NetworkStream TcpStream;
        protected const int Buffersize = ushort.MaxValue;
        public static Version CurrentVersion;
        protected bool IsConnected;
        protected static NBitcoin.Network Network;

        public Client(string host, int port, bool useSsl = false)
        {
            Host = host;
            Port = port;
            UseSsl = useSsl;
            Network = Globals.BTCNetwork;               
        }

        public Client(ClientSettings settings) : this(settings.Host, settings.Port, settings.UseSsl) { }

        #endregion

        #region Connect/Disconnect/SSL Method

        protected async Task ConnectWithSsl()
        {
            if (IsConnected)
                return;
            try
            {
                TcpClient = new TcpClient();
                await TcpClient.ConnectAsync(Host, Port);
                SslStream = new SslStream(TcpClient.GetStream(), true,
                    (sender, certificate, chan, sslPolicy) => true);
                await SslStream.AuthenticateAsClientAsync(Host);
                IsConnected = true;
            }
            catch (SocketException ex)
            {
                Console.WriteLine(ex);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }
        protected async Task ConnectNoSsl()
        {
            if (IsConnected)
                return;
            try
            {
                TcpClient = new TcpClient();
                await TcpClient.ConnectAsync(Host, Port);
                TcpStream = TcpClient.GetStream();
                IsConnected = true;
            }
            catch (SocketException ex)
            {
                Console.WriteLine(ex);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }
        private async Task<string> SendMessageWithSsl(byte[] requestData)
        {
            string response;
            var buffer = new byte[Buffersize];
            await SslStream.WriteAsync(requestData, 0, requestData.Length);

            await using (var ms = new MemoryStream())
            {
                int read;
                while ((read = await SslStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    ms.Write(buffer, 0, read);
                    if (read < Buffersize) break;
                }
                response = Encoding.ASCII.GetString(ms.ToArray());
            }
            Disconnect();
            return response;
        }
        private async Task<string> SendMessageNoSsl(byte[] requestData)
        {
            var response = new StringBuilder();
            var buffer = new byte[Buffersize];
            await TcpStream.WriteAsync(requestData, 0, requestData.Length);
            var bytes = await TcpStream.ReadAsync(buffer, 0, buffer.Length);
            do
            {
                response.Append(Encoding.UTF8.GetString(buffer, 0, bytes));
            }
            while (TcpStream.DataAvailable);

            Disconnect();
            return response.ToString();
        }
        protected void Disconnect()
        {
            IsConnected = false;
            TcpStream?.Close();
            SslStream?.Close();
            TcpClient?.Close();

        }
        protected void Dispose(bool disposing)
        {
            if (!disposing) return;
            TcpClient?.Dispose();
            SslStream?.Dispose();
            TcpStream?.Dispose();
            Disconnect();
        }
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        public static string GetScriptHash(IDestination addr)
        {
            var witBytes = addr.ScriptPubKey.WitHash.ToBytes();
            Array.Reverse(witBytes);
            return witBytes.Aggregate("", (current, c) => current + $"{c:x2}");
        }

        #endregion

        #region Commands

        #region Broadcast
        public async Task<BlockchainTransactionBroadcastResult> Broadcast(string hexTx)
        {
            if (string.IsNullOrEmpty(hexTx))
                return new BlockchainTransactionBroadcastResult();
            var requestData = new BlockchainTransactionBroadcastRequest(hexTx).GetRequestData();
            var buff = "";
            try
            {
                if (UseSsl)
                {
                    await ConnectWithSsl();
                    buff = await SendMessageWithSsl(requestData);
                }
                else
                {
                    await ConnectNoSsl();
                    buff = await SendMessageNoSsl(requestData);
                }
            }
            catch
            {
                // ignored
            }
            var response = !string.IsNullOrEmpty(buff) ? JsonConvert.DeserializeObject<BlockchainTransactionBroadcastResponse>(buff) : null;
            if (response?.Error != null)
                OnError?.Invoke(this, response.Error.Code, response.Error.Message);
            return response != null ? response.GetResultModel() : new BlockchainTransactionBroadcastResult();
        }
        #endregion

        #region Create Address
        private static BitcoinAddress CreateBitcoinAddress(string address)
        {
            var prefix = "";

            return string.IsNullOrEmpty(address) ? null : BitcoinAddress.Create(prefix + address, Globals.BTCNetwork);
        }
        #endregion

        #region Get History
        public async Task<List<BlockchainScripthashGetHistoryResult>> GetHistory(IDestination address)
        {
            if (address == null)
                return new List<BlockchainScripthashGetHistoryResult>();
            var scriptHash = GetScriptHash(address);
            var requestData = new BlockchainScripthashGetHistoryRequest(scriptHash).GetRequestData();
            var buff = "";
            try
            {
                if (UseSsl)
                {
                    await ConnectWithSsl();
                    buff = await SendMessageWithSsl(requestData);
                }
                else
                {
                    await ConnectNoSsl();
                    buff = await SendMessageNoSsl(requestData);
                }
            }
            catch
            {
                // ignored
            }
            var response = !string.IsNullOrEmpty(buff) ? JsonConvert.DeserializeObject<BlockchainScripthashGetHistoryResponse>(buff) : null;
            if (response?.Error != null)
                OnError?.Invoke(this, response.Error.Code, response.Error.Message);
            return response != null ? response.GetResultModel() : new List<BlockchainScripthashGetHistoryResult>();
        }

        public async Task<List<BlockchainScripthashGetHistoryResult>> GetHistory(string scriptHash)
        {
            if (string.IsNullOrEmpty(scriptHash))
                return new List<BlockchainScripthashGetHistoryResult>();
            var requestData = new BlockchainScripthashGetHistoryRequest(scriptHash).GetRequestData();
            var buff = "";
            try
            {
                if (UseSsl)
                {
                    await ConnectWithSsl();
                    buff = await SendMessageWithSsl(requestData);
                }
                else
                {
                    await ConnectNoSsl();
                    buff = await SendMessageNoSsl(requestData);
                }
            }
            catch
            {
                // ignored
            }
            var response = !string.IsNullOrEmpty(buff) ? JsonConvert.DeserializeObject<BlockchainScripthashGetHistoryResponse>(buff) : null;
            if (response?.Error != null)
                OnError?.Invoke(this, response.Error.Code, response.Error.Message);
            return response != null ? response.GetResultModel() : new List<BlockchainScripthashGetHistoryResult>();
        }

        #endregion

        #region Get Confirms
        public async Task<int> GetConfirms(string txHash)
        {
            if (string.IsNullOrEmpty(txHash))
                return -1;
            var requestData = new BlockchainTransactionGetRequest(txHash, true).GetRequestData();
            var buff = "";
            try
            {
                if (UseSsl)
                {
                    await ConnectWithSsl();
                    buff = await SendMessageWithSsl(requestData);
                }
                else
                {
                    await ConnectNoSsl();
                    buff = await SendMessageNoSsl(requestData);
                }
            }
            catch
            {
                // ignored
            }
            var response = !string.IsNullOrEmpty(buff) ? JsonConvert.DeserializeObject<BlockchainTransactionGetConfirmsResponse>(buff) : null;
            if (response?.Error != null)
                OnError?.Invoke(this, response.Error.Code, response.Error.Message);
            return response?.GetResultModel() ?? -1;
        }
        #endregion

        #region Get Balance
        public async Task<BlockchainScripthashGetBalanceResult> GetBalance(string address, bool isLegacy)
        {
            var prefix = "";

            var bAddr = BitcoinAddress.Create(prefix + address, Globals.BTCNetwork);
            var requestData = new BlockchainScripthashGetBalance(GetScriptHash(bAddr)).GetRequestData();
            var buff = "";
            try
            {
                if (UseSsl)
                {
                    await ConnectWithSsl();
                    buff = await SendMessageWithSsl(requestData);
                }
                else
                {
                    await ConnectNoSsl();
                    buff = await SendMessageNoSsl(requestData);
                }
            }
            catch
            {
                // ignored
            }
            var response = !string.IsNullOrEmpty(buff) ? JsonConvert.DeserializeObject<BlockchainScripthashGetBalanceResponse>(buff) : null;
            if (response?.Error != null)
                OnError?.Invoke(this, response.Error.Code, response.Error.Message);
            return response != null ? response.GetResultModel() : new BlockchainScripthashGetBalanceResult();
        }

        public async Task<BlockchainScripthashGetBalanceResult> GetBalance(IDestination address)
        {
            var requestData = new BlockchainScripthashGetBalance(GetScriptHash(address)).GetRequestData();
            var buff = "";
            try
            {
                if (UseSsl)
                {
                    await ConnectWithSsl();
                    buff = await SendMessageWithSsl(requestData);
                }
                else
                {
                    await ConnectNoSsl();
                    buff = await SendMessageNoSsl(requestData);
                }
            }
            catch
            {
                // ignored
            }
            var response = !string.IsNullOrEmpty(buff) ? JsonConvert.DeserializeObject<BlockchainScripthashGetBalanceResponse>(buff) : null;
            if (response?.Error != null)
                OnError?.Invoke(this, response.Error.Code, response.Error.Message);
            return response != null ? response.GetResultModel() : new BlockchainScripthashGetBalanceResult();
        }
        #endregion

        #region Get List Unspent
        public async Task<List<BlockchainScripthashListunspentResult>> GetListUnspent(string address, bool isLegacy)
        {

            var bAddr = CreateBitcoinAddress(address);
            return await GetListUnspent(bAddr);
        }
        public async Task<List<BlockchainScripthashListunspentResult>> GetListUnspent(IDestination address)
        {
            var scriptHash = GetScriptHash(address);
            return await GetListUnspent(scriptHash);
        }

        public async Task<List<BlockchainScripthashListunspentResult>> GetListUnspent(string scriptHash)
        {
            var requestData = new BlockchainScripthashListunspentRequest(scriptHash).GetRequestData();
            var buff = "";
            try
            {
                if (UseSsl)
                {
                    await ConnectWithSsl();
                    buff = await SendMessageWithSsl(requestData);
                }
                else
                {
                    await ConnectNoSsl();
                    buff = await SendMessageNoSsl(requestData);
                }
            }
            catch
            {
                // ignored
            }
            var response = !string.IsNullOrEmpty(buff) ? JsonConvert.DeserializeObject<BlockchainScripthashListunspentResponse>(buff) : null;
            if (response?.Error != null)
                OnError?.Invoke(this, response.Error.Code, response.Error.Message);
            return response != null ? response.GetResultModel() : new List<BlockchainScripthashListunspentResult>();
        }
        #endregion

        #region Get Server Version
        public async Task<ServerVersionResult> GetServerVersion()
        {
            var requestData = new ServerVersionRequest().GetRequestData();
            var buff = "";
            try
            {
                if (UseSsl)
                {
                    await ConnectWithSsl();
                    buff = await SendMessageWithSsl(requestData);
                }
                else
                {
                    await ConnectNoSsl();
                    buff = await SendMessageNoSsl(requestData);
                }
            }
            catch
            {
                // ignored
            }

            var response = !string.IsNullOrEmpty(buff) ? JsonConvert.DeserializeObject<ServerVersionResponse>(buff) : null;
            if (response?.Error != null)
                OnError?.Invoke(this, response.Error.Code, response.Error.Message);
            return response != null ? response.GetResultModel() : new ServerVersionResult();
        }
        #endregion

        #endregion
    }
}
