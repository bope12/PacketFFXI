using System;
using System.Net.Sockets;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Net;
using System.Text;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using System.Runtime.InteropServices;

namespace HeadlessFFXI
{
    public class Client
    {
        #region Vars
        const int Packet_Head = 28;
        uint[] startingkey = { 0x00000000, 0x00000000, 0x00000000, 0x00000000, 0xAD5DE056 }; // ;
        public bool chardata;
        public bool silient;
        Blowfish tpzblowfish;
        AccountInfo Account_Data;
        public My_Player Player_Data;
        public Entity[] Entity_List = new Entity[4096];
        zlib myzlib;
        string loginserver = "127.0.0.1";
        TcpClient lobbyview;
        NetworkStream viewstream;
        TcpClient lobbydata;
        NetworkStream datastream;
        IPEndPoint RemoteIpEndPoint;
        UdpClient Gameserver;
        static MD5 hasher = System.Security.Cryptography.MD5.Create();
        UInt16 PDcode = 1;
        UInt16 ClientPacketID = 1;
        UInt16 ServerPacketID = 1;
        public event EventHandler IncomingChat;
        Thread Incoming;
        Thread Logic;
        bool abort = false;
        public bool Connected = false;
        private readonly PacketHandlerRegistry _registry = new();
        #endregion
        #region Loginproccess
        public Client(Config cfg, bool Full = true, bool log = false)
        {
            Account_Data.Username = cfg.user;
            Account_Data.Password = cfg.password;
            Account_Data.Char_Slot = cfg.char_slot;
            loginserver = cfg.server;
            chardata = Full;
            silient = log;
        }
        public async Task<bool> Login()
        {
            Console.SetOut(new TimestampTextWriter(Console.Out));
            myzlib = new zlib();
            myzlib.Init();
            //Console.WriteLine("[Info]Attempting to login");
            try
            {
                //AppContext.SetSwitch("System.Net.Security.UseNetworkFramework", true);
                TcpClient client = new TcpClient(loginserver, 54231);

                using var sslStream = new SslStream(
                client.GetStream(),
                leaveInnerStreamOpen: false,
                userCertificateValidationCallback: (sender, certificate, chain, sslPolicyErrors) => true // ⚠️ Only for testing!
                );

                Byte[] data = new Byte[102];
                data[0x00] = 0xFF;
                data[0x01] = 0;
                data[0x02] = 0;
                data[0x03] = 0;
                data[0x04] = 0;
                data[0x05] = 0;
                data[0x06] = 0;
                data[0x07] = 0;
                data[0x08] = 0;
                System.Buffer.BlockCopy(System.Text.Encoding.ASCII.GetBytes(Account_Data.Username), 0, data, 0x09, Account_Data.Username.Length);
                System.Buffer.BlockCopy(System.Text.Encoding.ASCII.GetBytes(Account_Data.Password), 0, data, 0x19, Account_Data.Password.Length);

                System.Buffer.BlockCopy(System.Text.Encoding.ASCII.GetBytes(Account_Data.Password), 0, data, 0x30, Account_Data.Password.Length);
                data[0x39] = 0x10;

                System.Buffer.BlockCopy(System.Text.Encoding.ASCII.GetBytes("1.1.1"), 0, data, 0x61, 5);
                var options = new SslClientAuthenticationOptions
                {
                    TargetHost = loginserver,
                    EnabledSslProtocols = SslProtocols.Tls13, // <-- This is the key line
                    CertificateRevocationCheckMode = X509RevocationMode.NoCheck
                };

                try
                {
                    await sslStream.AuthenticateAsClientAsync(options);
                    Console.WriteLine($"Connected using {sslStream.SslProtocol}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Handshake failed: {ex.GetType().Name} - {ex.Message}");
                    if (ex.InnerException != null)
                        Console.WriteLine($"Inner: {ex.InnerException.GetType().Name} - {ex.InnerException.Message}");
                }

                await sslStream.WriteAsync(data, 0, 102);
                await sslStream.FlushAsync();

                byte[] indata = new Byte[21];
                int bytesRead = await sslStream.ReadAsync(indata.AsMemory(0, 21));
                sslStream.Close();

                switch (indata[0])
                {
                    case 0x0001: //Login Success
                        //Console.WriteLine("[Login]Logged In");
                        Account_Data.ID = BitConverter.ToUInt32(indata, 1);
                        if (!silient)
                            Console.WriteLine("[Info]Account id:{0:D}", Account_Data.ID);
                        Account_Data.SessionHash = new byte[16];
                        System.Buffer.BlockCopy(indata, 5, Account_Data.SessionHash, 0, 16);
                        lobbydata = new TcpClient(AddressFamily.InterNetwork);
                        await lobbydata.ConnectAsync(loginserver, 54230);
                        datastream = lobbydata.GetStream();
                        Byte[] dataByte = new Byte[28];
                        dataByte[0] = 0xFE;
                        System.Buffer.BlockCopy(Account_Data.SessionHash, 0, dataByte, 12, Account_Data.SessionHash.Length);
                        await datastream.WriteAsync(dataByte, 0, dataByte.Length);

                        await LobbyDataA1();
                        await LobbyView0x26();
                        await LobbyView0x1F();
                        await LobbyData0xA1();
                        await LobbyView0x24();
                        await LobbyView0x07();
                        await LobbyData0xA2();
                        break;
                    case 0x0002:
                        if (!silient)
                            Console.WriteLine("[Login]Login failed,Trying to create the account");
                        await AccountCreation(data);
                        break;
                    default:
                        if (!silient)
                            Console.WriteLine("[Login]Login failed Unsure Code:" + indata[0]);
                        break;
                }
                client.Close();
            }
            catch (SocketException d)
            {
                switch (d.ErrorCode)
                {
                    case 10061:
                        if (!silient)
                            Console.WriteLine("[Login]No responce from server");
                        break;
                    default:
                        if (!silient)
                            Console.WriteLine("[Login]SocketError received:" + d.ErrorCode + ", " + d.Message);
                        break;
                }
            }
            return false;
        }
        async Task AccountCreation(byte[] data)
        {
            TcpClient client = new TcpClient(loginserver, 54231);
            NetworkStream stream = client.GetStream();
            data[32] = 0x20;
            await stream.WriteAsync(data, 0, 33);
            byte[] indata = new Byte[1];
            await stream.ReadAsync(indata, 0, 1);
            switch (indata[0])
            {
                case 0x03: //Account creation success
                    if (!silient)
                        Console.WriteLine("[Login]New account created");
                    Login();
                    break;
                case 0x04: //Acount already exists
                    if (!silient)
                        Console.WriteLine("[Login]Account already exists, Check your username/password");
                    break;
                case 0x08: //Account creation disabled
                    if (!silient)
                        Console.WriteLine("[Login]Account creation is disabled, If your account already exists check your username/password");
                    break;
                case 0x09: //Acount creation error
                    if (!silient)
                        Console.WriteLine("[Login]Server failed to create a new account");
                    break;
                default:
                    break;
            }
        }

        //Setup connection to the lobbyData handler
        async Task LobbyDataA1()
        {
            byte[] data = new byte[28];
            data[0] = 0xA1;
            System.Buffer.BlockCopy(BitConverter.GetBytes(Account_Data.ID), 0, data, 1, 4);
            System.Buffer.BlockCopy(((System.Net.IPEndPoint)lobbydata.Client.RemoteEndPoint).Address.GetAddressBytes(), 0, data, 5, 4);
            System.Buffer.BlockCopy(Account_Data.SessionHash, 0, data, 12, Account_Data.SessionHash.Length);
            datastream.Write(data, 0, data.Length);
        }

        //The rest of the login process is a linary set of send receive packets with both the lobbyData and lobbyView connections
        //Client ver check, receive information about account
        async Task LobbyView0x26()
        {
            lobbyview = new TcpClient(AddressFamily.InterNetwork);
            await lobbyview.ConnectAsync(loginserver, 54001);
            viewstream = lobbyview.GetStream();

            Byte[] ver = System.Text.Encoding.ASCII.GetBytes("30250800_0");
            Byte[] data = new byte[152];
            data[8] = 0x26;
            System.Buffer.BlockCopy(ver, 0, data, 116, 10);
            System.Buffer.BlockCopy(Account_Data.SessionHash, 0, data, 12, Account_Data.SessionHash.Length);
            await viewstream.WriteAsync(data, 0, 152);
            data = new Byte[40];
            await viewstream.ReadAsync(data, 0, 40);
            //Console.WriteLine("[Info]Expantion Bitmask:{0:D}", BitConverter.ToUInt16(data, 32));
            //Console.WriteLine("[Info]Feature Bitmask:{0:D}", BitConverter.ToUInt16(data, 36));
        }

        async Task LobbyView0x1F()
        {
            Byte[] data = new byte[44];
            data[8] = 0x1F;
            System.Buffer.BlockCopy(Account_Data.SessionHash, 0, data, 12, Account_Data.SessionHash.Length);
            await viewstream.WriteAsync(data, 0, 44);
        }

