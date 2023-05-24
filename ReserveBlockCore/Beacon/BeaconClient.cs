using ReserveBlockCore.Services;
using ReserveBlockCore.Utilities;
using System.Net.Sockets;
using System.Text;

namespace ReserveBlockCore.Beacon
{
    public class BeaconClient
    {
        /// <summary>
        /// Percentage of the file being sent and its progress
        /// </summary>
        public static int ProgressValue;

        /// <summary>
        /// Receive the asset that is tied to the NFT
        /// </summary>
        /// <param name="fileName">Asset you are calling out for</param>
        /// <param name="TargetIP">IP Address of target beacon</param>
        /// <param name="Port">Server listening on this port</param>
        /// <returns>An object (BeaconResponse) with Status and Description</returns>
        public static BeaconResponse Receive(string fileName, string TargetIP, int Port, string scUID)
        {
            try
            {
                bool fileExist = File.Exists(NFTAssetFileUtility.CreateNFTAssetPath(fileName, scUID));
                if (fileExist)
                {
                    //do nothing
                    return new BeaconResponse { Status = -1, Description = "Error: " + "File already exist." };
                }
                else
                {
                    FileStream fs = null;
                    fs = new FileStream(NFTAssetFileUtility.CreateNFTAssetPath(fileName, scUID), FileMode.CreateNew);
                    long current_file_pointer = 0;
                    TcpClient tc = new TcpClient(TargetIP, Port);
                    NetworkStream ns = tc.GetStream();
                    byte[] data_tosend = CreateDataPacket(Encoding.UTF8.GetBytes("224"), Encoding.UTF8.GetBytes(fileName));
                    ns.Write(data_tosend, 0, data_tosend.Length);
                    ns.Flush();
                    bool loop_break = false;
                    while (true)
                    {
                        if (ns.ReadByte() == 2)
                        {
                            byte[] cmd_buffer = new byte[3];
                            ns.Read(cmd_buffer, 0, cmd_buffer.Length);
                            byte[] recv_data = ReadStream(ns);
                            switch (Convert.ToInt32(Encoding.UTF8.GetString(cmd_buffer)))
                            {
                                case 225:
                                    byte[] data_to_send = CreateDataPacket(Encoding.UTF8.GetBytes("226"), Encoding.UTF8.GetBytes(Convert.ToString(current_file_pointer)));
                                    ns.Write(data_to_send, 0, data_to_send.Length);
                                    ns.Flush();
                                    break;

                                case 227:
                                    fs.Seek(current_file_pointer, SeekOrigin.Begin);
                                    fs.Write(recv_data, 0, recv_data.Length);
                                    current_file_pointer = fs.Position;
                                    byte[] data_to_sends = CreateDataPacket(Encoding.UTF8.GetBytes("226"), Encoding.UTF8.GetBytes(Convert.ToString(current_file_pointer)));
                                    ns.Write(data_to_sends, 0, data_to_sends.Length);
                                    ns.Flush();
                                    break;

                                case 228:
                                    {
                                        fs.Close();
                                        loop_break = true;
                                    }
                                    break;
                                default:
                                    break;

                            }
                        }
                        else
                        {
                            loop_break = true;
                            ns.Close();
                            return new BeaconResponse { Status = -1, Description = "Error: " + "File already exist." };
                        }
                        if (loop_break == true)
                        {
                            ns.Close();
                            return new BeaconResponse { Status = 1, Description = "Receive successful." };
                        }
                    }
                }

            }
            catch (Exception e)
            {
                return new BeaconResponse { Status = -1, Description = "Error: " + e.ToString() };
            }
        }

