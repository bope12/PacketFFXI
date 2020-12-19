using System;
using System.Text.Encodings;
using System.Net.Sockets;
using System.IO;

namespace HeadlessFFXI
{
    class Program
    {
        static TcpClient lobbyview;
        static NetworkStream viewstream;
        static TcpClient lobbydata;
        static NetworkStream datastream;
        static uint actid;
        static uint charid;
        static string username;
        static string password;
        static int char_slot = 0;
        static uint zoneport;
        static void Main(string[] args)
        {
            if(File.Exists("config.cfg"))
            {
                string line;
                System.IO.StreamReader cfg = new System.IO.StreamReader("config.cfg");
                while((line = cfg.ReadLine()) != null)
                {
                    string[] setting = line.Split(":");
                    switch(setting[0])
                    {
                        case "username":
                            username = setting[1];
                            break;
                        case "password":
                            password = setting[1];
                            setting[1] = "********";
                            break;
                        case "char_slot":
                            char_slot = Int16.Parse(setting[1]) - 1;
                            break;
                    }
                    Console.WriteLine(setting[0] + " " + setting[1]);
                }
                if (username == null)
                {
                    Console.WriteLine("No username found in cfg");
                }
                if (password == null)
                {
                    Console.WriteLine("No password found in cfg");
                }
            }
            Console.Write("Attempting to login");
            try
            {
                TcpClient client = new TcpClient("127.0.0.1", 54231);
                NetworkStream stream = client.GetStream();
                Byte[] data = new Byte[33];
                System.Buffer.BlockCopy(System.Text.Encoding.ASCII.GetBytes(username), 0, data, 0, username.Length);
                System.Buffer.BlockCopy(System.Text.Encoding.ASCII.GetBytes(password), 0, data, 16, password.Length);
                data[32] = 0x10;
                stream.Write(data, 0, 33);
                data = new Byte[16];
                stream.Read(data, 0, 16);
                switch (data[0])
                {
                    case 0x0001:
                        Console.WriteLine(" ,Login passed");
                        actid = BitConverter.ToUInt32(data, 1);
                        Console.WriteLine("Account id:" + actid);
                        LobbyData();
                        LobbyView0x26();
                        break;
                    case 0x0002:
                        Console.WriteLine(" ,Login failed invalid user name or password");
                        break;
                    default:
                        Console.WriteLine(" ,Login failed Unsure Code:" + data[0]);
                        break;
                }
                stream.Close();
                client.Close();
            }
            catch (SocketException d)
            {
                switch (d.ErrorCode)
                {
                    case 10061:
                        Console.WriteLine(", Failed, No responce from server");
                        break;
                    default:
                        Console.WriteLine(" Error received:" + d.ErrorCode + ", " + d.Message);
                    break;
                }
            }
        }
        static void LobbyView0x26()
        {
            lobbyview = new TcpClient("127.0.0.1", 54001);
            viewstream = lobbyview.GetStream();
            Byte[] ver = System.Text.Encoding.ASCII.GetBytes("30201004_0");
            Byte[] data = new byte[152];
            data[8] = 0x26;
            System.Buffer.BlockCopy(ver, 0, data, 116, 10);
            viewstream.Write(data, 0, 152);
            data = new Byte[40];
            viewstream.Read(data, 0, 40);
            Console.WriteLine("Expantion Bitmask:" + BitConverter.ToUInt16(data, 32));
            Console.WriteLine("Feature Bitmask:" + BitConverter.ToUInt16(data, 36));
            LobbyView0x1F();
        }
        static void LobbyView0x1F()
        {
            Byte[] data = new byte[44];
            data[8] = 0x1F;
            viewstream.Write(data, 0, 44);
            LobbyData0xA1();
        }
        static void LobbyView0x24()
        {
            Byte[] data = new byte[44];
            data[8] = 0x24;
            viewstream.Write(data, 0, 44);
            data = new byte[64];
            viewstream.Read(data, 0, 64);
            Console.WriteLine("Server Name:" + System.Text.Encoding.UTF8.GetString(data, 36, 16));
            LobbyView0x07();
        }
        static void LobbyView0x07()
        {
            Byte[] data = new byte[88];
            data[8] = 0x07;
            Byte[] id = BitConverter.GetBytes(charid);
            System.Buffer.BlockCopy(id, 0, data, 28, id.Length);
            viewstream.Write(data, 0, 88);
            LobbyData0xA2();
        }
        static void LobbyData0xA2()
        {
            byte[] data = {0xA2,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x58,0xE0,0x5D,0xAD,0x00,0x00,0x00,0x00};
            datastream.Write(data,0,25);
            data = new byte[0x48];
            viewstream.Read(data, 0, 0x48);
            uint error = BitConverter.ToUInt16(data, 32);
            switch(error)
            {
                case 305:
                case 321:
                    Console.WriteLine("Login server failed to pass us off to the gameserver");
                    return;
                default:
                    break;
            }
            uint zoneip = BitConverter.ToUInt32(data, 0x38);
            zoneport = BitConverter.ToUInt16(data, 0x3C);
            uint searchip = BitConverter.ToUInt32(data, 0x40);
            uint searchport = BitConverter.ToUInt16(data, 0x44);
            Console.WriteLine("Handed off to gameserver " + zoneip + ":" + zoneport);
            GameserverStart();
        }
        static void GameserverStart()
        {
            var Packet_Head = 28;
            UdpClient Gameserver = new UdpClient("localhost", Convert.ToInt32(zoneport));
            Console.Read();
        }
        static void LobbyData()
        {
            lobbydata = new TcpClient("127.0.0.1", 54230);
            datastream = lobbydata.GetStream();
            Byte[] dat = new Byte[1];
            dat[0] = 0xA1;
            Byte[] id = BitConverter.GetBytes(actid);
            Byte[] data = new Byte[dat.Length + id.Length];
            System.Buffer.BlockCopy(dat, 0, data,0,dat.Length);
            System.Buffer.BlockCopy(id, 0, data, dat.Length, id.Length);
            datastream.Write(data, 0, data.Length);
        }
        static void LobbyData0xA1()
        {
            Byte[] data = new Byte[13];
            Byte[] ip = BitConverter.GetBytes(((uint)16777343));
            System.Buffer.BlockCopy(ip, 0, data, 0, ip.Length);
            //data[0] = 0xA1;
            datastream.Flush();
            datastream.Write(data);
            data = new byte[328];
            datastream.Read(data, 0, 328);           
            data = new byte[2272];
            viewstream.Read(data,0,2272);
            Console.WriteLine("Charid:" + BitConverter.ToUInt32(data,36 + (char_slot * 140)));
            charid = BitConverter.ToUInt32(data, 36 + (char_slot * 140));
            Console.WriteLine("Name:" +System.Text.Encoding.UTF8.GetString(data, 44 + (char_slot * 140), 16));
            /*ref<uint8>(CharList, 46 + 32 + i * 140) = MainJob;
            ref<uint8>(CharList, 73 + 32 + i * 140) = lvlMainJob;

            ref<uint8>(CharList, 44 + 32 + i * 140) = (uint8)Sql_GetIntData(SqlHandle, 5); // race;
            ref<uint8>(CharList, 56 + 32 + i * 140) = (uint8)Sql_GetIntData(SqlHandle, 6); // face;
            ref<uint16>(CharList, 58 + 32 + i * 140) = (uint16)Sql_GetIntData(SqlHandle, 7); // head;
            ref<uint16>(CharList, 60 + 32 + i * 140) = (uint16)Sql_GetIntData(SqlHandle, 8); // body;
            ref<uint16>(CharList, 62 + 32 + i * 140) = (uint16)Sql_GetIntData(SqlHandle, 9); // hands;
            ref<uint16>(CharList, 64 + 32 + i * 140) = (uint16)Sql_GetIntData(SqlHandle, 10); // legs;
            ref<uint16>(CharList, 66 + 32 + i * 140) = (uint16)Sql_GetIntData(SqlHandle, 11); // feet;
            ref<uint16>(CharList, 68 + 32 + i * 140) = (uint16)Sql_GetIntData(SqlHandle, 12); // main;
            ref<uint16>(CharList, 70 + 32 + i * 140) = (uint16)Sql_GetIntData(SqlHandle, 13); // sub;

            ref<uint8>(CharList, 72 + 32 + i * 140) = (uint8)zone;
            ref<uint16>(CharList, 78 + 32 + i * 140) = zone;*/
            LobbyView0x24();
        }
    }
}