        //Request char list
        async Task LobbyData0xA1()
        {
            Byte[] data = new Byte[28];
            System.Buffer.BlockCopy(BitConverter.GetBytes(Account_Data.ID), 0, data, 1, 4);
            System.Buffer.BlockCopy(((System.Net.IPEndPoint)lobbydata.Client.RemoteEndPoint).Address.GetAddressBytes(), 0, data, 5, 4);
            data[0] = 0xA1;
            //datastream.Flush();
            await datastream.WriteAsync(data);
            data = new byte[328];
            await datastream.ReadAsync(data, 0, 328);
            data = new byte[2272];
            await viewstream.ReadAsync(data, 0, 2272);
            if (BitConverter.ToUInt32(data, 36) != 0)
            {
                if (BitConverter.ToUInt32(data, 36 + (Account_Data.Char_Slot * 140)) != 0)
                {
                    Player_Data.ID = BitConverter.ToUInt32(data, 36 + (Account_Data.Char_Slot * 140));
                    Player_Data.Name = System.Text.Encoding.UTF8.GetString(data, 44 + (Account_Data.Char_Slot * 140), 16).TrimEnd('\0');
                    Player_Data.Job = data[46 + 32 + (Account_Data.Char_Slot * 140)];
                    Player_Data.Level = data[73 + 32 + (Account_Data.Char_Slot * 140)];
                    Player_Data.zoneid = data[72 + 32 + (Account_Data.Char_Slot * 140)];
                }
                else
                {
                    if (!silient)
                        Console.WriteLine("[Info]No charater in slot:{0:G} defaulting to slot 1", (Account_Data.Char_Slot + 1));
                    Account_Data.Char_Slot = 0;
                    Player_Data.ID = BitConverter.ToUInt32(data, 36);
                    string name = System.Text.Encoding.UTF8.GetString(data, 44, 16).TrimEnd('\0');
                    Player_Data.Name = name.Substring(0, name.IndexOfAny(new char[] { ' ', '0', '1', '2', '3', '4', '5', '6', '7', '8', '9' }));
                    Player_Data.Job = data[46 + 32];
                    Player_Data.Level = data[73 + 32];
                    Player_Data.zoneid = data[72 + 32];
                }
                if (!silient)
                    Console.WriteLine("[Info]Name:{0:G} CharID:{1:D} Job:{2:D} Level:{3:D}", new object[] { Player_Data.Name, Player_Data.ID, Player_Data.Job, Player_Data.Level });
            }
            else
            {
                //Create a charater
                if (!silient)
                    Console.WriteLine("[Login]No charater's on account");
                LobbyView0x22();
            }
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
        }

        void LobbyView0x22()
        {
            byte[] data = new byte[48];
            data[8] = 0x22;
            byte[] input = System.Text.Encoding.ASCII.GetBytes(Account_Data.Username.Length > 16 ? Account_Data.Username.Substring(0, 16) : Account_Data.Username);
            System.Buffer.BlockCopy(input, 0, data, 32, input.Length);
            viewstream.Write(data, 0, data.Length);
            data = new byte[0x24 + 16];
            viewstream.Read(data, 0, data.Length);
            if (BitConverter.ToInt16(data, 32) == 313)
            {
                if (!silient)
                    Console.WriteLine("[Login]Name taken or invalid");
            }
            else
            {
                LobbyView0x21();
            }
        }

        //Second part of creating a charater
        void LobbyView0x21()
        {
            byte[] data = new byte[64];
            data[8] = 0x21;
            data[48] = 0; //Race
            data[50] = 1; //Job
            data[54] = 2; //Nation
            data[57] = 1; //Size
            data[60] = 1; //Face
            viewstream.Write(data, 0, data.Length);
            data = new byte[64];
            viewstream.Read(data, 0, 64);
            if (data[0] == 0x20)
            {
                if (!silient)
                    Console.WriteLine("[Login]Char created");
                LobbyView0x1F();
            }
            else
            {
                if (!silient)
                    Console.WriteLine("[Login]Failed to create a char exiting");
                Exit();
            }
        }

        async Task LobbyView0x24()
        {
            /*
            byte[] data = new byte[44];
            data[8] = 0x24;
            try
            {
                Console.WriteLine("[Login]0x24 out");
                viewstream.Write(data, 0, 44);
            }
            catch(Exception e)
            {
                Console.WriteLine("[Login]0x24 Error in write");
            }
            data = new byte[64];
            try
            {
                Console.WriteLine("[Login]0x24 in1");
                int bytesreturned = await viewstream.ReadAsync(data, 0, 64);
                Console.WriteLine("[Login]0x24 in2");
            }
            catch (Exception e)
            {
                Console.WriteLine("[Login]0x24 Error in read");
            }
            if (!silient)
                Console.WriteLine("[Info]{0:G} Server", System.Text.Encoding.UTF8.GetString(data, 36, 16));
            */
        }

        async Task LobbyView0x07()
        {
            Byte[] data = new byte[88];
            data[8] = 0x07;
            Byte[] id = BitConverter.GetBytes(Player_Data.ID);
            System.Buffer.BlockCopy(id, 0, data, 28, id.Length);
            System.Buffer.BlockCopy(System.Text.Encoding.ASCII.GetBytes(Player_Data.Name), 0, data, 36, Player_Data.Name.Length);
            System.Buffer.BlockCopy(Account_Data.SessionHash, 0, data, 12, Account_Data.SessionHash.Length);
            await viewstream.WriteAsync(data, 0, 88);
        }

        // Packet with key for blowfish
        async Task LobbyData0xA2()
        {
            viewstream.Flush();
            //Starting Key
            byte[] data = {
                0xA2, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x58, 0xE0, 0x5D, 0xAD, 0x00, //,
                0x00, 0x00, 0x00 };
            Thread.Sleep(2000);
            await datastream.WriteAsync(data, 0, 25);
            data = new byte[72];
            await viewstream.ReadExactlyAsync(data.AsMemory(0, 72));
            Console.WriteLine("LobbyData0xA2 received:");
            Console.WriteLine(BitConverter.ToString(data, 0, 72));
            uint error = BitConverter.ToUInt16(data, 32);
            switch (error)
            {
                case 305:
                case 321:
                case 201:
                    if (!silient)
                        Console.WriteLine("[Login]Failed to pass us to the gameserver: " + error);
                    return;
                default:
                    Connected = true;
                    break;
            }
            uint zoneip = BitConverter.ToUInt32(data, 0x38);
            uint zoneport = BitConverter.ToUInt16(data, 0x3C);
            uint searchip = BitConverter.ToUInt32(data, 0x40);
            uint searchport = BitConverter.ToUInt16(data, 0x44);
            RemoteIpEndPoint = new IPEndPoint(zoneip, Convert.ToInt32(zoneport));
            if (!silient)
                Console.WriteLine("[Login]Handed off to gameserver " + RemoteIpEndPoint.Address + ":" + zoneport);
            GameserverStart();
        }
        #endregion
        #region Packet Helpers
        static void packet_addmd5(ref byte[] data)
        {
            byte[] tomd5 = new byte[data.Length - (Packet_Head + 16)];
            System.Buffer.BlockCopy(data, Packet_Head, tomd5, 0, tomd5.Length);
            tomd5 = hasher.ComputeHash(tomd5);
            System.Buffer.BlockCopy(tomd5, 0, data, data.Length - 16, 16);
        }

        static void packet_SetType(ref byte[] data, UInt16 typeIn)
        {
            ushort type = (ushort)(typeIn & 0x1FF);
            data[Packet_Head] = (byte)(type & 0xFF);   // lower 8 bits of type
        }

        static void packet_SetSize(ref byte[] data, UInt16 sizeIn)
        {
            byte size = (byte)(sizeIn & 0xFE);
            data[Packet_Head + 1] = (byte)(size & 0xFE); // second byte replaced with size
        }

        public void packet_Compress(ref byte[] data)
        {
            byte[] buffer = new byte[1800];
            byte[] input = new byte[data.Length - Packet_Head];
            System.Buffer.BlockCopy(data, Packet_Head, input, 0, data.Length - Packet_Head);
            //Lets Compress this packet
            var finalsize = myzlib.ZlibCompress(input, ref buffer);
            Array.Resize(ref buffer, finalsize);
            input = new byte[Packet_Head + finalsize + 16];
            Array.Copy(buffer, 0, input, Packet_Head, finalsize);
            Array.Copy(data, 0, input, 0, Packet_Head);
            data = input;

            input = BitConverter.GetBytes(finalsize);
            System.Buffer.BlockCopy(input, 0, data, data.Length - 20, input.Length);

            // Console.WriteLine("Compressed 0x11 size {0:G}", finalsize);
        }

        public void packet_Encode(ref byte[] data)
        {
            //Lets encipher this packet
            uint CypherSize = (uint)((data.Length / 4) & ~1); // same as & -2

            for (uint j = 0; j < CypherSize; j += 2)
            {
                int offset1 = (int)(4 * (j + 7));
                int offset2 = (int)(4 * (j + 8));

                // If not enough bytes remain for a full 64-bit block, skip
                if (offset2 + 4 > data.Length)
                    break;

                // Read two 32-bit words from buff
                uint xl = BitConverter.ToUInt32(data, offset1);
                uint xr = BitConverter.ToUInt32(data, offset2);

                // Encrypt the pair
                tpzblowfish.Blowfish_encipher(ref xl, ref xr);

                // Write them back into the buffer
                Array.Copy(BitConverter.GetBytes(xl), 0, data, offset1, 4);
                Array.Copy(BitConverter.GetBytes(xr), 0, data, offset2, 4);
            }
        }

