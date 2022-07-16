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
                bool fileExist = File.Exists(GetPathUtility.GetBeaconPath() + fileName);
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
                return new BeaconResponse { Status = -1, Description = "Error: " + e.Message };
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
                string Selected_file = FilePath;
                string File_name = Path.GetFileName(Selected_file);
                FileStream fs = new FileStream(Selected_file, FileMode.Open);
                TcpClient tc = new TcpClient(TargetIP, Port);
                NetworkStream ns = tc.GetStream();
                byte[] data_tosend = CreateDataPacket(Encoding.UTF8.GetBytes("125"), Encoding.UTF8.GetBytes(File_name));
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
                                    ConsoleWriterService.Output($"{File_name} - Upload Progress: {ProgressValue}");
                                }
                                else
                                {
                                    byte[] data_to_send = CreateDataPacket(Encoding.UTF8.GetBytes("128"), Encoding.UTF8.GetBytes("Close"));
                                    ns.Write(data_to_send, 0, data_to_send.Length);
                                    ns.Flush();
                                    fs.Close();
                                    loop_break = true;
                                }
                                break;
                            default:
                                break;
                        }
                    }
                    if (loop_break == true)
                    {
                        ns.Close();
                        return new BeaconResponse { Status = 1, Description = "Send successful." };
                    }

                }
            }
            catch (Exception e)
            {
                return new BeaconResponse { Status = -1, Description = "Error: " + e.Message };
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
    }
}
