using System;
using System.Net.Sockets;
using System.IO;
using System.Threading;
using System.Net;
using System.Text;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Buffers.Binary;
using HeadlessFFXI.Networking.Packets;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using FFXISpellData;

namespace HeadlessFFXI
{
    public class Client
    {
        #region Vars
        const int Packet_Head = 28;
        uint[] startingkey = { 0x00000000, 0x00000000, 0x00000000, 0x00000000, 0xAD5DE056 }; // ;
        public bool chardata;
        public bool silient;
        private Blowfish _currentBlowfish;
        private readonly object _blowfishLock = new object();

        public Blowfish CurrentBlowfish
        {
            get
            {
                lock (_blowfishLock)
                {
                    return _currentBlowfish;
                }
            }
            set
            {
                lock (_blowfishLock)
                {
                    _currentBlowfish = value;
                }
            }
        }
        AccountInfo Account_Data;
        public My_Player Player_Data;
        public Entity[] Entity_List = new Entity[4096];
        Zlib myzlib;
        OutgoingQueue Packetqueue = new OutgoingQueue();
        PacketSender Packetsender;
        string loginserver = "127.0.0.1";
        TcpClient lobbyview;
        NetworkStream viewstream;
        TcpClient lobbydata;
        NetworkStream datastream;
        IPEndPoint RemoteIpEndPoint;
        UdpClient Gameserver;
        ushort ClientPacketID = 1;
        ushort ServerPacketID = 1;
        public event EventHandler<IncomingChatEventArgs> IncomingChat;
        public event EventHandler<IncomingPartyInviteEventArgs> IncomingPartyInvite;
        public bool Connected = false;
        private readonly PacketHandlerRegistry _registry = new();
        static MD5 hasher = System.Security.Cryptography.MD5.Create();
        private CancellationTokenSource _posCts;
        private CancellationTokenSource _incCts;
        private Task _incomingTask;
        private Task _positionTask;
        public SpellRepository Spellrepo;
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
            myzlib = new Zlib();
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
                    Player_Data = new My_Player();
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
            //Console.WriteLine("LobbyData0xA2 received:");
            //Console.WriteLine(BitConverter.ToString(data, 0, 72));
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

        public async Task Logout()
        {
            if (Gameserver != null)
            {
                byte[] data = new byte[8];

                OutgoingPacket logoutPacket2 = new OutgoingPacket(data);
                logoutPacket2.SetType(0x0D);
                logoutPacket2.SetSize(0x04);
                Packetqueue.Enqueue(logoutPacket2);

                await Task.Delay(600); // Give packets time to send

                data = new byte[8];
                BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(0x04), (ushort)0x03);
                BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(0x06), (ushort)0x03);

                OutgoingPacket logoutPacket = new OutgoingPacket(data);
                logoutPacket.SetType(0xE7);
                logoutPacket.SetSize(0x04);
                Packetqueue.Enqueue(logoutPacket);

                if (!silient)
                    Console.WriteLine("[Game]Log Out sent");

                await Task.Delay(600); // Give packets time to send
            }

            await CleanupGameSession();

            datastream?.Close();
            viewstream?.Close();