        //static void packet_head()
        //static void blowfish.en
        //static void blowfish.de
        //static void libz.en
        //static void libz.de
        #endregion
        public void Logout()
        {
            if (Gameserver != null)
            {
                abort = true;
                byte[] data = new byte[4 + Packet_Head + 16];
                byte[] input = BitConverter.GetBytes(PDcode); //Packet count
                System.Buffer.BlockCopy(input, 0, data, 0, input.Length);
                input = BitConverter.GetBytes(((UInt16)0xE7)); //Packet type
                System.Buffer.BlockCopy(input, 0, data, Packet_Head, input.Length);
                input = new byte[] { 0x04 }; //Size
                System.Buffer.BlockCopy(input, 0, data, Packet_Head + 0x01, input.Length);
                input = BitConverter.GetBytes(PDcode); //Packet count
                System.Buffer.BlockCopy(input, 0, data, Packet_Head + 0x02, input.Length);
                data[0x06] = 0x03;
                packet_addmd5(ref data);
                if (!silient)
                    Console.WriteLine("[Game]Log Out sent");
                Gameserver.Send(data, data.Length);
                data = new byte[4 + Packet_Head + 16];
                input = BitConverter.GetBytes(PDcode); //Packet count
                System.Buffer.BlockCopy(input, 0, data, 0, input.Length);
                input = BitConverter.GetBytes(((UInt16)0x0D)); //Packet type
                System.Buffer.BlockCopy(input, 0, data, Packet_Head, input.Length);
                input = new byte[] { 0x04 }; //Size
                System.Buffer.BlockCopy(input, 0, data, Packet_Head + 0x01, input.Length);
                input = BitConverter.GetBytes(PDcode); //Packet count
                System.Buffer.BlockCopy(input, 0, data, Packet_Head + 0x02, input.Length);
                packet_addmd5(ref data);
                Gameserver.Send(data, data.Length);
            }
            if (datastream != null)
            {
                datastream.Close();
            }
            if (viewstream != null)
            {
                viewstream.Close();
            }
        }
        async Task GameserverStart()
        {
            Gameserver = new UdpClient();
            Gameserver.Connect(RemoteIpEndPoint);
            ThreadStart Incomingref = new ThreadStart(ParseIncomingPacket);
            Incoming = new Thread(Incomingref);
            Incoming.Start();
            ThreadStart Logicref = new ThreadStart(Logintozone);
            Logic = new Thread(Logicref);
            Logic.Start();
            //Logintozone();
        }
        void ParseIncomingPacket()
        {
            while (!abort)
            {
                try
                {
                    byte[] receiveBytes = Gameserver.Receive(ref RemoteIpEndPoint);
                    UInt16 server_packet_id = BitConverter.ToUInt16(receiveBytes, 0);
                    UInt16 client_packet_id = BitConverter.ToUInt16(receiveBytes, 2);
                    UInt32 packet_time = BitConverter.ToUInt32(receiveBytes, 8);
                    ServerPacketID = server_packet_id;

                    //// Raw
                    //string[] bytes = BitConverter.ToString(receiveBytes).Split('-');
                    //for (int i = 0; i < bytes.Length; i += 16)
                    //{
                    //    Console.WriteLine("Incoming: " + string.Join(" ", bytes.Skip(i).Take(16)));
                    //}

                    byte[] deblown = new byte[receiveBytes.Length - Packet_Head];
                    byte[] blowhelper;
                    int k = 0;
                    for (int j = Packet_Head; j < receiveBytes.Length && receiveBytes.Length - j >= 8; j += 8)
                    {
                        blowhelper = new byte[8];
                        uint l = BitConverter.ToUInt32(receiveBytes, j);
                        uint r = BitConverter.ToUInt32(receiveBytes, j + 4);
                        tpzblowfish.Blowfish_decipher(ref l, ref r);
                        System.Buffer.BlockCopy(BitConverter.GetBytes(l), 0, blowhelper, 0, 4);
                        System.Buffer.BlockCopy(BitConverter.GetBytes(r), 0, blowhelper, 4, 4);
                        System.Buffer.BlockCopy(blowhelper, 0, deblown, j - Packet_Head, 8);
                        k += 8;
                    }
                    System.Buffer.BlockCopy(deblown, 0, receiveBytes, Packet_Head, k);
                    //Deblowfished
                    //bytes = BitConverter.ToString(receiveBytes).Split('-');
                    //for (int i = 0; i < bytes.Length; i += 16)
                    //{
                    //    Console.WriteLine("Decode:   " + string.Join(" ", bytes.Skip(i).Take(16)));
                    //}
                    //Zlib compress's all but header
                    uint packetsize = BitConverter.ToUInt32(receiveBytes, receiveBytes.Length - 20);// - 20; //Location of packetsize set by encoding by server
                    //byte[] buffer = new byte[receiveBytes.Length - 21 - Packet_Head];
                    byte[] buffer = new byte[(int)Math.Ceiling(packetsize / 8m)];
                    System.Buffer.BlockCopy(receiveBytes, Packet_Head + 1, buffer, 0, buffer.Length);
                    //Console.WriteLine("ToDelib:   " + BitConverter.ToString(buffer).Replace("-", " "));
                    int w = 0;
                    uint pos = myzlib.jump[0];
                    byte[] outbuf = new byte[4000];
                    //Console.WriteLine(buffer.Length + ":" + packetsize);
                    for (int i = 0; i < packetsize && w < 4000; i++)
                    {
                        int s = ((buffer[i / 8] >> (i & 7)) & 1);
                        pos = myzlib.jump[pos + s];
                        //Console.WriteLine("{0:G} : {1:G}  0,1 {2:G},{3:G}", s, pos, myzlib.jump[pos], myzlib.jump[pos+1]);
                        if (myzlib.jump[pos] != 0 || myzlib.jump[pos + 1] != 0)
                        {
                            //Console.WriteLine("Pos:{0:G} not both zero", pos);
                            continue;
                        }
                        //Console.WriteLine("DATA:{0:G}", myzlib.jump[pos + 3]);
                        outbuf[w++] = BitConverter.GetBytes(myzlib.jump[pos + 3])[0];
                        //Console.WriteLine(BitConverter.GetBytes(myzlib.jump[pos + 3])[0]);
                        pos = myzlib.jump[0];
                    }
                    byte[] final = new byte[w];
                    System.Buffer.BlockCopy(outbuf, 0, final, 0, w);

                    //Console.WriteLine("Dezlib size:" + final.Length);
                    //bytes = BitConverter.ToString(final).Split('-');
                    //for (int i = 0; i < bytes.Length; i += 16)
                    //{
                    //    Console.WriteLine("Dezlib:   " + string.Join(" ", bytes.Skip(i).Take(16)));
                    //}

                    int index = 0;
                    int size = final.Length > 2 ? final[index + 1] & 0x0FE : 0;

                    while (index + 2 <= final.Length)
                    {
                        // Make sure we can at least read the type and size
                        ushort type = (ushort)(BitConverter.ToUInt16(final, index) & 0x1FF);
                        size = final[index + 1] & 0x0FE;

                        int packetLength = size * 2;

                        // Safety check
                        if (packetLength <= 0 || index + packetLength > final.Length)
                        {
                            //Console.WriteLine($"[WARN] Invalid packet {type} length at index {index}. Size={packetLength}, Remaining={final.Length - index}");
                            break; // prevent crash
                        }

                        ReadOnlySpan<byte> packetData = new ReadOnlySpan<byte>(final, index, packetLength);
                        _registry.TryHandle(this, type, packetData);

                        index += packetLength;
                    }
                }
                catch (SocketException d)
                {
                    if (!silient)
                        Console.WriteLine("[Game]Connection lost or refused, Exiting");
                    Exit();
                }
            }
        }
        void Logintozone()
        {
            startingkey[4] += 2;

            byte[] byteArray = new byte[startingkey.Length * sizeof(uint)];
            Buffer.BlockCopy(startingkey, 0, byteArray, 0, byteArray.Length);
            byte[] hashkey = hasher.ComputeHash(byteArray, 0, 20);

            for (int i = 0; i < 16; ++i)
            {
                if (hashkey[i] == 0)
                {
                    Array.Clear(hashkey, i, 16 - i);
                    break;
                }
            }

            // Console.WriteLine("[Info]Blowfish key:" + BitConverter.ToString(byteArray).Replace("-", " "));
            // Console.WriteLine("[Info]Blowfish hash:" + BitConverter.ToString(hashkey).Replace("-", " "));

            tpzblowfish = new Blowfish();
            tpzblowfish.Init(hashkey, 16);

            #region ZoneInpackets
            byte[] data = new byte[136];
            byte[] input = BitConverter.GetBytes(ClientPacketID); //Packet count
            System.Buffer.BlockCopy(input, 0, data, 0, input.Length);
            input = BitConverter.GetBytes(((UInt16)0x0A)); //Packet type
            System.Buffer.BlockCopy(input, 0, data, Packet_Head, input.Length);
            input = new byte[] { 0x2E }; //Size
            System.Buffer.BlockCopy(input, 0, data, Packet_Head + 0x01, input.Length);
            input = BitConverter.GetBytes(ClientPacketID); //Packet count
            System.Buffer.BlockCopy(input, 0, data, Packet_Head + 0x02, input.Length);
            input = BitConverter.GetBytes(Player_Data.ID);
            System.Buffer.BlockCopy(input, 0, data, Packet_Head + 0x0C, input.Length);


            byte checksum = 0;

            const int checksumOffset = Packet_Head + 0x03;
            const int checksumLength = 84;

            for (int i = 0; i < checksumLength; i++)
            {
                checksum += data[checksumOffset + i];
            }
            Console.WriteLine("Checksum: " + checksum);

            data[Packet_Head + 0x04] = checksum;



            packet_addmd5(ref data);
            if (!silient)
                Console.WriteLine("[Game]Outgoing packet 0x0A, Zone in");
            try
            {
                Thread.Sleep(2000);
                Gameserver.Send(data, data.Length);
                Thread.Sleep(1000);
                ClientPacketID++;
                input = BitConverter.GetBytes(ClientPacketID); //Packet count
                System.Buffer.BlockCopy(input, 0, data, 0, input.Length);
                Gameserver.Send(data, data.Length);
            }
            catch (SocketException d)
            {
                if (!silient)
                    Console.WriteLine("[Game]Failed to connect retrying");
                startingkey[4] -= 2;
                Logintozone();
            }

            #endregion
        }

