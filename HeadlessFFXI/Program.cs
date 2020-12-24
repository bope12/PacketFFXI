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
        static string loginserver = "127.0.0.1";
        static TcpClient lobbyview;
        static NetworkStream viewstream;
        static TcpClient lobbydata;
        static NetworkStream datastream;
        static IPEndPoint RemoteIpEndPoint;
        static UdpClient Gameserver;
        static MD5 hasher = MD5.Create();
        static UInt16 PDcode = 1;
        static uint[] startingkey = { 0x00000000, 0x00000000, 0x00000000, 0x00000000, 0xAD5DE056 };
        static Blowfish tpzblowfish;
        static AccountInfo Account_Data; 
        static My_Player Player_Data;
        const int Packet_Head = 28;
        static void Main(string[] args)
        {
            if(args.Length == 6 || args.Length == 8)
            { 
                for(int i=0;i<=args.Length/2;i+=2)
                {
                    string s = args[i];
                    switch(s)
                    {
                        case "-user":
                            Account_Data.Username = args[i+1];
                            break;
                        case "-pass":
                            Account_Data.Password = args[i + 1];
                            break;
                        case "-slot":
                            Account_Data.Char_Slot = Int16.Parse(args[i + 1]) - 1;
                            break;
                        case "-server":
                            loginserver = args[i + 1];
                            break;
                    }
                    Console.WriteLine(args[i] + " " + args[i+1]);
                }    
            }
            else if(File.Exists("config.cfg"))
            {
                string line;
                System.IO.StreamReader cfg = new System.IO.StreamReader("config.cfg");
                while((line = cfg.ReadLine()) != null)
                {
                    string[] setting = line.Split(":");
                    switch(setting[0])
                    {
                        case "username":
                            Account_Data.Username = setting[1];
                            break;
                        case "password":
                            Account_Data.Password = setting[1];
                            setting[1] = "********";
                            break;
                        case "char_slot":
                            Account_Data.Char_Slot = Int16.Parse(setting[1]) - 1;
                            break;
                        case "server":
                            loginserver = setting[1];
                            break;
                    }
                    Console.WriteLine(setting[0] + " " + setting[1]);
                }
                if (Account_Data.Username == null)
                {
                    Console.WriteLine("No username found in cfg");
                }
                if (Account_Data.Password == null)
                {
                    Console.WriteLine("No password found in cfg");
                }
            }
            else
            {
                Console.WriteLine("No login information provided, Move config file into folder with exe or add launch args with -user user -pass pass -slot #");
            }
            Account_Login();
        }
        #region Loginproccess
        static void Account_Login()
        {
            Console.Write("Attempting to login ");
            try
            {
                TcpClient client = new TcpClient(loginserver, 54231);
                NetworkStream stream = client.GetStream();
                Byte[] data = new Byte[33];
                System.Buffer.BlockCopy(System.Text.Encoding.ASCII.GetBytes(Account_Data.Username), 0, data, 0, Account_Data.Username.Length);
                System.Buffer.BlockCopy(System.Text.Encoding.ASCII.GetBytes(Account_Data.Password), 0, data, 16, Account_Data.Password.Length);
                data[32] = 0x10;
                stream.Write(data, 0, 33);
                byte[] indata = new Byte[16];
                stream.Read(indata, 0, 16);
                switch (indata[0])
                {
                    case 0x0001: //Login Success
                        Console.WriteLine(",Login passed");
                        Account_Data.ID = BitConverter.ToUInt32(indata, 1);
                        Console.WriteLine("Account id:{0:D}", Account_Data.ID);
                        LobbyData();
                        LobbyView0x26();
                        break;
                    case 0x0002:
                        Console.WriteLine(",Login failed.Trying to create the account");
                        Account_Creation(data);
                        break;
                    default:
                        Console.WriteLine(" ,Login failed Unsure Code:" + indata[0]);
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
        static void Account_Creation(byte[] data)
        {
            TcpClient client = new TcpClient(loginserver, 54231);
            NetworkStream stream = client.GetStream();
            data[32] = 0x20;
            stream.Write(data, 0, 33);
            byte[] indata = new Byte[1];
            stream.Read(indata, 0, 1);
            switch (indata[0])
            {
                case 0x03: //Account creation success
                    Console.WriteLine("New account created");
                    Account_Login();
                    break;
                case 0x04: //Acount already exists
                    Console.WriteLine("Account already exists, Check your username/password");
                    break;
                case 0x08: //Account creation disabled
                    Console.WriteLine("Account creation is disabled, If your account already exists check your username/password");
                    break;
                case 0x09: //Acount creation error
                    Console.WriteLine("Server failed to create a new account");
                    break;
                default:
                    break;
            }
        }
        //Setup connection to the lobbyData handler
        static void LobbyData()
        {
            lobbydata = new TcpClient(loginserver, 54230);
            datastream = lobbydata.GetStream();
            Byte[] dat = new Byte[1];
            dat[0] = 0xA1;
            Byte[] id = BitConverter.GetBytes(Account_Data.ID);
            Byte[] data = new Byte[dat.Length + id.Length];
            System.Buffer.BlockCopy(dat, 0, data, 0, dat.Length);
            System.Buffer.BlockCopy(id, 0, data, dat.Length, id.Length);
            datastream.Write(data, 0, data.Length);
        }
        //The rest of the login process is a linary set of send receive packets with both the lobbyData and lobbyView connections
        //Client ver check, receive information about account
        static void LobbyView0x26()
        {
            lobbyview = new TcpClient(loginserver, 54001);
            viewstream = lobbyview.GetStream();
            Byte[] ver = System.Text.Encoding.ASCII.GetBytes("30201004_0");
            Byte[] data = new byte[152];
            data[8] = 0x26;
            System.Buffer.BlockCopy(ver, 0, data, 116, 10);
            viewstream.Write(data, 0, 152);
            data = new Byte[40];
            viewstream.Read(data, 0, 40);
            Console.WriteLine("Expantion Bitmask:{0:D}", BitConverter.ToUInt16(data, 32));
            Console.WriteLine("Feature Bitmask:{0:D}", BitConverter.ToUInt16(data, 36));
            LobbyView0x1F();
        }
        static void LobbyView0x1F()
        {
            Byte[] data = new byte[44];
            data[8] = 0x1F;
            viewstream.Write(data, 0, 44);
            LobbyData0xA1();
        }
        static void LobbyData0xA1()
        {
            Byte[] data = new Byte[13];
            Byte[] ip = BitConverter.GetBytes(((uint)16777343)); //This does not seem to really matter
            System.Buffer.BlockCopy(ip, 0, data, 0, ip.Length);
            //data[0] = 0xA1;
            datastream.Flush();
            datastream.Write(data);
            data = new byte[328];
            datastream.Read(data, 0, 328);
            data = new byte[2272];
            viewstream.Read(data, 0, 2272);
            Player_Data.ID = BitConverter.ToUInt32(data, 36 + (Account_Data.Char_Slot * 140));
            Player_Data.Name = System.Text.Encoding.UTF8.GetString(data, 44 + (Account_Data.Char_Slot * 140), 16);
            Player_Data.Job = data[46 + 32 + (Account_Data.Char_Slot * 140)];
            Player_Data.Level = data[73 + 32 + (Account_Data.Char_Slot * 140)];
            Player_Data.zone = data[72 + 32 + (Account_Data.Char_Slot * 140)];

            Console.WriteLine("Name:{0:G} CharID:{1:D} Job:{2:D} Level:{3:D}", new object[] { Player_Data.Name, Player_Data.ID, Player_Data.Job, Player_Data.Level });
            /*
            ref<uint8>(CharList, 44 + 32 + i * 140) = (uint8)Sql_GetIntData(SqlHandle, 5); // race;
            ref<uint8>(CharList, 56 + 32 + i * 140) = (uint8)Sql_GetIntData(SqlHandle, 6); // face;
            ref<uint16>(CharList, 58 + 32 + i * 140) = (uint16)Sql_GetIntData(SqlHandle, 7); // head;
            ref<uint16>(CharList, 60 + 32 + i * 140) = (uint16)Sql_GetIntData(SqlHandle, 8); // body;
            ref<uint16>(CharList, 62 + 32 + i * 140) = (uint16)Sql_GetIntData(SqlHandle, 9); // hands;
            ref<uint16>(CharList, 64 + 32 + i * 140) = (uint16)Sql_GetIntData(SqlHandle, 10); // legs;
            ref<uint16>(CharList, 66 + 32 + i * 140) = (uint16)Sql_GetIntData(SqlHandle, 11); // feet;
            ref<uint16>(CharList, 68 + 32 + i * 140) = (uint16)Sql_GetIntData(SqlHandle, 12); // main;
            ref<uint16>(CharList, 70 + 32 + i * 140) = (uint16)Sql_GetIntData(SqlHandle, 13); // sub;
            ref<uint16>(CharList, 78 + 32 + i * 140) = zone;
            */


            LobbyView0x24();
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
            Byte[] id = BitConverter.GetBytes(Player_Data.ID);
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
        #endregion
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
            tpzblowfish = new Blowfish();
            tpzblowfish.Init(hashkey, 16);

            #region ZoneInpackets
            byte[] data = new byte[136];
            byte[] input = BitConverter.GetBytes(PDcode); //Packet count
            System.Buffer.BlockCopy(input, 0, data, 0, input.Length);
            input = BitConverter.GetBytes(((UInt16)0x0A)); //Packet type
            System.Buffer.BlockCopy(input, 0, data, Packet_Head, input.Length);
            input = new byte[] { 0x2E }; //Size
            System.Buffer.BlockCopy(input, 0, data, Packet_Head + 0x01, input.Length);
            input = BitConverter.GetBytes(PDcode); //Packet count
            System.Buffer.BlockCopy(input, 0, data, Packet_Head + 0x02, input.Length);
            input = BitConverter.GetBytes(Player_Data.ID);
            System.Buffer.BlockCopy(input, 0, data, Packet_Head + 0x0C, input.Length);
            packet_addmd5(ref data);
            Console.WriteLine("Sending a request to load into a zone");
            try
            {
                Gameserver.Send(data, data.Length);
            }
            catch(SocketException d)
            {
                Console.WriteLine("Failed to connect to the gameserver retrying");
                startingkey[4] -= 2;
                Logintozone();
            }
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
            #endregion
            #region RequestCharInfo
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
            #endregion
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
                Console.WriteLine("Outgoing Packet: Line dancing Pos:{0:G}",pos);
                packet_addmd5(ref data);
                Gameserver.Send(data, data.Length);
                Thread.Sleep(750);
            }

            Console.Read();
        }

    }
    #region Structs
    struct My_Player
    {
        public uint ID;
        public string Name;
        public byte Job;
        public byte Level;
        public byte zone;
        public Position pos;
    }
    struct Position
    {
        public float X;
        public float Y;
        public float Z;
        public byte Rot;
    }
   struct AccountInfo
   {
        public uint ID;
        public int Char_Slot;
        public string Username;
        public string Password;
   }
    #endregion
}