        /// <summary>
        /// Send the asset that is tied to the NFT
        /// </summary>
        /// <param name="FilePath">Asset location that you want to send</param>
        /// <param name="TargetIP">IP Address of target beacon</param>
        /// <param name="Port">Server listening on this port</param>
        /// <returns>An object (BeaconResponse) with Status and Description</returns>
        public static BeaconResponse Send(string FilePath, string TargetIP, int Port)
        {
            try
            {
                BeaconLogUtility.Log("Beginning Beacon Asset Transfer", "BeaconClient.Send()");
                string Selected_file = FilePath;
                string File_name = Path.GetFileName(Selected_file);
                BeaconLogUtility.Log($"Sending File: {File_name}", "BeaconClient.Send()");
                FileStream fs = new FileStream(Selected_file, FileMode.Open);
                TcpClient tc = new TcpClient(TargetIP, Port);
                NetworkStream ns = tc.GetStream();
                byte[] data_tosend = CreateDataPacket(Encoding.UTF8.GetBytes("125"), Encoding.UTF8.GetBytes(File_name));
                ns.Write(data_tosend, 0, data_tosend.Length);
                ns.Flush();
                bool loop_break = false;
                bool loop_break_file_exist = false;
                int progValueRec = 0;
                while (true)
                {
                    if (ns.ReadByte() == 2)
                    {
                        byte[] cmd_buffer = new byte[3];
                        ns.Read(cmd_buffer, 0, cmd_buffer.Length);
                        byte[] recv_data = ReadStream(ns);
                        switch (Convert.ToInt32(Encoding.UTF8.GetString(cmd_buffer)))
                        {
                            case 126:
                                long recv_file_pointer = long.Parse(Encoding.UTF8.GetString(recv_data));
                                if (recv_file_pointer != fs.Length)
                                {
                                    fs.Seek(recv_file_pointer, SeekOrigin.Begin);
                                    int temp_buffer_length = (int)(fs.Length - recv_file_pointer < 20000 ? fs.Length - recv_file_pointer : 20000);
                                    byte[] temp_buffer = new byte[temp_buffer_length];
                                    fs.Read(temp_buffer, 0, temp_buffer.Length);
                                    byte[] data_to_send = CreateDataPacket(Encoding.UTF8.GetBytes("127"), temp_buffer);
                                    ns.Write(data_to_send, 0, data_to_send.Length);
                                    ns.Flush();
                                    ProgressValue = (int)Math.Ceiling((double)recv_file_pointer / (double)fs.Length * 100);
                                    if(ProgressValue > progValueRec)
                                    {
                                        ConsoleWriterService.Output($"{File_name} - Upload Progress: {ProgressValue}%");
                                        BeaconLogUtility.Log($"{File_name} - Upload Progress: {ProgressValue}%", "BeaconClient.Send()");
                                        progValueRec = ProgressValue;
                                    }
                                }
                                else
                                {
                                    byte[] data_to_send = CreateDataPacket(Encoding.UTF8.GetBytes("128"), Encoding.UTF8.GetBytes("Close"));
                                    ns.Write(data_to_send, 0, data_to_send.Length);
                                    ns.Flush();
                                    fs.Close();
                                    loop_break = true;
                                    progValueRec = 0;
                                    BeaconLogUtility.Log($"{File_name} - Upload Closing. Code 128", "BeaconClient.Send()");
                                }
                                break;
                            case 777:
                                BeaconLogUtility.Log($"{File_name} - Already Exist. Code 777.", "BeaconClient.Send()");
                                ns.Flush();
                                fs.Close();
                                progValueRec = 0;
                                loop_break_file_exist = true;
                                break;
                            default:
                                break;
                        }
                    }
                    else
                    {
                        BeaconLogUtility.Log($"{File_name} - NS.ReadBytes != 2. Error.", "BeaconClient.Send()");
                        loop_break = true;
                        ns.Close();
                        return new BeaconResponse { Status = -1, Description = "Error: " + "Error sending asset." };
                    }
                    if (loop_break == true)
                    {
                        progValueRec = 0;
                        ns.Close();
                        return new BeaconResponse { Status = 1, Description = "Send successful." };
                    }
                    if (loop_break_file_exist == true)
                    {
                        progValueRec = 0;
                        ns.Close();
                        return new BeaconResponse { Status = 777, Description = "File Already Exist" };
                    }

                }
            }
            catch (Exception ex)
            {
                BeaconLogUtility.Log($"Unknown Error: {ex.ToString()}", "BeaconClient.Send()");
                return new BeaconResponse { Status = -1, Description = "Error: " + ex.ToString() };
            }
        }

        private static byte[] ReadStream(NetworkStream ns)
        {
            byte[] data_buff = null;

            int b = 0;
            string buff_Length = "";
            while ((b = ns.ReadByte()) != 4)
            {
                buff_Length += (char)b;
            }
            int data_Length = Convert.ToInt32(buff_Length);
            data_buff = new byte[data_Length];
            int byte_Read = 0;
            int byte_Offset = 0;
            while (byte_Offset < data_Length)
            {
                byte_Read = ns.Read(data_buff, byte_Offset, data_Length - byte_Offset);
                byte_Offset += byte_Read;
            }

            return data_buff;
        }

        private static byte[] CreateDataPacket(byte[] cmd, byte[] data)
        {
            byte[] initialize = new byte[1];
            initialize[0] = 2;
            byte[] separator = new byte[1];
            separator[0] = 4;
            byte[] dataLength = Encoding.UTF8.GetBytes(Convert.ToString(data.Length));
            MemoryStream ms = new MemoryStream();
            ms.Write(initialize, 0, initialize.Length);
            ms.Write(cmd, 0, cmd.Length);
            ms.Write(dataLength, 0, dataLength.Length);
            ms.Write(separator, 0, separator.Length);
            ms.Write(data, 0, data.Length);

            return ms.ToArray();
        }