        public void SendTell(String User, String Message)
        {
            byte[] data = new byte[21 + 45 + Packet_Head + 30];
            byte[] input = BitConverter.GetBytes(ClientPacketID); //Client Packet Id
            System.Buffer.BlockCopy(input, 0, data, 0, input.Length);
            input = BitConverter.GetBytes(ServerPacketID); //Server Packet Id
            System.Buffer.BlockCopy(input, 0, data, 2, input.Length);
            input = BitConverter.GetBytes(((UInt16)0xB6)); //Packet type
            System.Buffer.BlockCopy(input, 0, data, Packet_Head, input.Length);
            input = BitConverter.GetBytes(20); //Size
            System.Buffer.BlockCopy(input, 0, data, Packet_Head + 0x01, input.Length);
            input = BitConverter.GetBytes(PDcode); //Packet count
            System.Buffer.BlockCopy(input, 0, data, Packet_Head + 0x02, input.Length);
            input = Encoding.ASCII.GetBytes(User);
            System.Buffer.BlockCopy(input, 0, data, Packet_Head + 0x06, input.Length);
            //string message = "Your verification code is: !c Mir50013";
            input = Encoding.UTF8.GetBytes(Message);

            System.Buffer.BlockCopy(input, 0, data, Packet_Head + 0x15, input.Length);
            packet_addmd5(ref data);
            if (!silient)
                Console.WriteLine("Sending Tell");
            if (Gameserver != null)
                Gameserver.Send(data, data.Length);
            else
                Console.WriteLine("Null Gameserver");
        }
        static void Exit()
        {
            System.Environment.Exit(1);
        }


        [Flags]
        public enum SendFlags : byte
        {
            None = 0,
            Position = 1 << 0,
            ClaimStatus = 1 << 1,
            General = 1 << 2,
            Name = 1 << 3,
            Model = 1 << 4,
            Despawn = 1 << 5,
            Name2 = 1 << 6
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct Flags0
        {
            private uint value;

            public ushort MovTime
            {
                get => (ushort)(value & 0x1FFF); // bits 0–12
                set => this.value = (this.value & ~0x1FFFu) | ((uint)value & 0x1FFFu);
            }

            public bool RunMode
            {
                get => (value & (1u << 13)) != 0;
                set => this.value = value ? (this.value | (1u << 13)) : (this.value & ~(1u << 13));
            }

            public bool Unknown_1_6
            {
                get => (value & (1u << 14)) != 0;
                set => this.value = value ? (this.value | (1u << 14)) : (this.value & ~(1u << 14));
            }

            public bool GroundFlag
            {
                get => (value & (1u << 15)) != 0;
                set => this.value = value ? (this.value | (1u << 15)) : (this.value & ~(1u << 15));
            }

            public bool KingFlag
            {
                get => (value & (1u << 16)) != 0;
                set => this.value = value ? (this.value | (1u << 16)) : (this.value & ~(1u << 16));
            }

            public ushort FaceTarget
            {
                get => (ushort)((value >> 17) & 0x7FFF); // bits 17–31
                set => this.value = (this.value & ~(0x7FFFu << 17)) | (((uint)value & 0x7FFFu) << 17);
            }

            public uint Raw
            {
                get => value;
                set => this.value = value;
            }
        }
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct StatusFlags
        {
            public uint RawValue;

            // Bit masks
            private const uint MonsterFlagMask     = 1u << 0;
            private const uint HideFlagMask        = 1u << 1;
            private const uint SleepFlagMask       = 1u << 2;
            private const uint Unknown_0_3Mask     = 1u << 3;
            private const uint Unknown_0_4Mask     = 1u << 4;
            private const uint ChocoboIndexMask    = 0b111u << 5;
            private const uint CliPosInitFlagMask  = 1u << 8;
            private const uint GraphSizeMask       = 0b11u << 9;
            private const uint LfgFlagMask         = 1u << 11;
            private const uint AnonymousFlagMask   = 1u << 12;
            private const uint YellFlagMask        = 1u << 13;
            private const uint AwayFlagMask        = 1u << 14;
            private const uint GenderMask          = 1u << 15;
            private const uint PlayOnlineFlagMask  = 1u << 16;
            private const uint LinkShellFlagMask   = 1u << 17;
            private const uint LinkDeadFlagMask    = 1u << 18;
            private const uint TargetOffFlagMask   = 1u << 19;
            private const uint TalkUcoffFlagMask   = 1u << 20;
            private const uint Unknown_2_5Mask     = 1u << 21;
            private const uint Unknown_2_6Mask     = 1u << 22;
            private const uint Unknown_2_7Mask     = 1u << 23;
            private const uint GmLevelMask         = 0b111u << 24;
            private const uint HackMoveMask        = 1u << 27;
            private const uint Unknown_3_4Mask     = 1u << 28;
            private const uint InvisFlagMask       = 1u << 29;
            private const uint TurnFlagMask        = 1u << 30;
            private const uint BazaarFlagMask      = 1u << 31;

            // Accessors
            public bool MonsterFlag
            {
                readonly get => (RawValue & MonsterFlagMask) != 0;
                set => RawValue = value ? (RawValue | MonsterFlagMask) : (RawValue & ~MonsterFlagMask);
            }

            public bool HideFlag
            {
                readonly get => (RawValue & HideFlagMask) != 0;
                set => RawValue = value ? (RawValue | HideFlagMask) : (RawValue & ~HideFlagMask);
            }

            public bool SleepFlag
            {
                readonly get => (RawValue & SleepFlagMask) != 0;
                set => RawValue = value ? (RawValue | SleepFlagMask) : (RawValue & ~SleepFlagMask);
            }

            // ... repeat pattern for other flags as needed ...

            public byte ChocoboIndex
            {
                readonly get => (byte)((RawValue & ChocoboIndexMask) >> 5);
                set => RawValue = (RawValue & ~ChocoboIndexMask) | ((uint)(value & 0b111) << 5);
            }

            public byte GraphSize
            {
                readonly get => (byte)((RawValue & GraphSizeMask) >> 9);
                set => RawValue = (RawValue & ~GraphSizeMask) | ((uint)(value & 0b11) << 9);
            }

            public byte GmLevel
            {
                readonly get => (byte)((RawValue & GmLevelMask) >> 24);
                set => RawValue = (RawValue & ~GmLevelMask) | ((uint)(value & 0b111) << 24);
            }
        }

        #region Outgoing Packets
        // Char Pos
        void OutGoing_015()
        {
            ClientPacketID++;
            byte[] data = new byte[74];
            byte[] input = BitConverter.GetBytes(ClientPacketID); //Client Packet Id
            System.Buffer.BlockCopy(input, 0, data, 0, input.Length);
            input = BitConverter.GetBytes(ServerPacketID); //Server Packet Id
            System.Buffer.BlockCopy(input, 0, data, 2, input.Length);

            packet_SetType(ref data, 0x15);
            packet_SetSize(ref data, 0x10);

            input = BitConverter.GetBytes(ClientPacketID); //Packet count
            System.Buffer.BlockCopy(input, 0, data, Packet_Head + 0x02, input.Length);

            input = BitConverter.GetBytes(Player_Data.pos.X);
            System.Buffer.BlockCopy(input, 0, data, Packet_Head + 0x04, input.Length); //Xpos
            input = BitConverter.GetBytes(Player_Data.pos.Y);
            System.Buffer.BlockCopy(input, 0, data, Packet_Head + 0x08, input.Length); //Ypos
            input = BitConverter.GetBytes(Player_Data.pos.Z);
            System.Buffer.BlockCopy(input, 0, data, Packet_Head + 0x0C, input.Length); //Zpos

            if (Player_Data.pos.HasChanged(Player_Data.oldpos))
            {
                Player_Data.pos.moving = (ushort)(Player_Data.pos.moving + 7);
                Player_Data.oldpos = Player_Data.pos;
            }
            else
            {
                Player_Data.pos.moving = 0;
            }

            input = BitConverter.GetBytes(Player_Data.pos.moving);
            System.Buffer.BlockCopy(input, 0, data, Packet_Head + 0x12, input.Length);//MoveFlame

            input = new byte[] { Player_Data.pos.Rot };
            System.Buffer.BlockCopy(input, 0, data, Packet_Head + 0x14, input.Length);//dir

            packet_Compress(ref data);
            packet_addmd5(ref data);
            packet_Encode(ref data);
            Gameserver.Send(data, data.Length);

        }
        // Zone in confirmation
        public void OutGoing_O11()
        {
            Thread.Sleep(500);

            ClientPacketID++;
            byte[] data = new byte[Packet_Head + 8];
            byte[] input = BitConverter.GetBytes(ClientPacketID); //Client Packet Id
            System.Buffer.BlockCopy(input, 0, data, 0, input.Length);
            input = BitConverter.GetBytes(ServerPacketID); //Server Packet Id
            System.Buffer.BlockCopy(input, 0, data, 2, input.Length);

            packet_SetType(ref data, 0x11);
            packet_SetSize(ref data, 0x04);

            input = BitConverter.GetBytes(ClientPacketID); //Packet count
            System.Buffer.BlockCopy(input, 0, data, Packet_Head + 0x02, input.Length);

            data[Packet_Head + 4] = 2;

            packet_Compress(ref data);

            packet_addmd5(ref data);
            if (!silient)
                Console.WriteLine("[Game]Outgoing packet 0x11, Zone in confirmation");

            packet_Encode(ref data);

            Gameserver.Send(data, data.Length);

            Thread.Sleep(2000);

            Task.Run(async () =>
            {
                while (!abort)
                {
                    OutGoing_015();                    // your send logic
                    await Task.Delay(400);             // non-blocking 400 ms wait
                }
            });

            OutGoing_ZoneInData();
        }

