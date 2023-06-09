using ReserveBlockCore.Models;
using ReserveBlockCore.Utilities;
using System.Net.Sockets;
using System.Net;
using System.Text;
using ReserveBlockCore.Beacon;

namespace ReserveBlockCore.Beacon
{
    public class BeaconServerFast
    {
        public static async Task StartBeaconServer()
        {
            try
            {
                if (Globals.SelfBeacon?.SelfBeaconActive == true)
                {
                    var builder = Host.CreateDefaultBuilder()
                    .ConfigureWebHostDefaults(webBuilder =>
                    {
                        webBuilder.UseKestrel(options =>
                        {
                            options.ListenAnyIP(Globals.Port + 1 + 20000);

                        })
                        .UseStartup<BeaconStartup>()
                        .ConfigureLogging(loggingBuilder => loggingBuilder.ClearProviders());
                    });

                    _ = builder.RunConsoleAsync();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }

        private static bool CheckExtension(string fileName)
        {
            bool output = false;

            string ext = Path.GetExtension(fileName);

            if (!string.IsNullOrEmpty(ext))
            {
                var rejectedExtList = Globals.RejectAssetExtensionTypes;
                var exist = rejectedExtList.Contains(ext);
                if (!exist)
                    output = true;
            }
            return output;
        }
        enum RequestType
        {
            Upload,
            Download,
            Ping
        }

        class Request
        {
            public RequestType RequestType { get; }
            public string FileName { get; }
            public string UniqueId { get; }

            public Request(RequestType requestType, string fileName, string uniqueId)
            {
                RequestType = requestType;
                FileName = fileName;
                UniqueId = uniqueId;
            }
        }

        static async Task ProcessClient(TcpClient client)
        {
            using (client)
            {
                Console.WriteLine($"Client connected: {client.Client.RemoteEndPoint}");

                var saveArea = GetPathUtility.GetBeaconPath();
                var request = await ReceiveRequest(client.GetStream());

                if (request == null)
                    return;

                var ip_address = client.Client.RemoteEndPoint != null ? ((IPEndPoint)client.Client.RemoteEndPoint).Address.ToString() : "NA";
                var fileName = request.FileName;
                var scUID = request.UniqueId;

                var scuidFolder = request.UniqueId.Replace(":", "");

                if (request.RequestType == RequestType.Ping)
                {
                    var randomNum = request.UniqueId;
                    var response = new Response(ResponseType.Success, $"Hello: {ip_address} - UniqueId: {randomNum}");
                    await SendResponse(client.GetStream(), response);
                    return;
                }

                if (request.RequestType == RequestType.Upload)
                {
                    Console.WriteLine($"Receiving file from {client.Client.RemoteEndPoint}...");

                   

                    if (!Directory.Exists($@"{saveArea}{scuidFolder}{Path.DirectorySeparatorChar}"))
                        Directory.CreateDirectory($@"{saveArea}{scuidFolder}{Path.DirectorySeparatorChar}");

                    
                    //perform file check
                    var extChkResult = CheckExtension(fileName);
                    if (!extChkResult)
                    {
                        //Extension found in reject list
                        var failResponse = new Response(ResponseType.Failure, "Bad Extension Type.");
                        await SendResponse(client.GetStream(), failResponse);
                        return;
                    }

                    bool fileExist = File.Exists($@"{saveArea}{scuidFolder}{Path.DirectorySeparatorChar}{request.FileName}");
                    if (fileExist)
                    {
                        var failResponse = new Response(ResponseType.Success, "Success. File Already Exist");
                        await SendResponse(client.GetStream(), failResponse);
                        return;
                    }

                    var beaconData = BeaconData.GetBeaconData();
                    if (beaconData != null)
                    {
                        var authCheck = beaconData.Exists(x => x.IPAdress == ip_address && x.AssetName == fileName);
                        if (!authCheck)
                        {
                            var failResponse = new Response(ResponseType.Failure, "Bad Authorization Type");
                            await SendResponse(client.GetStream(), failResponse);
                            return;
                        }
                        else
                        {
                            var _beaconData = beaconData.Where(x => x.IPAdress == ip_address && x.AssetName == fileName).FirstOrDefault();
                            if (_beaconData != null)
                            {
                                await ReceiveFile(client.GetStream(), $@"{saveArea}{scuidFolder}{Path.DirectorySeparatorChar}{request.FileName}", request.UniqueId);
                                Console.WriteLine($"File received from {client.Client.RemoteEndPoint} successfully!");

                                // Send a response back to the client
                                var response = new Response(ResponseType.Success, "File uploaded successfully");
                                await SendResponse(client.GetStream(), response);

                                _beaconData.AssetReceiveDate = TimeUtil.GetTime();//received today
                                _beaconData.AssetExpireDate = TimeUtil.GetTimeForBeaconRelease(); //expires in 5 days
                                var beaconDatas = BeaconData.GetBeacon();
                                if (beaconDatas != null)
                                {
                                    beaconDatas.UpdateSafe(_beaconData);
                                }

                                return;
                            }

                        }
                    }
                    else
                    {
                        var failResponse = new Response(ResponseType.Failure, "Beacon Data was Null.");
                        await SendResponse(client.GetStream(), failResponse);
                        return;
                    }
                }
                else if (request.RequestType == RequestType.Download)
                {
                    //make sure its the correct person and what not.
                    //check if file exist
                    //create unique id + asset name to find.
                    var filePath = $@"{saveArea}{scuidFolder}{Path.DirectorySeparatorChar}{request.FileName}";
                    bool fileExist = File.Exists(filePath);
                    if (!fileExist)
                    {
                        var failResponse = new Response(ResponseType.Failure, "File Does Not Exist.");
                        await SendResponse(client.GetStream(), failResponse);
                        return;
                    }

                    var beaconDataDb = BeaconData.GetBeacon();
                    if (beaconDataDb != null)
                    {
                        var bdd = beaconDataDb.FindOne(x => x.AssetName.ToLower() == fileName.ToLower() && x.DownloadIPAddress == ip_address && x.SmartContractUID == scUID);
                        if (bdd != null)
                        {
                            Console.WriteLine($"Sending requested file: {request.FileName} to {client.Client.RemoteEndPoint}...");
                            await SendFile(client.GetStream(), filePath);
                            Console.WriteLine($"File sent to {client.Client.RemoteEndPoint} successfully!");
                        }
                        else
                        {
                            var failResponse = new Response(ResponseType.Failure, "Beacon Data Record was not found.");
                            await SendResponse(client.GetStream(), failResponse);
                            return;
                        }
                    }
                    else
                    {
                        var failResponse = new Response(ResponseType.Failure, "Beacon Data Was Null.");
                        await SendResponse(client.GetStream(), failResponse);
                        return;
                    }

                }
            }
        }

        static async Task SendResponse(NetworkStream stream, Response response)
        {
            string responseString = $"{response.ResponseType}|{response.Message}###";
            byte[] responseBytes = Encoding.UTF8.GetBytes(responseString);
            await stream.WriteAsync(responseBytes, 0, responseBytes.Length);
        }

        enum ResponseType
        {
            Success,
            Failure
        }

        class Response
        {
            public ResponseType ResponseType { get; }
            public string Message { get; }

            public Response(ResponseType responseType, string message)
            {
                ResponseType = responseType;
                Message = message;
            }
        }

        static async Task<Request> ReceiveRequest(NetworkStream stream)
        {
            StringBuilder sb = new StringBuilder();
            byte[] buffer = new byte[1];
            string delimiter = "###"; // Define a delimiter that will indicate the end of the unique identifier

            // Read the request string character by character until the delimiter is encountered
            while (true)
            {
                int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                char receivedChar = (char)buffer[0];
                sb.Append(receivedChar);

                if (sb.ToString().EndsWith(delimiter))
                {
                    sb.Length -= delimiter.Length; // Remove the delimiter from the end
                    break;
                }
            }

            string requestString = sb.ToString();

            string[] requestParts = requestString.Split('|');
            RequestType requestType = Enum.Parse<RequestType>(requestParts[0]);
            string fileName = requestParts[1];
            string uniqueId = requestParts[2];

            return new Request(requestType, fileName, uniqueId);
        }

        static async Task ReceiveFile(NetworkStream stream, string filePath, string uniqueId)
        {
            try
            {
                using (var fileStream = File.Create($"{filePath}"))
                {
                    byte[] buffer = new byte[8192]; // Specify the desired buffer size
                    int bytesRead;

                    while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length).WaitAsync(new TimeSpan(0,0,1))) > 0)
                    {
                        // Check if the received data contains the end marker
                        int endMarkerIndex = IndexOfEndMarker(buffer, bytesRead);
                        if (endMarkerIndex != -1)
                        {
                            // Write the portion of the buffer before the end marker to the file
                            await fileStream.WriteAsync(buffer, 0, endMarkerIndex);
                            break; // File transfer complete, exit the loop
                        }

                        // Write the entire buffer to the file
                        await fileStream.WriteAsync(buffer, 0, bytesRead);
                    }
                }
            }
            catch
            {
                if(File.Exists(filePath))
                    File.Delete(filePath);
            }
            
        }

        static int IndexOfEndMarker(byte[] buffer, int length)
        {
            string endMarker = "END_OF_FILE_TRANSFER";
            int markerLength = endMarker.Length;

            for (int i = 0; i <= length - markerLength; i++)
            {
                bool found = true;
                for (int j = 0; j < markerLength; j++)
                {
                    if (buffer[i + j] != endMarker[j])
                    {
                        found = false;
                        break;
                    }
                }

                if (found)
                    return i;
            }

            return -1;
        }

        static async Task SendFile(NetworkStream stream, string filePath)
        {
            using (var fileStream = File.OpenRead(filePath))
            {
                byte[] buffer = new byte[8192]; // Specify the desired buffer size
                int bytesRead;

                while ((bytesRead = await fileStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    await stream.WriteAsync(buffer, 0, bytesRead);
                }

                // Send a marker indicating the end of the file transfer
                byte[] endMarker = Encoding.ASCII.GetBytes("END_OF_FILE_TRANSFER");
                await stream.WriteAsync(endMarker, 0, endMarker.Length);
            }
        }
    }
}