        //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        //New Method Sending
        //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        public static async Task<BeaconResponse> Send_New(string FilePath, string TargetIP, int Port, string scUID)
        {
            BeaconLogUtility.Log("Beginning Beacon Asset Transfer", "BeaconClient.Send()");
            string Selected_file = FilePath;
            string File_name = Path.GetFileName(Selected_file);
            BeaconLogUtility.Log($"Sending File: {File_name}", "BeaconClient.Send()");

            string serverIpAddress = TargetIP;
            int serverPort = Port;

            using (var client = new TcpClient())
            {
                await client.ConnectAsync(serverIpAddress, serverPort);
                Console.WriteLine($"Connected to server: {client.Client.RemoteEndPoint}");

                // Prepare the request
                var request = new Request(RequestType.Upload, File_name, scUID);

                // Send the request to the server
                await SendRequest(client.GetStream(), request);

                // Upload the file to the server
                Console.WriteLine("Uploading file to the server...");
                await SendFile(client.GetStream(), Selected_file);
                Console.WriteLine("File uploaded successfully!");

                // Optionally, you can receive a response from the server after uploading the file
                var response = await ReceiveResponse(client.GetStream());
                Console.WriteLine("Response received: " + response.Message);

                if(response != null)
                {
                    if(response.ResponseType== ResponseType.Success)
                    {
                        return new BeaconResponse { Status = 1, Description = "Success"};
                    }
                    else
                    {
                        return new BeaconResponse { Status = -1, Description = response.Message };
                    }
                }
            }

            return new BeaconResponse { Status = -1, Description = "Fail" };
        }

        public static async Task<BeaconResponse> Receive_New(string fileName, string TargetIP, int Port, string scUID)
        {
            bool fileExist = File.Exists(NFTAssetFileUtility.CreateNFTAssetPath(fileName, scUID));
            if (fileExist)
            {
                //do nothing
                return new BeaconResponse { Status = -1, Description = "Error: " + "File already exist." };
            }
            string serverIpAddress = TargetIP;
            int serverPort = Port;
            var saveArea = NFTAssetFileUtility.CreateNFTAssetPath(fileName, scUID);
            var scuidFolder = scUID.Replace(":", "");

            using (var client = new TcpClient())
            {
                await client.ConnectAsync(serverIpAddress, serverPort);
                Console.WriteLine($"Connected to server: {client.Client.RemoteEndPoint}");

                // Prepare the request
                var request = new Request(RequestType.Download, fileName, scUID);

                //perform file check
                var extChkResult = CheckExtension(fileName);
                if (!extChkResult)
                {
                    //Extension found in reject list
                    return new BeaconResponse { Status = -1, Description = "Bad Extension Type" };
                }

                // Send the request to the server
                await SendRequest(client.GetStream(), request);

                // Upload the file to the server
                Console.WriteLine("Requesting file to the server...");
                await ReceiveFile(client.GetStream(), saveArea, request.UniqueId);
                Console.WriteLine("File downloaded successfully!");
                return new BeaconResponse { Status = 1, Description = "Success" };
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
            Download
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

        private static async Task SendResponse(NetworkStream stream, Response response)
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

            public Response(string responseString, string? message = null)
            {
                string[] responseParts = responseString.Split('|');
                ResponseType = Enum.Parse<ResponseType>(responseParts[0]);
                Message = responseParts[1];
            }
        }
        private static async Task SendRequest(NetworkStream stream, Request request)
        {
            string requestString = $"{request.RequestType}|{request.FileName}|{request.UniqueId}###";
            byte[] buffer = Encoding.ASCII.GetBytes(requestString);
            await stream.WriteAsync(buffer, 0, buffer.Length);
        }

        private static async Task ReceiveFile(NetworkStream stream, string filePath, string uniqueId)
        {
            using (var fileStream = File.Create($"{filePath}"))
            {
                byte[] buffer = new byte[8192]; // Specify the desired buffer size
                int bytesRead;

                while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
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

        private static int IndexOfEndMarker(byte[] buffer, int length)
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

        private static async Task<Response> ReceiveResponse(NetworkStream stream)
        {
            StringBuilder sb = new StringBuilder();
            byte[] buffer = new byte[1];
            string delimiter = "###";

            while (true)
            {
                int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                char receivedChar = (char)buffer[0];
                sb.Append(receivedChar);

                if (sb.ToString().EndsWith(delimiter))
                {
                    sb.Length -= delimiter.Length;
                    break;
                }
            }

            string responseString = sb.ToString();
            return new Response(responseString);
        }
    }
}