        void OutGoing_ZoneInData()
        {
            Console.WriteLine("[Game]Sending Zone in data serverpacket:{0:G}", ServerPacketID);
            if (chardata)
            {
                ClientPacketID++;
                byte[] data = new byte[183];

                byte[] input = BitConverter.GetBytes(ClientPacketID); //Client Packet Id
                System.Buffer.BlockCopy(input, 0, data, 0, input.Length);
                input = BitConverter.GetBytes(ServerPacketID); //Server Packet Id
                System.Buffer.BlockCopy(input, 0, data, 2, input.Length);

                input = BitConverter.GetBytes(((UInt16)0x0C)); //Packet type
                System.Buffer.BlockCopy(input, 0, data, Packet_Head, input.Length);
                input = new byte[] { 0x06 }; //Size
                System.Buffer.BlockCopy(input, 0, data, Packet_Head + 0x01, input.Length);
                input = BitConverter.GetBytes(ClientPacketID); //Packet count
                System.Buffer.BlockCopy(input, 0, data, Packet_Head + 0x02, input.Length);

                int new_Head = Packet_Head + (0x06 * 2);

                input = BitConverter.GetBytes(((UInt16)0x61)); //Packet type
                System.Buffer.BlockCopy(input, 0, data, new_Head, input.Length);
                input = new byte[] { 0x04 }; //Size
                System.Buffer.BlockCopy(input, 0, data, new_Head + 0x01, input.Length);
                input = BitConverter.GetBytes(ClientPacketID); //Packet count
                System.Buffer.BlockCopy(input, 0, data, new_Head + 0x02, input.Length);

                new_Head = new_Head + (0x04 * 2);

                input = BitConverter.GetBytes(((UInt16)0x01A)); //Packet type
                System.Buffer.BlockCopy(input, 0, data, new_Head, input.Length);
                input = new byte[] { 0x0E }; //Size
                System.Buffer.BlockCopy(input, 0, data, new_Head + 0x01, input.Length);
                input = BitConverter.GetBytes(ClientPacketID); //Packet count
                System.Buffer.BlockCopy(input, 0, data, new_Head + 0x02, input.Length);
                input = new byte[] { 0x14 }; //Action type
                System.Buffer.BlockCopy(input, 0, data, new_Head + 0x0A, input.Length);

                new_Head = new_Head + (0x0E * 2);

                input = BitConverter.GetBytes(((UInt16)0x4B)); //Packet type
                System.Buffer.BlockCopy(input, 0, data, new_Head, input.Length);
                input = new byte[] { 0x0C }; //Size
                System.Buffer.BlockCopy(input, 0, data, new_Head + 0x01, input.Length);
                input = BitConverter.GetBytes(ClientPacketID); //Packet count
                System.Buffer.BlockCopy(input, 0, data, new_Head + 0x02, input.Length);
                input = new byte[] { 0x02, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 }; //Language,Timestamp,Lengh,Start offset
                System.Buffer.BlockCopy(input, 0, data, new_Head + 0x07, input.Length);

                new_Head = new_Head + (0x0C * 2);

                input = BitConverter.GetBytes(((UInt16)0x0F)); //Packet type
                System.Buffer.BlockCopy(input, 0, data, new_Head, input.Length);
                input = new byte[] { 0x12 }; //Size
                System.Buffer.BlockCopy(input, 0, data, new_Head + 0x01, input.Length);
                input = BitConverter.GetBytes(ClientPacketID); //Packet count
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
                input = BitConverter.GetBytes(ClientPacketID); //Packet count
                System.Buffer.BlockCopy(input, 0, data, new_Head + 0x02, input.Length);

                //new_Head = new_Head + (0x02 * 2);

                packet_Compress(ref data);

                packet_addmd5(ref data);

                packet_Encode(ref data);

                if (!silient)
                    Console.WriteLine("[Game]Outgoing packet multi,Sending Post zone data requests");

                Gameserver.Send(data, data.Length);

            }
        }
        #endregion
    }



