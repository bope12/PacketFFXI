using System;
using System.Net.Sockets;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Net;
using System.IO.Compression;

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
        static IPEndPoint RemoteIpEndPoint;
        static MD5 hasher = MD5.Create();
        static Blowfish tpzblowfish = new Blowfish();
        static UdpClient Gameserver;
        static uint[] startingkey = { 0x00000000, 0x00000000, 0x00000000, 0x00000000, 0xAD5DE056 };
        static int Packet_Head = 28;
        static UInt16 PDcode = 1;
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
        // Packet with key for blowfish
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
            uint zoneport = BitConverter.ToUInt16(data, 0x3C);
            uint searchip = BitConverter.ToUInt32(data, 0x40);
            uint searchport = BitConverter.ToUInt16(data, 0x44);
            RemoteIpEndPoint = new IPEndPoint(zoneip, Convert.ToInt32(zoneport));
            Console.WriteLine("Handed off to gameserver " + zoneip + ":" + zoneport);
            GameserverStart();
        }
        static void packet_addmd5(ref byte[] data)
        {
            byte[] tomd5 = new byte[data.Length - (Packet_Head + 16)];
            System.Buffer.BlockCopy(data, Packet_Head, tomd5, 0, tomd5.Length);
            tomd5 = hasher.ComputeHash(tomd5);
            System.Buffer.BlockCopy(tomd5, 0, data, data.Length - 16, 16);
        }
        static void GameserverStart()
        {
            Gameserver = new UdpClient();
            Gameserver.Connect(RemoteIpEndPoint);
            ThreadStart Incomingref = new ThreadStart(ParseIncomingPacket);
            Thread Incoming = new Thread(Incomingref);
            Incoming.Start();
            //Gameserver.Connect("127.0.0.1", Convert.ToInt32(zoneport));
            Logintozone();
        }
        static void ParseIncomingPacket()
        {

            while (true)
            {
                Byte[] receiveBytes = Gameserver.Receive(ref RemoteIpEndPoint);
                UInt16 server_packet_id = BitConverter.ToUInt16(receiveBytes, 0);
                UInt16 client_packet_id = BitConverter.ToUInt16(receiveBytes, 2);
                UInt32 packet_time = BitConverter.ToUInt32(receiveBytes, 8);
                Console.WriteLine("Incoming Packet Serverid:" + server_packet_id + " Clientid:" + client_packet_id + " sent at:" + packet_time + " RawSize:" + receiveBytes.Length);

                //Raw
                //Console.WriteLine("Incoming: " + BitConverter.ToString(receiveBytes).Replace("-", " "));

                byte[] deblown = new byte[receiveBytes.Length - Packet_Head];
                byte[] blowhelper;
                int k=0;
                for (int j = Packet_Head; j < receiveBytes.Length && receiveBytes.Length-j >= 8; j += 8)
                {
                    blowhelper = new byte[8];
                    uint l = BitConverter.ToUInt32(receiveBytes, j);
                    uint r = BitConverter.ToUInt32(receiveBytes, j + 4);
                    tpzblowfish.Blowfish_decipher(ref l, ref r);
                    System.Buffer.BlockCopy(BitConverter.GetBytes(l), 0, blowhelper, 0, 4);
                    System.Buffer.BlockCopy(BitConverter.GetBytes(r), 0, blowhelper, 4, 4);
                    System.Buffer.BlockCopy(blowhelper, 0, deblown, j-Packet_Head, 8);
                    k += 8;
                }
                System.Buffer.BlockCopy(deblown, 0, receiveBytes, Packet_Head, k);
                //Deblowfished
                //Console.WriteLine("Decode:   " + BitConverter.ToString(receiveBytes).Replace("-", " "));


                //TODO Unzlib the packet
                //Zlib compress's all but header


                //uint packetsize = BitConverter.ToUInt32(receiveBytes, receiveBytes.Length - 4 - 16); //Location of packetsize set by encoding by server
                //byte[] buffer = new byte[deblown.Length -4 - 16];
                //System.Buffer.BlockCopy(deblown, 0, buffer,0, buffer.Length);
                //DeflateStream decompress = new DeflateStream(new MemoryStream(buffer), CompressionMode.Decompress);


                //byte type = finaloutput.ToArray()[0];
                //byte size = finaloutput.ToArray()[1];
                //Console.WriteLine("Received packet:" + type + " Size:" + size);

            }
        }
        static void Logintozone()
        {
            startingkey[4] += 2;
            byte[] byteArray = new byte[startingkey.Length * 4];
            Buffer.BlockCopy(startingkey, 0, byteArray, 0, startingkey.Length * 4);
            Console.WriteLine("Blowfish key raw " + BitConverter.ToString(byteArray).Replace("-", ""));
            byte[] hashkey;
            hashkey = hasher.ComputeHash(byteArray);
            for (int i = 0; i < 16; ++i)
            {
                if (hashkey[i] == 0)
                {
                    byte[] zero = { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
                    System.Buffer.BlockCopy(zero, i, hashkey, i, 16 - i);
                }
            }
            Console.WriteLine("Blowfish hashed key " + BitConverter.ToString(hashkey).Replace(" - ", ""));
            tpzblowfish.Init(hashkey, 16);
            byte[] data = new byte[136];
            byte[] input = BitConverter.GetBytes(PDcode); //Packet count
            System.Buffer.BlockCopy(input, 0, data, 0, input.Length);
            input = BitConverter.GetBytes(((UInt16)0x0A)); //Packet type
            System.Buffer.BlockCopy(input, 0, data, Packet_Head, input.Length);
            input = new byte[] { 0x2E }; //Size
            System.Buffer.BlockCopy(input, 0, data, Packet_Head + 0x01, input.Length);
            input = BitConverter.GetBytes(PDcode); //Packet count
            System.Buffer.BlockCopy(input, 0, data, Packet_Head + 0x02, input.Length);
            input = BitConverter.GetBytes(charid);
            System.Buffer.BlockCopy(input, 0, data, Packet_Head + 0x0C, input.Length);
            packet_addmd5(ref data);
            Console.WriteLine("Sending a request to load into a zone");
            Gameserver.Send(data, data.Length);
            PDcode++;
            data = new byte[53];
            input = BitConverter.GetBytes(PDcode); //Packet count
            System.Buffer.BlockCopy(input, 0, data, 0, input.Length);
            input = BitConverter.GetBytes(((UInt16)0x11)); //Packet type
            System.Buffer.BlockCopy(input, 0, data, Packet_Head, input.Length);
            input = new byte[] { 0x04 }; //Size
            System.Buffer.BlockCopy(input, 0, data, Packet_Head + 0x01, input.Length);
            input = BitConverter.GetBytes(PDcode); //Packet count
            System.Buffer.BlockCopy(input, 0, data, Packet_Head + 0x02, input.Length);
            packet_addmd5(ref data);
            Console.WriteLine("Sending zone in confirmation");
            Gameserver.Send(data, data.Length);

            data = new byte[183];
            input = BitConverter.GetBytes(PDcode); //Packet count
            System.Buffer.BlockCopy(input, 0, data, 0, input.Length);

            input = BitConverter.GetBytes(((UInt16)0x0C)); //Packet type
            System.Buffer.BlockCopy(input, 0, data, Packet_Head, input.Length);
            input = new byte[] { 0x06 }; //Size
            System.Buffer.BlockCopy(input, 0, data, Packet_Head + 0x01, input.Length);
            input = BitConverter.GetBytes(PDcode); //Packet count
            System.Buffer.BlockCopy(input, 0, data, Packet_Head + 0x02, input.Length);
            int new_Head = Packet_Head + (0x06 * 2);
            
            input = BitConverter.GetBytes(((UInt16)0x61)); //Packet type
            System.Buffer.BlockCopy(input, 0, data, new_Head, input.Length);
            input = new byte[] { 0x04 }; //Size
            System.Buffer.BlockCopy(input, 0, data, new_Head + 0x01, input.Length);
            input = BitConverter.GetBytes(PDcode); //Packet count
            System.Buffer.BlockCopy(input, 0, data, new_Head + 0x02, input.Length);
            new_Head = new_Head + (0x04 * 2);

            input = BitConverter.GetBytes(((UInt16)0x01A)); //Packet type
            System.Buffer.BlockCopy(input, 0, data, new_Head, input.Length);
            input = new byte[] { 0x0E }; //Size
            System.Buffer.BlockCopy(input, 0, data, new_Head + 0x01, input.Length);
            input = BitConverter.GetBytes(PDcode); //Packet count
            System.Buffer.BlockCopy(input, 0, data, new_Head + 0x02, input.Length);
            input = new byte[] { 0x14}; //Action type
            System.Buffer.BlockCopy(input, 0, data, new_Head + 0x0A, input.Length);
            new_Head = new_Head + (0x0E * 2);

            input = BitConverter.GetBytes(((UInt16)0x4B)); //Packet type
            System.Buffer.BlockCopy(input, 0, data, new_Head, input.Length);
            input = new byte[] { 0x0C }; //Size
            System.Buffer.BlockCopy(input, 0, data, new_Head + 0x01, input.Length);
            input = BitConverter.GetBytes(PDcode); //Packet count
            System.Buffer.BlockCopy(input, 0, data, new_Head + 0x02, input.Length);
            input = new byte[] { 0x02,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00}; //Language,Timestamp,Lengh,Start offset
            System.Buffer.BlockCopy(input, 0, data, new_Head + 0x07, input.Length);
            new_Head = new_Head + (0x0C * 2);

            input = BitConverter.GetBytes(((UInt16)0x0F)); //Packet type
            System.Buffer.BlockCopy(input, 0, data, new_Head, input.Length);
            input = new byte[] { 0x12 }; //Size
            System.Buffer.BlockCopy(input, 0, data, new_Head + 0x01, input.Length);
            input = BitConverter.GetBytes(PDcode); //Packet count
            System.Buffer.BlockCopy(input, 0, data, new_Head + 0x02, input.Length);
            new_Head = new_Head + (0x12 * 2);

            //input = BitConverter.GetBytes(((UInt16)0x0DB)); //Packet type
            //System.Buffer.BlockCopy(input, 0, data, new_Head, input.Length);
            //input = new byte[] { 0x14 }; //Size
            //System.Buffer.BlockCopy(input, 0, data, new_Head + 0x01, input.Length);
            //input = BitConverter.GetBytes(PDcode); //Packet count
            //System.Buffer.BlockCopy(input, 0, data, new_Head + 0x02, input.Length);
            //input = new byte[] { 0x02 }; //Language
            //System.Buffer.BlockCopy(input, 0, data, new_Head + 0x24, input.Length);
            //new_Head = new_Head + (0x14 * 2);

            input = BitConverter.GetBytes(((UInt16)0x5A)); //Packet type
            System.Buffer.BlockCopy(input, 0, data, new_Head, input.Length);
            input = new byte[] { 0x02 }; //Size
            System.Buffer.BlockCopy(input, 0, data, new_Head + 0x01, input.Length);
            input = BitConverter.GetBytes(PDcode); //Packet count
            System.Buffer.BlockCopy(input, 0, data, new_Head + 0x02, input.Length);
            new_Head = new_Head + (0x02 * 2);

            packet_addmd5(ref data);
            Console.WriteLine("Sending Post zone data request");
            Gameserver.Send(data, data.Length);
            PDcode++;
            float pos = 0;
            bool plus = true;
            while(true)
            {
                data = new byte[74];
                input = BitConverter.GetBytes(PDcode); //Packet count
                System.Buffer.BlockCopy(input, 0, data, 0, input.Length);
                input = BitConverter.GetBytes(((UInt16)0x15)); //Packet type
                System.Buffer.BlockCopy(input, 0, data, Packet_Head, input.Length);
                input = new byte[] { 0x10 }; //Size
                System.Buffer.BlockCopy(input, 0, data, Packet_Head + 0x01, input.Length);
                input = BitConverter.GetBytes(PDcode); //Packet count
                System.Buffer.BlockCopy(input, 0, data, Packet_Head + 0x02, input.Length);
                input = BitConverter.GetBytes(pos);
                System.Buffer.BlockCopy(input, 0, data, Packet_Head + 0x04, input.Length); //Xpos
                System.Buffer.BlockCopy(input, 0, data, Packet_Head + 0x0C, input.Length); //Zpos
                input = new byte[] { 0x02 };
                System.Buffer.BlockCopy(input, 0, data, Packet_Head + 0x08, input.Length); //Ypos
                input = BitConverter.GetBytes((UInt16)2);
                System.Buffer.BlockCopy(input, 0, data, Packet_Head + 0x12, input.Length);//steps?
                input = plus ? new byte[] { 0xE7 } : new byte[] { 0x5A };
                System.Buffer.BlockCopy(input, 0, data, Packet_Head + 0x14, input.Length);//rot
                pos = plus ? pos + .2F : pos - .2F;
                if (pos > 3F || pos < -3f)
                    plus = !plus;
                Console.WriteLine("Line Dance");
                packet_addmd5(ref data);
                Gameserver.Send(data, data.Length);
                Thread.Sleep(750);
            }

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
