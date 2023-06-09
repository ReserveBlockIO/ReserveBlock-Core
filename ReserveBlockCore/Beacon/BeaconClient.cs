using ReserveBlockCore.Services;
using ReserveBlockCore.Utilities;
using System.Net.Sockets;
using System.Security.Cryptography;
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

            using (var client = Globals.HttpClientFactory.CreateClient())
            {
                try
                {
                    // Create a new MultipartFormDataContent
                    using (var formData = new MultipartFormDataContent())
                    {
                        // Read the file as a stream
                        using (var fileStream = File.OpenRead(Selected_file))
                        {
                            // Create a StreamContent from the file stream
                            var fileContent = new StreamContent(fileStream);

                            // Add the file content to the form data
                            formData.Add(fileContent, "file", Path.GetFileName(Selected_file));
                            string url = $"http://{serverIpAddress}:{serverPort}/upload/{scUID}";
                            // Send the POST request to the API endpoint
                            var response = await client.PostAsync(url, formData);

                            // Check if the request was successful (status code 200)
                            if (response.IsSuccessStatusCode)
                            {
                                var responseContent = await response.Content.ReadAsStringAsync();
                                Console.WriteLine("File uploaded successfully!");
                                Console.WriteLine("Server response: " + responseContent);
                               
                                return new BeaconResponse { Status = 1, Description = "Success" };
                            }
                            else
                            {
                                Console.WriteLine("File upload failed. Status code: " + response.StatusCode);
                                return new BeaconResponse { Status = -1, Description = $"Fail. Reason: {response.StatusCode}" };
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("An error occurred: " + ex.Message);
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

            using (var client = Globals.HttpClientFactory.CreateClient())
            {
                try
                {
                    //perform file check
                    var extChkResult = CheckExtension(fileName);
                    if (!extChkResult)
                    {
                        //Extension found in reject list
                        return new BeaconResponse { Status = -1, Description = "Bad Extension Type" };
                    }

                    string url = $"http://{serverIpAddress}:{serverPort}/download/{scUID}/{fileName}";

                    var response = await client.GetAsync(url);

                    if (!response.IsSuccessStatusCode)
                    {
                        Console.WriteLine($"File download failed. Status code: {response.StatusCode}");
                        return new BeaconResponse { Status = -1, Description = "Failed" };
                    }

                    using (var fileStream = new FileStream(saveArea, FileMode.Create))
                    {
                        await response.Content.CopyToAsync(fileStream);
                    }

                    Console.WriteLine("File downloaded successfully!");
                    return new BeaconResponse { Status = 1, Description = "Success" };
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"An error occurred while downloading the file: {ex.Message}");
                }
                return new BeaconResponse { Status = -1, Description = "Fail" };
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
    }
}