    class Blowfish
    {
        uint[] P =
        {
            0x243F6A88, 0x85A308D3, 0x13198A2E, 0x03707344,
            0xA4093822, 0x299F31D0, 0x082EFA98, 0xEC4E6C89,
            0x452821E6, 0x38D01377, 0xBE5466CF, 0x34E90C6C,
            0xC0AC29B7, 0xC97C50DD, 0x3F84D5B5, 0xB5470917,
            0x9216D5D9, 0x8979FB1B
        },
        KS0 =
        {
            0xD1310BA6, 0x98DFB5AC, 0x2FFD72DB, 0xD01ADFB7,
            0xB8E1AFED, 0x6A267E96, 0xBA7C9045, 0xF12C7F99,
            0x24A19947, 0xB3916CF7, 0x0801F2E2, 0x858EFC16,
            0x636920D8, 0x71574E69, 0xA458FEA3, 0xF4933D7E,
            0x0D95748F, 0x728EB658, 0x718BCD58, 0x82154AEE,
            0x7B54A41D, 0xC25A59B5, 0x9C30D539, 0x2AF26013,
            0xC5D1B023, 0x286085F0, 0xCA417918, 0xB8DB38EF,
            0x8E79DCB0, 0x603A180E, 0x6C9E0E8B, 0xB01E8A3E,
            0xD71577C1, 0xBD314B27, 0x78AF2FDA, 0x55605C60,
            0xE65525F3, 0xAA55AB94, 0x57489862, 0x63E81440,
            0x55CA396A, 0x2AAB10B6, 0xB4CC5C34, 0x1141E8CE,
            0xA15486AF, 0x7C72E993, 0xB3EE1411, 0x636FBC2A,
            0x2BA9C55D, 0x741831F6, 0xCE5C3E16, 0x9B87931E,
            0xAFD6BA33, 0x6C24CF5C, 0x7A325381, 0x28958677,
            0x3B8F4898, 0x6B4BB9AF, 0xC4BFE81B, 0x66282193,
            0x61D809CC, 0xFB21A991, 0x487CAC60, 0x5DEC8032,
            0xEF845D5D, 0xE98575B1, 0xDC262302, 0xEB651B88,
            0x23893E81, 0xD396ACC5, 0x0F6D6FF3, 0x83F44239,
            0x2E0B4482, 0xA4842004, 0x69C8F04A, 0x9E1F9B5E,
            0x21C66842, 0xF6E96C9A, 0x670C9C61, 0xABD388F0,
            0x6A51A0D2, 0xD8542F68, 0x960FA728, 0xAB5133A3,
            0x6EEF0B6C, 0x137A3BE4, 0xBA3BF050, 0x7EFB2A98,
            0xA1F1651D, 0x39AF0176, 0x66CA593E, 0x82430E88,
            0x8CEE8619, 0x456F9FB4, 0x7D84A5C3, 0x3B8B5EBE,
            0xE06F75D8, 0x85C12073, 0x401A449F, 0x56C16AA6,
            0x4ED3AA62, 0x363F7706, 0x1BFEDF72, 0x429B023D,
            0x37D0D724, 0xD00A1248, 0xDB0FEAD3, 0x49F1C09B,
            0x075372C9, 0x80991B7B, 0x25D479D8, 0xF6E8DEF7,
            0xE3FE501A, 0xB6794C3B, 0x976CE0BD, 0x04C006BA,
            0xC1A94FB6, 0x409F60C4, 0x5E5C9EC2, 0x196A2463,
            0x68FB6FAF, 0x3E6C53B5, 0x1339B2EB, 0x3B52EC6F,
            0x6DFC511F, 0x9B30952C, 0xCC814544, 0xAF5EBD09,
            0xBEE3D004, 0xDE334AFD, 0x660F2807, 0x192E4BB3,
            0xC0CBA857, 0x45C8740F, 0xD20B5F39, 0xB9D3FBDB,
            0x5579C0BD, 0x1A60320A, 0xD6A100C6, 0x402C7279,
            0x679F25FE, 0xFB1FA3CC, 0x8EA5E9F8, 0xDB3222F8,
            0x3C7516DF, 0xFD616B15, 0x2F501EC8, 0xAD0552AB,
            0x323DB5FA, 0xFD238760, 0x53317B48, 0x3E00DF82,
            0x9E5C57BB, 0xCA6F8CA0, 0x1A87562E, 0xDF1769DB,
            0xD542A8F6, 0x287EFFC3, 0xAC6732C6, 0x8C4F5573,
            0x695B27B0, 0xBBCA58C8, 0xE1FFA35D, 0xB8F011A0,
            0x10FA3D98, 0xFD2183B8, 0x4AFCB56C, 0x2DD1D35B,
            0x9A53E479, 0xB6F84565, 0xD28E49BC, 0x4BFB9790,
            0xE1DDF2DA, 0xA4CB7E33, 0x62FB1341, 0xCEE4C6E8,
            0xEF20CADA, 0x36774C01, 0xD07E9EFE, 0x2BF11FB4,
            0x95DBDA4D, 0xAE909198, 0xEAAD8E71, 0x6B93D5A0,
            0xD08ED1D0, 0xAFC725E0, 0x8E3C5B2F, 0x8E7594B7,
            0x8FF6E2FB, 0xF2122B64, 0x8888B812, 0x900DF01C,
            0x4FAD5EA0, 0x688FC31C, 0xD1CFF191, 0xB3A8C1AD,
            0x2F2F2218, 0xBE0E1777, 0xEA752DFE, 0x8B021FA1,
            0xE5A0CC0F, 0xB56F74E8, 0x18ACF3D6, 0xCE89E299,
            0xB4A84FE0, 0xFD13E0B7, 0x7CC43B81, 0xD2ADA8D9,
            0x165FA266, 0x80957705, 0x93CC7314, 0x211A1477,
            0xE6AD2065, 0x77B5FA86, 0xC75442F5, 0xFB9D35CF,
            0xEBCDAF0C, 0x7B3E89A0, 0xD6411BD3, 0xAE1E7E49,
            0x00250E2D, 0x2071B35E, 0x226800BB, 0x57B8E0AF,
            0x2464369B, 0xF009B91E, 0x5563911D, 0x59DFA6AA,
            0x78C14389, 0xD95A537F, 0x207D5BA2, 0x02E5B9C5,
            0x83260376, 0x6295CFA9, 0x11C81968, 0x4E734A41,
            0xB3472DCA, 0x7B14A94A, 0x1B510052, 0x9A532915,
            0xD60F573F, 0xBC9BC6E4, 0x2B60A476, 0x81E67400,
            0x08BA6FB5, 0x571BE91F, 0xF296EC6B, 0x2A0DD915,
            0xB6636521, 0xE7B9F9B6, 0xFF34052E, 0xC5855664,
            0x53B02D5D, 0xA99F8FA1, 0x08BA4799, 0x6E85076A
        },
        KS1 =
        {
            0x4B7A70E9, 0xB5B32944, 0xDB75092E, 0xC4192623,
            0xAD6EA6B0, 0x49A7DF7D, 0x9CEE60B8, 0x8FEDB266,
            0xECAA8C71, 0x699A17FF, 0x5664526C, 0xC2B19EE1,
            0x193602A5, 0x75094C29, 0xA0591340, 0xE4183A3E,
            0x3F54989A, 0x5B429D65, 0x6B8FE4D6, 0x99F73FD6,
            0xA1D29C07, 0xEFE830F5, 0x4D2D38E6, 0xF0255DC1,
            0x4CDD2086, 0x8470EB26, 0x6382E9C6, 0x021ECC5E,
            0x09686B3F, 0x3EBAEFC9, 0x3C971814, 0x6B6A70A1,
            0x687F3584, 0x52A0E286, 0xB79C5305, 0xAA500737,
            0x3E07841C, 0x7FDEAE5C, 0x8E7D44EC, 0x5716F2B8,
            0xB03ADA37, 0xF0500C0D, 0xF01C1F04, 0x0200B3FF,
            0xAE0CF51A, 0x3CB574B2, 0x25837A58, 0xDC0921BD,
            0xD19113F9, 0x7CA92FF6, 0x94324773, 0x22F54701,
            0x3AE5E581, 0x37C2DADC, 0xC8B57634, 0x9AF3DDA7,
            0xA9446146, 0x0FD0030E, 0xECC8C73E, 0xA4751E41,
            0xE238CD99, 0x3BEA0E2F, 0x3280BBA1, 0x183EB331,
            0x4E548B38, 0x4F6DB908, 0x6F420D03, 0xF60A04BF,
            0x2CB81290, 0x24977C79, 0x5679B072, 0xBCAF89AF,
            0xDE9A771F, 0xD9930810, 0xB38BAE12, 0xDCCF3F2E,
            0x5512721F, 0x2E6B7124, 0x501ADDE6, 0x9F84CD87,
            0x7A584718, 0x7408DA17, 0xBC9F9ABC, 0xE94B7D8C,
            0xEC7AEC3A, 0xDB851DFA, 0x63094366, 0xC464C3D2,
            0xEF1C1847, 0x3215D908, 0xDD433B37, 0x24C2BA16,
            0x12A14D43, 0x2A65C451, 0x50940002, 0x133AE4DD,
            0x71DFF89E, 0x10314E55, 0x81AC77D6, 0x5F11199B,
            0x043556F1, 0xD7A3C76B, 0x3C11183B, 0x5924A509,
            0xF28FE6ED, 0x97F1FBFA, 0x9EBABF2C, 0x1E153C6E,
            0x86E34570, 0xEAE96FB1, 0x860E5E0A, 0x5A3E2AB3,
            0x771FE71C, 0x4E3D06FA, 0x2965DCB9, 0x99E71D0F,
            0x803E89D6, 0x5266C825, 0x2E4CC978, 0x9C10B36A,
            0xC6150EBA, 0x94E2EA78, 0xA5FC3C53, 0x1E0A2DF4,
            0xF2F74EA7, 0x361D2B3D, 0x1939260F, 0x19C27960,
            0x5223A708, 0xF71312B6, 0xEBADFE6E, 0xEAC31F66,
            0xE3BC4595, 0xA67BC883, 0xB17F37D1, 0x018CFF28,
            0xC332DDEF, 0xBE6C5AA5, 0x65582185, 0x68AB9802,
            0xEECEA50F, 0xDB2F953B, 0x2AEF7DAD, 0x5B6E2F84,
            0x1521B628, 0x29076170, 0xECDD4775, 0x619F1510,
            0x13CCA830, 0xEB61BD96, 0x0334FE1E, 0xAA0363CF,
            0xB5735C90, 0x4C70A239, 0xD59E9E0B, 0xCBAADE14,
            0xEECC86BC, 0x60622CA7, 0x9CAB5CAB, 0xB2F3846E,
            0x648B1EAF, 0x19BDF0CA, 0xA02369B9, 0x655ABB50,
            0x40685A32, 0x3C2AB4B3, 0x319EE9D5, 0xC021B8F7,
            0x9B540B19, 0x875FA099, 0x95F7997E, 0x623D7DA8,
            0xF837889A, 0x97E32D77, 0x11ED935F, 0x16681281,
            0x0E358829, 0xC7E61FD6, 0x96DEDFA1, 0x7858BA99,
            0x57F584A5, 0x1B227263, 0x9B83C3FF, 0x1AC24696,
            0xCDB30AEB, 0x532E3054, 0x8FD948E4, 0x6DBC3128,
            0x58EBF2EF, 0x34C6FFEA, 0xFE28ED61, 0xEE7C3C73,
            0x5D4A14D9, 0xE864B7E3, 0x42105D14, 0x203E13E0,
            0x45EEE2B6, 0xA3AAABEA, 0xDB6C4F15, 0xFACB4FD0,
            0xC742F442, 0xEF6ABBB5, 0x654F3B1D, 0x41CD2105,
            0xD81E799E, 0x86854DC7, 0xE44B476A, 0x3D816250,
            0xCF62A1F2, 0x5B8D2646, 0xFC8883A0, 0xC1C7B6A3,
            0x7F1524C3, 0x69CB7492, 0x47848A0B, 0x5692B285,
            0x095BBF00, 0xAD19489D, 0x1462B174, 0x23820E00,
            0x58428D2A, 0x0C55F5EA, 0x1DADF43E, 0x233F7061,
            0x3372F092, 0x8D937E41, 0xD65FECF1, 0x6C223BDB,
            0x7CDE3759, 0xCBEE7460, 0x4085F2A7, 0xCE77326E,
            0xA6078084, 0x19F8509E, 0xE8EFD855, 0x61D99735,
            0xA969A7AA, 0xC50C06C2, 0x5A04ABFC, 0x800BCADC,
            0x9E447A2E, 0xC3453484, 0xFDD56705, 0x0E1E9EC9,
            0xDB73DBD3, 0x105588CD, 0x675FDA79, 0xE3674340,
            0xC5C43465, 0x713E38D8, 0x3D28F89E, 0xF16DFF20,
            0x153E21E7, 0x8FB03D4A, 0xE6E39F2B, 0xDB83ADF7
        },
        KS2 =
        {
            0xE93D5A68, 0x948140F7, 0xF64C261C, 0x94692934,
            0x411520F7, 0x7602D4F7, 0xBCF46B2E, 0xD4A20068,
            0xD4082471, 0x3320F46A, 0x43B7D4B7, 0x500061AF,
            0x1E39F62E, 0x97244546, 0x14214F74, 0xBF8B8840,
            0x4D95FC1D, 0x96B591AF, 0x70F4DDD3, 0x66A02F45,
            0xBFBC09EC, 0x03BD9785, 0x7FAC6DD0, 0x31CB8504,
            0x96EB27B3, 0x55FD3941, 0xDA2547E6, 0xABCA0A9A,
            0x28507825, 0x530429F4, 0x0A2C86DA, 0xE9B66DFB,
            0x68DC1462, 0xD7486900, 0x680EC0A4, 0x27A18DEE,
            0x4F3FFEA2, 0xE887AD8C, 0xB58CE006, 0x7AF4D6B6,
            0xAACE1E7C, 0xD3375FEC, 0xCE78A399, 0x406B2A42,
            0x20FE9E35, 0xD9F385B9, 0xEE39D7AB, 0x3B124E8B,
            0x1DC9FAF7, 0x4B6D1856, 0x26A36631, 0xEAE397B2,
            0x3A6EFA74, 0xDD5B4332, 0x6841E7F7, 0xCA7820FB,
            0xFB0AF54E, 0xD8FEB397, 0x454056AC, 0xBA489527,
            0x55533A3A, 0x20838D87, 0xFE6BA9B7, 0xD096954B,
            0x55A867BC, 0xA1159A58, 0xCCA92963, 0x99E1DB33,
            0xA62A4A56, 0x3F3125F9, 0x5EF47E1C, 0x9029317C,
            0xFDF8E802, 0x04272F70, 0x80BB155C, 0x05282CE3,
            0x95C11548, 0xE4C66D22, 0x48C1133F, 0xC70F86DC,
            0x07F9C9EE, 0x41041F0F, 0x404779A4, 0x5D886E17,
            0x325F51EB, 0xD59BC0D1, 0xF2BCC18F, 0x41113564,
            0x257B7834, 0x602A9C60, 0xDFF8E8A3, 0x1F636C1B,
            0x0E12B4C2, 0x02E1329E, 0xAF664FD1, 0xCAD18115,
            0x6B2395E0, 0x333E92E1, 0x3B240B62, 0xEEBEB922,
            0x85B2A20E, 0xE6BA0D99, 0xDE720C8C, 0x2DA2F728,
            0xD0127845, 0x95B794FD, 0x647D0862, 0xE7CCF5F0,
            0x5449A36F, 0x877D48FA, 0xC39DFD27, 0xF33E8D1E,
            0x0A476341, 0x992EFF74, 0x3A6F6EAB, 0xF4F8FD37,
            0xA812DC60, 0xA1EBDDF8, 0x991BE14C, 0xDB6E6B0D,
            0xC67B5510, 0x6D672C37, 0x2765D43B, 0xDCD0E804,
            0xF1290DC7, 0xCC00FFA3, 0xB5390F92, 0x690FED0B,
            0x667B9FFB, 0xCEDB7D9C, 0xA091CF0B, 0xD9155EA3,
            0xBB132F88, 0x515BAD24, 0x7B9479BF, 0x763BD6EB,
            0x37392EB3, 0xCC115979, 0x8026E297, 0xF42E312D,
            0x6842ADA7, 0xC66A2B3B, 0x12754CCC, 0x782EF11C,
            0x6A124237, 0xB79251E7, 0x06A1BBE6, 0x4BFB6350,
            0x1A6B1018, 0x11CAEDFA, 0x3D25BDD8, 0xE2E1C3C9,
            0x44421659, 0x0A121386, 0xD90CEC6E, 0xD5ABEA2A,
            0x64AF674E, 0xDA86A85F, 0xBEBFE988, 0x64E4C3FE,
            0x9DBC8057, 0xF0F7C086, 0x60787BF8, 0x6003604D,
            0xD1FD8346, 0xF6381FB0, 0x7745AE04, 0xD736FCCC,
            0x83426B33, 0xF01EAB71, 0xB0804187, 0x3C005E5F,
            0x77A057BE, 0xBDE8AE24, 0x55464299, 0xBF582E61,
            0x4E58F48F, 0xF2DDFDA2, 0xF474EF38, 0x8789BDC2,
            0x5366F9C3, 0xC8B38E74, 0xB475F255, 0x46FCD9B9,
            0x7AEB2661, 0x8B1DDF84, 0x846A0E79, 0x915F95E2,
            0x466E598E, 0x20B45770, 0x8CD55591, 0xC902DE4C,
            0xB90BACE1, 0xBB8205D0, 0x11A86248, 0x7574A99E,
            0xB77F19B6, 0xE0A9DC09, 0x662D09A1, 0xC4324633,
            0xE85A1F02, 0x09F0BE8C, 0x4A99A025, 0x1D6EFE10,
            0x1AB93D1D, 0x0BA5A4DF, 0xA186F20F, 0x2868F169,
            0xDCB7DA83, 0x573906FE, 0xA1E2CE9B, 0x4FCD7F52,
            0x50115E01, 0xA70683FA, 0xA002B5C4, 0x0DE6D027,
            0x9AF88C27, 0x773F8641, 0xC3604C06, 0x61A806B5,
            0xF0177A28, 0xC0F586E0, 0x006058AA, 0x30DC7D62,
            0x11E69ED7, 0x2338EA63, 0x53C2DD94, 0xC2C21634,
            0xBBCBEE56, 0x90BCB6DE, 0xEBFC7DA1, 0xCE591D76,
            0x6F05E409, 0x4B7C0188, 0x39720A3D, 0x7C927C24,
            0x86E3725F, 0x724D9DB9, 0x1AC15BB4, 0xD39EB8FC,
            0xED545578, 0x08FCA5B5, 0xD83D7CD3, 0x4DAD0FC4,
            0x1E50EF5E, 0xB161E6F8, 0xA28514D9, 0x6C51133C,
            0x6FD5C7E7, 0x56E14EC4, 0x362ABFCE, 0xDDC6C837,
            0xD79A3234, 0x92638212, 0x670EFA8E, 0x406000E0
        },
        KS3 =
        {
            0x3A39CE37, 0xD3FAF5CF, 0xABC27737, 0x5AC52D1B,
            0x5CB0679E, 0x4FA33742, 0xD3822740, 0x99BC9BBE,
            0xD5118E9D, 0xBF0F7315, 0xD62D1C7E, 0xC700C47B,
            0xB78C1B6B, 0x21A19045, 0xB26EB1BE, 0x6A366EB4,
            0x5748AB2F, 0xBC946E79, 0xC6A376D2, 0x6549C2C8,
            0x530FF8EE, 0x468DDE7D, 0xD5730A1D, 0x4CD04DC6,
            0x2939BBDB, 0xA9BA4650, 0xAC9526E8, 0xBE5EE304,
            0xA1FAD5F0, 0x6A2D519A, 0x63EF8CE2, 0x9A86EE22,
            0xC089C2B8, 0x43242EF6, 0xA51E03AA, 0x9CF2D0A4,
            0x83C061BA, 0x9BE96A4D, 0x8FE51550, 0xBA645BD6,
            0x2826A2F9, 0xA73A3AE1, 0x4BA99586, 0xEF5562E9,
            0xC72FEFD3, 0xF752F7DA, 0x3F046F69, 0x77FA0A59,
            0x80E4A915, 0x87B08601, 0x9B09E6AD, 0x3B3EE593,
            0xE990FD5A, 0x9E34D797, 0x2CF0B7D9, 0x022B8B51,
            0x96D5AC3A, 0x017DA67D, 0xD1CF3ED6, 0x7C7D2D28,
            0x1F9F25CF, 0xADF2B89B, 0x5AD6B472, 0x5A88F54C,
            0xE029AC71, 0xE019A5E6, 0x47B0ACFD, 0xED93FA9B,
            0xE8D3C48D, 0x283B57CC, 0xF8D56629, 0x79132E28,
            0x785F0191, 0xED756055, 0xF7960E44, 0xE3D35E8C,
            0x15056DD4, 0x88F46DBA, 0x03A16125, 0x0564F0BD,
            0xC3EB9E15, 0x3C9057A2, 0x97271AEC, 0xA93A072A,
            0x1B3F6D9B, 0x1E6321F5, 0xF59C66FB, 0x26DCF319,
            0x7533D928, 0xB155FDF5, 0x03563482, 0x8ABA3CBB,
            0x28517711, 0xC20AD9F8, 0xABCC5167, 0xCCAD925F,
            0x4DE81751, 0x3830DC8E, 0x379D5862, 0x9320F991,
            0xEA7A90C2, 0xFB3E7BCE, 0x5121CE64, 0x774FBE32,
            0xA8B6E37E, 0xC3293D46, 0x48DE5369, 0x6413E680,
            0xA2AE0810, 0xDD6DB224, 0x69852DFD, 0x09072166,
            0xB39A460A, 0x6445C0DD, 0x586CDECF, 0x1C20C8AE,
            0x5BBEF7DD, 0x1B588D40, 0xCCD2017F, 0x6BB4E3BB,
            0xDDA26A7E, 0x3A59FF45, 0x3E350A44, 0xBCB4CDD5,
            0x72EACEA8, 0xFA6484BB, 0x8D6612AE, 0xBF3C6F47,
            0xD29BE463, 0x542F5D9E, 0xAEC2771B, 0xF64E6370,
            0x740E0D8D, 0xE75B1357, 0xF8721671, 0xAF537D5D,
            0x4040CB08, 0x4EB4E2CC, 0x34D2466A, 0x0115AF84,
            0xE1B00428, 0x95983A1D, 0x06B89FB4, 0xCE6EA048,
            0x6F3F3B82, 0x3520AB82, 0x011A1D4B, 0x277227F8,
            0x611560B1, 0xE7933FDC, 0xBB3A792B, 0x344525BD,
            0xA08839E1, 0x51CE794B, 0x2F32C9B7, 0xA01FBAC9,
            0xE01CC87E, 0xBCC7D1F6, 0xCF0111C3, 0xA1E8AAC7,
            0x1A908749, 0xD44FBD9A, 0xD0DADECB, 0xD50ADA38,
            0x0339C32A, 0xC6913667, 0x8DF9317C, 0xE0B12B4F,
            0xF79E59B7, 0x43F5BB3A, 0xF2D519FF, 0x27D9459C,
            0xBF97222C, 0x15E6FC2A, 0x0F91FC71, 0x9B941525,
            0xFAE59361, 0xCEB69CEB, 0xC2A86459, 0x12BAA8D1,
            0xB6C1075E, 0xE3056A0C, 0x10D25065, 0xCB03A442,
            0xE0EC6E0E, 0x1698DB3B, 0x4C98A0BE, 0x3278E964,
            0x9F1F9532, 0xE0D392DF, 0xD3A0342B, 0x8971F21E,
            0x1B0A7441, 0x4BA3348C, 0xC5BE7120, 0xC37632D8,
            0xDF359F8D, 0x9B992F2E, 0xE60B6F47, 0x0FE3F11D,
            0xE54CDA54, 0x1EDAD891, 0xCE6279CF, 0xCD3E7E6F,
            0x1618B166, 0xFD2C1D05, 0x848FD2C5, 0xF6FB2299,
            0xF523F357, 0xA6327623, 0x93A83531, 0x56CCCD02,
            0xACF08162, 0x5A75EBB5, 0x6E163697, 0x88D273CC,
            0xDE966292, 0x81B949D0, 0x4C50901B, 0x71C65614,
            0xE6C6C7BD, 0x327A140A, 0x45E1D006, 0xC3F27B9A,
            0xC9AA53FD, 0x62A80F00, 0xBB25BFE2, 0x35BDD2F6,
            0x71126905, 0xB2040222, 0xB6CBCF7C, 0xCD769C2B,
            0x53113EC0, 0x1640E3D3, 0x38ABBD60, 0x2547ADF0,
            0xBA38209C, 0xF746CE76, 0x77AFA1C5, 0x20756060,
            0x85CBFE4E, 0x8AE88DD8, 0x7AAAF9B0, 0x4CF9AA7E,
            0x1948C25C, 0x02FB8A8C, 0x01C36AE4, 0xD6EBE1F9,
            0x90D4F869, 0xA65CDEA0, 0x3F09252D, 0xC208E69F,
            0xB74E6132, 0xCE77E25B, 0x578FDFE3, 0x3AC372E6
        };
        byte[] hash;

