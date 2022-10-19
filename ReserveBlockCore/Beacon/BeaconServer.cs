using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;
using ReserveBlockCore.Models;
using ReserveBlockCore.Utilities;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace ReserveBlockCore.Beacon
{
    public class BeaconServer
    {
        public string SaveTo;
        public int Port;
        TcpListener obj_server;


        public BeaconServer(string SaveTo, int Port)
        {
            this.SaveTo = SaveTo;
            this.Port = Port;
            obj_server = new TcpListener(IPAddress.Any, Port);
        }

        /// <summary>
        /// Start the Beacon Server and listening on p2p port
        /// </summary>
        public ThreadStart StartServer()
        {
            obj_server.Start();
            while (true)
            {
                TcpClient tc = obj_server.AcceptTcpClient();
                SocketHandler obj_handler = new SocketHandler(tc, SaveTo);

                Thread obj_thread = new Thread(obj_handler.ProcessSocketRequest);
                obj_thread.Start();
            }
        }

    }

    class SocketHandler
    {
        NetworkStream ns;
        string SaveTo;
        public SocketHandler(TcpClient tc, string SaveTo)
        {
            this.SaveTo = SaveTo;
            ns = tc.GetStream();
        }

        public void ProcessSocketRequest()
        {
            FileStream fs = null;
            FileStream fse = null;
            long current_file_pointer = 0;
            bool loop_break = false;
            string fileName = "";
            var ip_address = ns.Socket.RemoteEndPoint != null ? ((IPEndPoint)ns.Socket.RemoteEndPoint).Address.ToString() : "NA";
            while (true)
            {
                //byte[] readPbValue = ReadStream();
                try
                {
                    if (ns.ReadByte() == 2)
                    {
                        byte[] cmd_buffer = new byte[3];
                        ns.Read(cmd_buffer, 0, cmd_buffer.Length);
                        byte[] recv_data = ReadStream();

                        switch (Convert.ToInt32(Encoding.UTF8.GetString(cmd_buffer)))
                        {
                            case 125:
                                {
                                    bool fileExist = File.Exists(@"" + SaveTo + Encoding.UTF8.GetString(recv_data));
                                    if (fileExist)
                                    {
                                        byte[] data_file_exist = CreateDataPacket(Encoding.UTF8.GetBytes("777"), Encoding.UTF8.GetBytes(Convert.ToString(current_file_pointer)));
                                        ns.Write(data_file_exist, 0, data_file_exist.Length);
                                        ns.Flush();
                                        loop_break = true;
                                        break;
                                    }
                                    fileName = Encoding.UTF8.GetString(recv_data);
                                    //perform file check
                                    var extChkResult = CheckExtensionApproval(fileName);
                                    if (!extChkResult)
                                    {
                                        //Extension found in reject list
                                        ns.Flush();
                                        loop_break = true;
                                        break;
                                    }
                                    var beaconData = BeaconData.GetBeaconData();
                                    if (beaconData != null)
                                    {
                                        var authCheck = beaconData.Exists(x => x.IPAdress == ip_address && x.AssetName == fileName);
                                        if (!authCheck)
                                        {
                                            ns.Flush();
                                            loop_break = true;
                                            break;
                                        }
                                        else
                                        {
                                            var _beaconData = beaconData.Where(x => x.IPAdress == ip_address && x.AssetName == fileName).FirstOrDefault();
                                            if (_beaconData != null)
                                            {
                                                _beaconData.AssetReceiveDate = TimeUtil.GetTime();//received today
                                                _beaconData.AssetExpireDate = TimeUtil.GetTimeForBeaconRelease(); //expires in 5 days
                                                var beaconDatas = BeaconData.GetBeacon();
                                                if (beaconDatas != null)
                                                {
                                                    beaconDatas.UpdateSafe(_beaconData);
                                                }
                                            }

                                        }
                                    }

                                    fs = new FileStream(@"" + SaveTo + fileName, FileMode.CreateNew);
                                    byte[] data_to_send = CreateDataPacket(Encoding.UTF8.GetBytes("126"), Encoding.UTF8.GetBytes(Convert.ToString(current_file_pointer)));
                                    ns.Write(data_to_send, 0, data_to_send.Length);
                                    ns.Flush();
                                }
                                break;
                            case 127:
                                {
                                    fs.Seek(current_file_pointer, SeekOrigin.Begin);
                                    fs.Write(recv_data, 0, recv_data.Length);
                                    current_file_pointer = fs.Position;
                                    byte[] data_to_send = CreateDataPacket(Encoding.UTF8.GetBytes("126"), Encoding.UTF8.GetBytes(Convert.ToString(current_file_pointer)));
                                    ns.Write(data_to_send, 0, data_to_send.Length);
                                    ns.Flush();
                                    long size = fs.Length;
                                    var currentSize = size.ToSize(GenericExtensions.SizeUnits.MB);
                                    if (currentSize > 150M)
                                    {
                                        loop_break = true;//current size of file is greater than 150 mb and will be deleted now.
                                        try
                                        {
                                            fs.Close();
                                            File.Delete(@"" + SaveTo + fileName);
                                            break;
                                        }
                                        catch
                                        {
                                            break;
                                        }

                                    }
                                }
                                break;
                            case 128:
                                {
                                    fs.Close();
                                    loop_break = true;
                                }
                                break;
                            case 224:
                                bool fileExistLoc = File.Exists(@"" + SaveTo + Encoding.UTF8.GetString(recv_data));
                                if (!fileExistLoc)
                                {
                                    loop_break = true;
                                    byte[] data_to_send = CreateDataPacket(Encoding.UTF8.GetBytes("228"), Encoding.UTF8.GetBytes("Close"));
                                    ns.Write(data_to_send, 0, data_to_send.Length);
                                    ns.Flush();
                                    if (fse != null)
                                    {
                                        fse.Close();
                                    }
                                    break;
                                }
                                string Selected_file = (@"" + SaveTo + Encoding.UTF8.GetString(recv_data));
                                string File_name = Path.GetFileName(Selected_file);

                                var beaconDataDb = BeaconData.GetBeacon();
                                if (beaconDataDb != null)
                                {
                                    var bdd = beaconDataDb.FindOne(x => x.AssetName.ToLower() == File_name.ToLower() && x.DownloadIPAddress == ip_address);
                                    if (bdd == null)
                                    {
                                        loop_break = true;
                                        byte[] data_to_send = CreateDataPacket(Encoding.UTF8.GetBytes("228"), Encoding.UTF8.GetBytes("Close"));
                                        ns.Write(data_to_send, 0, data_to_send.Length);
                                        ns.Flush();
                                        if (fse != null)
                                        {
                                            fse.Close();
                                        }
                                        break;
                                    }
                                    else
                                    {
                                        bdd.AssetReceiveDate = TimeUtil.GetTime();
                                        beaconDataDb.UpdateSafe(bdd);
                                    }
                                }

                                if (fse == null)
                                {
                                    fse = new FileStream(Selected_file, FileMode.Open);

                                }

                                byte[] data_to_sends = CreateDataPacket(Encoding.UTF8.GetBytes("225"), Encoding.UTF8.GetBytes(Convert.ToString(current_file_pointer)));
                                ns.Write(data_to_sends, 0, data_to_sends.Length);
                                ns.Flush();
                                break;

                            case 226:
                                long recv_file_pointer = long.Parse(Encoding.UTF8.GetString(recv_data));
                                if (recv_file_pointer != fse.Length)
                                {
                                    fse.Seek(recv_file_pointer, SeekOrigin.Begin);
                                    int temp_buffer_length = (int)(fse.Length - recv_file_pointer < 20000 ? fse.Length - recv_file_pointer : 20000);
                                    byte[] temp_buffer = new byte[temp_buffer_length];
                                    fse.Read(temp_buffer, 0, temp_buffer.Length);
                                    byte[] data_to_send = CreateDataPacket(Encoding.UTF8.GetBytes("227"), temp_buffer);
                                    ns.Write(data_to_send, 0, data_to_send.Length);
                                    ns.Flush();
                                }
                                else
                                {
                                    byte[] data_to_send = CreateDataPacket(Encoding.UTF8.GetBytes("228"), Encoding.UTF8.GetBytes("Close"));
                                    ns.Write(data_to_send, 0, data_to_send.Length);
                                    ns.Flush();
                                    fse.Close();
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
                        break;
                    }
                }
                catch(Exception ex)
                {
                    ErrorLogUtility.LogError($"Error in Beacon Server. Error: {ex.Message}", "BeaconServer.ProcessSocketRequest()");
                    try
                    {
                        File.Delete(@"" + SaveTo + fileName);
                        break;
                    }
                    catch { }
                }
                
            }
        }

        public static void SendFile()
        {
            
        }

        public byte[] ReadStream()
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

        private byte[] CreateDataPacket(byte[] cmd, byte[] data)
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

        private bool CheckExtensionApproval(string fileName)
        {
            bool output = false;

            string ext = Path.GetExtension(fileName);

            if(!string.IsNullOrEmpty(ext))
            {
                var rejectedExtList = Globals.RejectAssetExtensionTypes;
                var exist = rejectedExtList.Contains(ext);                
                if(!exist)
                    output = true;
            }
            return output;
        }

    }
}