            Exit();
        }

        async Task GameserverStart()
        {
            Gameserver = new UdpClient();
            Gameserver.Connect(RemoteIpEndPoint);

            // Create new cancellation token for this game session
            _posCts = new CancellationTokenSource();
            _incCts = new CancellationTokenSource();
            // Start the incoming packet parser
            _incomingTask = Task.Run(() => ParseIncomingPacket(_incCts.Token), _incCts.Token);

            //Spellrepo = SpellLuaParser.ParseFile(@"D:\Windower\res\spells.lua");

            // Do initial zone login
            await Task.Run(() => Logintozone(true), _posCts.Token);
        }
        void ParseIncomingPacket(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    //Console.WriteLine($"[ParseIncoming] Waiting for packet...");
                    byte[] receiveBytes = Gameserver.Receive(ref RemoteIpEndPoint);
                    byte[] backUpBytes = new byte[receiveBytes.Length];
                    receiveBytes.CopyTo(backUpBytes, 0);
                    //Console.WriteLine($"[ParseIncoming] Received {receiveBytes.Length} bytes");
                    ushort server_packet_id = BitConverter.ToUInt16(receiveBytes, 0);
                    ushort client_packet_id = BitConverter.ToUInt16(receiveBytes, 2);
                    uint packet_time = BitConverter.ToUInt32(receiveBytes, 8);
                    ServerPacketID = server_packet_id;
                    //Console.WriteLine("ServerPacket {0:G} Client:{1:G}", server_packet_id , client_packet_id);
                    Packetsender.UpdateServerPacketId(server_packet_id);

                    //// Raw
                    //string[] bytes = BitConverter.ToString(receiveBytes).Split('-');
                    //for (int i = 0; i < bytes.Length; i += 16)
                    //{
                    //    Console.WriteLine("Incoming: " + string.Join(" ", bytes.Skip(i).Take(16)));
                    //}

                    byte[] deblown = new byte[receiveBytes.Length - Packet_Head];
                    byte[] blowhelper;
                    var currentBlowfish = CurrentBlowfish;
                    int k = 0;
                    for (int j = Packet_Head; j < receiveBytes.Length && receiveBytes.Length - j >= 8; j += 8)
                    {
                        blowhelper = new byte[8];
                        uint l = BitConverter.ToUInt32(receiveBytes, j);
                        uint r = BitConverter.ToUInt32(receiveBytes, j + 4);
                        currentBlowfish.Blowfish_decipher(ref l, ref r);
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
                    byte[] tomd5 = new byte[receiveBytes.Length - Packet_Head - 16];
                    System.Buffer.BlockCopy(receiveBytes, Packet_Head, tomd5, 0, tomd5.Length);
                    tomd5 = hasher.ComputeHash(tomd5);

                    ReadOnlySpan<byte> receivedSpan = receiveBytes;
                    ReadOnlySpan<byte> tail = receivedSpan.Slice(receivedSpan.Length - 16, 16);
                    if (!tail.SequenceEqual(tomd5))
                    {
                        Console.WriteLine("[ParseIncoming] No md5 match keyhash:{0:G}", currentBlowfish.Key);
                    }


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
                        //Console.WriteLine("[ParseIncoming]Type:{0:G}", type);
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
                    if (!ct.IsCancellationRequested)
                    {
                        if (!silient)
                            Console.WriteLine("[Game]Connection lost or refused, Exiting");
                        Exit();
                    }
                    break;
                }
                catch (OperationCanceledException)
                {
                    // Expected when cancelling
                    break;
                }
                catch (Exception ex)
                {
                    // Catch any other general exceptions
                        // Get stack trace for the exception with source file information
                    var st = new StackTrace(ex, true);
                    // Get the top stack frame
                    var frame = st.GetFrame(0);
                    // Get the line number from the stack frame
                    var line = frame.GetFileLineNumber();
                    Console.WriteLine($"An unexpected error occurred: {ex.Message}" + ex.ToString() + frame.GetFileName);
                }
            }
        }

        public async Task HandleZoneChange(uint ipRaw, ushort port)
        {
            var packet = new P00DBuilder();
            Packetqueue.Enqueue(packet.Build());

            var ipAddress = new IPAddress(BitConverter.GetBytes(ipRaw));
            var newEndpoint = new IPEndPoint(ipAddress, port);

            // Check if we actually need to change servers
            bool needsNewConnection = !RemoteIpEndPoint.Equals(newEndpoint);

            if (!silient)
                Console.WriteLine($"[Game]Zone change requested to {ipAddress}:{port} (NewConnection: {needsNewConnection})");

            // Set zoning flag to prevent position updates from sending during transition
            Player_Data.zoning = true;

            // Always stop position updates during zone change
            _posCts?.Cancel();

            // Wait for tasks to stop
            var tasksToWait = new List<Task>();
            if (_positionTask != null) tasksToWait.Add(_positionTask);

            if (tasksToWait.Count > 0)
            {
                try
                {
                    await Task.WhenAll(tasksToWait.Select(t => Task.WhenAny(t, Task.Delay(2000))));
                }
                catch (OperationCanceledException) { }
            }

            // Dispose packet sender
            Packetsender?.Dispose();
            Packetsender = null;

            // Dispose cancellation token
            _posCts?.Dispose();
            _posCts = null;

            _posCts = new CancellationTokenSource();

            if (needsNewConnection)
            {
                if (!silient)
                    Console.WriteLine($"[Game]Changing zone server to {newEndpoint.Address}:{port}");

                // Close old connection
                Gameserver?.Close();

                _incCts?.Cancel();

                // Wait for BOTH tasks to stop
                var tasksToWait2 = new List<Task>();
                if (_incomingTask != null) tasksToWait2.Add(_incomingTask);

                if (tasksToWait2.Count > 0)
                {
                    try
                    {
                        await Task.WhenAll(tasksToWait2.Select(t => Task.WhenAny(t, Task.Delay(2000))));
                    }
                    catch (OperationCanceledException) { }
                }

                // Dispose cancellation token
                _incCts?.Dispose();
                _incCts = null;

                _incCts = new CancellationTokenSource();

                // Create new connection
                Gameserver = new UdpClient();
                Gameserver.Connect(newEndpoint);
                RemoteIpEndPoint = newEndpoint;

                // Start new game session with new cancellation token
                _incomingTask = Task.Run(() => ParseIncomingPacket(_incCts.Token), _incCts.Token);
            }
            else
            {
                if (!silient)
                    Console.WriteLine("[Game]Zone change on same server");
            }

            // Give the incoming task a moment to start listening
            await Task.Delay(100);

            // Always do zone login (this will restart position updates via OutGoing_O11)
            await Task.Run(() => Logintozone(false), _posCts.Token);
        }

        private async Task CleanupGameSession()
        {
            // Dispose packet sender
            Packetsender?.Dispose();
            Packetsender = null;

            // Cancel all tasks
            _incCts?.Cancel();
            _posCts?.Cancel();
            // Wait for BOTH tasks to stop
            var tasksToWait = new List<Task>();
            if (_incomingTask != null) tasksToWait.Add(_incomingTask);
            if (_positionTask != null) tasksToWait.Add(_positionTask);

            if (tasksToWait.Count > 0)
            {
                try
                {
                    await Task.WhenAll(tasksToWait.Select(t => Task.WhenAny(t, Task.Delay(2000))));
                }
                catch (OperationCanceledException) { }
            }

            // Dispose cancellation token
            _incCts?.Dispose();
            _incCts = null;
            _posCts?.Dispose();
            _posCts = null;

            // Close UDP connection
            Gameserver?.Close();
        }

        void Logintozone(bool firstLogin)
        {
            startingkey[4] += 2;
            ClientPacketID = 1;
            ServerPacketID = 1;

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

            //if (!silient)
            //    Console.WriteLine("[Info]Blowfish key:" + BitConverter.ToString(byteArray).Replace("-", " "));
            if (!silient)
                Console.WriteLine("[Info]Blowfish hash:" + BitConverter.ToString(hashkey).Replace("-", " "));


            var shashkey = new sbyte[16];
            Buffer.BlockCopy(hashkey, 0, shashkey, 0, 16);
            CurrentBlowfish = new Blowfish();
            CurrentBlowfish.Init(shashkey, 16);

            // Keep PacketSender encryption in sync
            if (Packetsender != null)
                Packetsender.UpdateBlowfish(CurrentBlowfish);

            Packetsender = new PacketSender(Packetqueue, Gameserver, CurrentBlowfish, myzlib);

            // Let this send its own packet as it does not follow the normal packet rules
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
            //Console.WriteLine("Checksum: " + checksum);

            data[Packet_Head + 0x04] = checksum;

            byte[] tomd5 = new byte[data.Length - (Packet_Head + 16)];
            System.Buffer.BlockCopy(data, Packet_Head, tomd5, 0, tomd5.Length);
            tomd5 = hasher.ComputeHash(tomd5);
            System.Buffer.BlockCopy(tomd5, 0, data, data.Length - 16, 16);

            if (!silient)
                Console.WriteLine("[Game]Outgoing packet 0x0A, Zone in");
            try
            {
                Thread.Sleep(2000);
                Gameserver.Send(data, data.Length);
                Thread.Sleep(1000);
                if (firstLogin)
                {
                    ClientPacketID++;
                    input = BitConverter.GetBytes(ClientPacketID); //Packet count
                    System.Buffer.BlockCopy(input, 0, data, 0, input.Length);
                    Gameserver.Send(data, data.Length);
                }
            }
            catch (SocketException d)
            {
                if (!silient)
                    Console.WriteLine("[Game]Failed to connect retrying");
                startingkey[4] -= 2;
                Logintozone(firstLogin);
            }
            Packetsender.UpdateClientPacketId(ClientPacketID);

            #endregion
        }

        #region Combat Methods
        public void Attack(ushort targetIndex)
        {
            if (Entity_List[targetIndex] != null)
            {
                var attackPacket = new P01ABuilder(Entity_List[targetIndex].ID, targetIndex, 0x02, new uint[4]);
                Packetqueue.Enqueue(attackPacket.Build());
            }
        }

        public void Disengage(ushort targetIndex)
        {
            if (Entity_List[targetIndex] != null)
            {
                var attackPacket = new P01ABuilder(Entity_List[targetIndex].ID, targetIndex, 0x04, new uint[4]);
                Packetqueue.Enqueue(attackPacket.Build());
            }
        }

        public void Assist(ushort targetIndex)
        {
            if (Entity_List[targetIndex] != null)
            {
                var attackPacket = new P01ABuilder(Entity_List[targetIndex].ID, targetIndex, 0x0C, new uint[4]);
                Packetqueue.Enqueue(attackPacket.Build());
            }
        }

        public void RangedAttack(ushort targetIndex)
        {
            if (Entity_List[targetIndex] != null)
            {
                var attackPacket = new P01ABuilder(Entity_List[targetIndex].ID, targetIndex, 0x10, new uint[4]);
                Packetqueue.Enqueue(attackPacket.Build());
            }
        }

        public void WeaponSkill(ushort targetIndex, uint skillId)
        {
            if (Entity_List[targetIndex] != null)
            {
                var parms = new uint[4];
                parms[0] = skillId;
                var Packet = new P01ABuilder(Entity_List[targetIndex].ID, targetIndex, 0x07, parms);
                Packetqueue.Enqueue(Packet.Build());
            }
        }

        public void JobAbility(ushort targetIndex, uint skillId)
        {
            if (Entity_List[targetIndex] != null)
            {
                var parms = new uint[4];
                parms[0] = skillId;
                var Packet = new P01ABuilder(Entity_List[targetIndex].ID, targetIndex, 0x09, parms);
                Packetqueue.Enqueue(Packet.Build());
            }
        }

        public void CastMagic(ushort targetIndex, uint spellId)
        {
            if (Entity_List[targetIndex] != null)
            {
                var parms = new uint[4];
                parms[0] = spellId;
                var castPacket = new P01ABuilder(Entity_List[targetIndex].ID, targetIndex, 0x03, parms);
                Packetqueue.Enqueue(castPacket.Build());
            }
        }

        public bool CanUseSpell(uint spellId)
        {
            // Check if spell is learned
            int byteIndex = (int)(spellId / 8);
            int bitIndex = (int)(spellId % 8);
            if (byteIndex < 0 || byteIndex >= Player_Data.SpellList.Length)
                return false;

            bool learned = (Player_Data.SpellList[byteIndex] & (1 << bitIndex)) != 0;

            if (!learned)
                return false;

            return true;
/* Regex on spell parsing is boinked fix later
            var spell = Spellrepo.GetById(spellId);

            var mjob = Player_Data.Job;
            var mjob_level = Player_Data.Level;
            var sjob = Player_Data.SubJob;
            var sjob_level = Player_Data.SubLevel;

            if ((spell.Levels.ContainsKey(mjob) && spell.Levels[mjob] < mjob_level) || (spell.Levels.ContainsKey(sjob) && spell.Levels[sjob] < sjob_level))
                return true;

            return false;
*/
        }
        #endregion
        #region Movement
        public void Mount(uint mountId)
        {
            var parms = new uint[4];
            parms[0] = mountId;
            var castPacket = new P01ABuilder(Player_Data.ID, Player_Data.Index, 0x1A, parms);
            Packetqueue.Enqueue(castPacket.Build());
        }

        public void Dismount()
        {
            var parms = new uint[4];
            var castPacket = new P01ABuilder(Player_Data.ID, Player_Data.Index, 0x12, parms);
            Packetqueue.Enqueue(castPacket.Build());
        }
        #endregion
        #region Social
        public void SendTell(string User, string Message)
        {
            var tellPacket = new P0B6Builder(User, Message);
            Packetqueue.Enqueue(tellPacket.Build());

            if (!silient)
                Console.WriteLine("Sending Tell");
        }

        public void SendPartyInvite(string User)
        {
            var match = Entity_List.FirstOrDefault(e =>
                e != null &&
                e.IsValid &&
                (EntityType)e.Type == EntityType.PC &&
                e.Name != null &&
                e.Name == User);

            // Check if match is null
            if (match == null)
            {
                Console.WriteLine($"Party invite target not found: {User}");
                return;
            }

            var partyPacket = new P06EBuilder(match.ID, 0, 0);
            Packetqueue.Enqueue(partyPacket.Build());
        }

        public void PartyInviteResponce(bool Accept)
        {
            var partyPacket = new P074Builder(Accept);
            Packetqueue.Enqueue(partyPacket.Build());
        }

       #endregion
        #region Events
        public class IncomingChatEventArgs : EventArgs
        {
            public string Name { get; set; }
            public string Message { get; set; }
            public byte MessageType { get; set; }
            public bool IsGM { get; set; }
            public ushort ZoneID { get; set; }

            public IncomingChatEventArgs(string name, string message, byte messageType, bool isGM, ushort zoneID)
            {
                Name = name;
                Message = message;
                MessageType = messageType;
                IsGM = isGM;
                ZoneID = zoneID;
            }
        }

        public void OnIncomeChat(IncomingChatEventArgs e)
        {
            IncomingChat?.Invoke(this, e);
        }

        public class IncomingPartyInviteEventArgs : EventArgs
        {
            public string Name { get; set; }
            public uint CharId { get; set; }
            public ushort CharIndex { get; set; }

            public IncomingPartyInviteEventArgs(string name, uint charId, ushort charIndex)
            {
                Name = name;
                CharId = charId;
                CharIndex = charIndex;
            }
        }
        public void OnIncomePartyInvite(IncomingPartyInviteEventArgs e)
        {
            IncomingPartyInvite?.Invoke(this, e);
        }
        #endregion
        #region Lookup Functions
        public Entity GetEntityById(uint id)
        {
            return Entity_List.FirstOrDefault(e => e != null && e.IsValid && e.ID == id);
        }
        public Entity GetEntityByName(string name)
        {
            return Entity_List.FirstOrDefault(e =>
                e != null &&
                e.IsValid &&
                e.Name != null &&
                e.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        }
        public Entity GetEntityByIndex(ushort index)
        {
            if (Entity_List[index] != null && Entity_List[index].IsValid)
                return Entity_List[index];
            return null;
        }
        #endregion
        #region TempStruct
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
        #endregion
        #region Outgoing Packets

        // Zone in confirmation
        public void OutGoing_O11()
        {
            Thread.Sleep(500);

            byte[] data = new byte[8];
            data[4] = 2;

            OutgoingPacket zoneInPacket = new OutgoingPacket(data);
            zoneInPacket.SetType(0x11);
            zoneInPacket.SetSize(0x04);
            Packetqueue.Enqueue(zoneInPacket);

            if (!silient)
                Console.WriteLine("[Game]Outgoing packet 0x11, Zone in confirmation");

            // Clear zoning flag
            Player_Data.zoning = false;

            // Start position update task (reuses same cancellation token as game session)
            _positionTask = Task.Run(async () =>
            {
                while (!_posCts.Token.IsCancellationRequested)
                {
                    try
                    {
                        var posBuilder = new P015Builder(Player_Data);
                        Packetqueue.Enqueue(posBuilder.Build());
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("[Error] Outgoing task exception: " + ex);
                    }

                    try
                    {
                        await Task.Delay(400, _posCts.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
            }, _posCts.Token);

            OutGoing_ZoneInData();
        }

        void OutGoing_ZoneInData()
        {
            Console.WriteLine("[Game]Sending Zone in data serverpacket:{0:G}", ServerPacketID);
            if (chardata)
            {

                byte[] data = new byte[0x0C];
                OutgoingPacket zoneInPacket = new OutgoingPacket(data);
                zoneInPacket.SetType(0x0C);
                zoneInPacket.SetSize(0x06);
                Packetqueue.Enqueue(zoneInPacket);

                data = new byte[0x08];
                OutgoingPacket zoneInPacket2 = new OutgoingPacket(data);
                zoneInPacket2.SetType(0x61);
                zoneInPacket2.SetSize(0x04);
                Packetqueue.Enqueue(zoneInPacket2);

                data = new byte[0x1C];
                data[0x0A] = 0x14; //Action type
                OutgoingPacket zoneInPacket3 = new OutgoingPacket(data);
                zoneInPacket3.SetType(0x01A);
                zoneInPacket3.SetSize(0x0E);
                Packetqueue.Enqueue(zoneInPacket3);

                data = new byte[0x18];
                byte[] input = new byte[] { 0x02, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 }; //Language,Timestamp,Lengh,Start offset
                System.Buffer.BlockCopy(input, 0, data, 0x07, input.Length);
                OutgoingPacket zoneInPacket4 = new OutgoingPacket(data);
                zoneInPacket4.SetType(0x4B);
                zoneInPacket4.SetSize(0x0C);
                Packetqueue.Enqueue(zoneInPacket4);

                data = new byte[0x24];
                OutgoingPacket zoneInPacket5 = new OutgoingPacket(data);
                zoneInPacket5.SetType(0x0F);
                zoneInPacket5.SetSize(0x12);
                Packetqueue.Enqueue(zoneInPacket5);

                //input = BitConverter.GetBytes(((UInt16)0x0DB)); //Packet type
                //System.Buffer.BlockCopy(input, 0, data, new_Head, input.Length);
                //input = new byte[] { 0x14 }; //Size
                //System.Buffer.BlockCopy(input, 0, data, new_Head + 0x01, input.Length);
                //input = BitConverter.GetBytes(ClientPacketID); //Packet count
                //System.Buffer.BlockCopy(input, 0, data, new_Head + 0x02, input.Length);
                //input = new byte[] { 0x02 }; //Language
                //System.Buffer.BlockCopy(input, 0, data, new_Head + 0x24, input.Length);
                //new_Head = new_Head + (0x14 * 2);

                data = new byte[0x04];
                OutgoingPacket zoneInPacket6 = new OutgoingPacket(data);
                zoneInPacket6.SetType(0x5A);
                zoneInPacket6.SetSize(0x02);
                Packetqueue.Enqueue(zoneInPacket6);

                if (!silient)
                    Console.WriteLine("[Game]Outgoing packet multi,Sending Post zone data requests");
            }
        }
        #endregion
        static void Exit()
        {
            System.Environment.Exit(1);
        }

    }

    #region Structs
    public class My_Player
    {
        public uint ID;
        public ushort Index;
        public string Name;
        public byte Job;
        public byte SubJob;
        public byte Level;
        public byte SubLevel = 37;
        public byte zoneid;
        public Zone_Info zone;
        public Position pos;
        public Position oldpos;
        public uint HP;
        public uint MP;
        public uint TP;
        public uint MaxHP;
        public uint MaxMP;
        public ushort Str;
        public ushort Dex;
        public ushort Vit;
        public ushort Agi;
        public ushort Int;
        public ushort Mnd;
        public ushort Chr;
        public uint TargetId;
        public Inventory Inv;
        public Equipment[] Equip;
        public byte[] SpellList = new byte[128];
        public bool zoning;
    }
    public struct Equipment
    {
        public byte InventorySlot;
        public byte Container;
    }
    public struct Zone_Info
    {
        public ushort ID;
        public ushort Weather;
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

        public ushort moving;

        public readonly bool HasChanged(Position other, float tolerance = 0.01f)
        {
            return Math.Abs(X - other.X) > tolerance ||
                   Math.Abs(Y - other.Y) > tolerance ||
                   Math.Abs(Z - other.Z) > tolerance ||
                   Rot != other.Rot;
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
        public ushort available;
        public InventorySlot[] slots;
    }
    public struct InventorySlot
    {
        public ushort itemid;
        public uint quantity;
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
    public class Entity
    {
        public bool IsValid = false;
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