        private uint TT(uint working)
        {
            return (((KS1[(working >> 8) & 0xff] & 1) ^ 32) + ((KS3[(working >> 24)] & 1) ^ 32) + KS2[((working >> 16) & 0xff)] + KS0[working & 0xff]);
        }

        public void Init(byte[] key, UInt16 keybytes)
        {
            int i;
            int j;
            int k;
            uint data;
            uint datal;
            uint datar;

            const int N = 16;

            j = 0;

            for (i = 0; i < N + 2; ++i)
            {
                data = 0;
                for (k = 0; k < 4; ++k)
                {
                    uint tempkey = key[j];

                    if (key[j].ToString("X2").IndexOfAny(new char[] { 'A', 'B', 'C', 'D', 'E', 'F' }) == 0)
                    {
                        tempkey = tempkey | 4294967040;
                    }
                    //Console.WriteLine(tempkey + "," + data+","+ (data << 8));
                    data = (data << 8) | tempkey;
                    j = j + 1;
                    if (j >= keybytes)
                    {
                        j = 0;
                    }
                }
                //Console.WriteLine(P[i]+","+data);
                P[i] = P[i] ^ data;
                //Console.WriteLine(P[i]);
            }

            datal = 0;
            datar = 0;

            for (i = 0; i < N + 2; i += 2)
            {
                Blowfish_encipher(ref datal, ref datar);

                P[i] = datal;
                P[i + 1] = datar;
                //Console.WriteLine(P[i] + "," + P[i + 1]);
            }

            for (j = 0; j < 256; j += 2)
            {
                Blowfish_encipher(ref datal, ref datar);

                KS0[j] = datal;
                KS0[j + 1] = datar;
                //Console.WriteLine(KS0[j] + "," + KS0[j + 1]);
            }
            for (j = 0; j < 256; j += 2)
            {
                Blowfish_encipher(ref datal, ref datar);

                KS1[j] = datal;
                KS1[j + 1] = datar;
                //Console.WriteLine(KS1[j] + "," + KS1[j + 1]);
            }
            for (j = 0; j < 256; j += 2)
            {
                Blowfish_encipher(ref datal, ref datar);

                KS2[j] = datal;
                KS2[j + 1] = datar;
                //Console.WriteLine(KS2[j] + "," + KS2[j + 1]);
            }
            for (j = 0; j < 256; j += 2)
            {
                Blowfish_encipher(ref datal, ref datar);

                KS3[j] = datal;
                KS3[j + 1] = datar;
                //Console.WriteLine(KS3[j] + "," + KS3[j + 1]);
            }
            //Debugger.Break();
        }
        public void Blowfish_encipher(ref uint xl, ref uint xr)
        {

            uint Xl;
            uint Xr;
            uint temp;
            uint i;

            const int N = 16;
            Xl = xl;
            Xr = xr;

            for (i = 0; i < N; ++i)
            {
                Xl = Xl ^ P[i];
                Xr = TT(Xl) ^ Xr;

                temp = Xl;
                Xl = Xr;
                Xr = temp;
            }

            temp = Xl;
            Xl = Xr;
            Xr = temp;

            Xr = Xr ^ P[N];
            Xl = Xl ^ P[N + 1];
            //Console.WriteLine("Xr:{0:G} Xl:{1:G}", Xr, Xl);
            xl = Xl;
            xr = Xr;

        }
        public void Blowfish_decipher(ref uint xl, ref uint xr)
        {

            uint Xl;
            uint Xr;
            uint temp;
            uint i;
            Xl = xl;
            Xr = xr;
            const int N = 16;
            for (i = N + 1; i > 1; --i)
            {
                Xl = Xl ^ P[i];
                Xr = TT(Xl) ^ Xr;

                /*Exchange Xl and Xr*/
                temp = Xl;
                Xl = Xr;
                Xr = temp;
            }

            /*Exchange Xl and Xr*/
            temp = Xl;
            Xl = Xr;
            Xr = temp;

            Xr = Xr ^ P[1];
            Xl = Xl ^ P[0];

            xl = Xl;
            xr = Xr;

        }

    }
    #region Structs
    public struct My_Player
    {
        public uint ID;
        public string Name;
        public byte Job;
        public byte SubJob;
        public byte Level;
        public byte zoneid;
        public Zone_Info zone;
        public Position pos;
        public Position oldpos;
        public uint HP;
        public uint MP;
        public uint TP;
        public uint MaxHP;
        public uint MaxMP;
        public UInt16 targid;
        public UInt16 Str;
        public UInt16 Dex;
        public UInt16 Vit;
        public UInt16 Agi;
        public UInt16 Int;
        public UInt16 Mnd;
        public UInt16 Chr;
        public Inventory Inv;
    }
    public struct Zone_Info
    {
        public UInt16 ID;
        public UInt16 Weather;
        public uint Weather_time;
        public byte Music_Battle_Solo;
        public byte Music_Battle_Party;
        public byte Music_BG_Day;
        public byte Music_BG_Night;
    }
    public struct Position
    {
        public float X;
        public float Y;
        public float Z;
        public byte Rot;

        public UInt16 moving;

        public readonly bool HasChanged(Position other)
        {
            return X != other.X || Y != other.Y || Z != other.Z || Rot != other.Rot;
        }
        public override string ToString() => $"({X}, {Y}, {Z}, Rot={Rot})";
    }
    struct AccountInfo
    {
        public uint ID;
        public int Char_Slot;
        public string Username;
        public string Password;
        public byte[] SessionHash;
    }
    public struct Inventory
    {
        public Storage[] Container;
    }
    public struct Storage
    {
        public byte size;
        public UInt16 available;
        public InventorySlot[] slots;
    }
    public struct InventorySlot
    {
        public UInt16 itemid;
        public UInt32 quantity;
        public byte lockFlag;
        public byte[] extra;
    }
    public struct Config
    {
        public string user;
        public string password;
        public string server;
        public int char_slot;
    }
    public struct Entity
    {
        public bool IsValid;
        public byte Type;
        public uint ID;
        public UInt16 TargetIndex;
        public string Name;
        public byte Hpp;
        public byte Animation;
        public byte Status;
        public Position Pos;
    }
    [Flags]
    public enum EntityType : byte
    {
        None   = 0x00,
        PC     = 0x01,
        NPC    = 0x02,
        Mob    = 0x04,
        Pet    = 0x08,
        Ship   = 0x10,
        Trust  = 0x20,
        Fellow = 0x40
    }
    #endregion
    //0-1023 Mobs/NPCS/Ships
    //1024-1791 Players
    //1792-4095 Dynamic Pets/Trust/DE Npc/DE Mobs

}

public class TimestampTextWriter : TextWriter
{
    private readonly TextWriter _originalOut;
    public override Encoding Encoding => _originalOut.Encoding;

    public TimestampTextWriter(TextWriter originalOut)
    {
        _originalOut = originalOut;
    }

    public override void WriteLine(string value)
    {
        string timestamp = $"[{DateTime.Now:HH:mm:ss.fff}] ";
        _originalOut.WriteLine(timestamp + value);
    }
}