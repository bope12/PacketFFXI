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
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json;
using PathFinder.Common;
using System.Buffers;
using HeadlessFFXI.Packets;
using HeadlessFFXI.Packets.Outgoing;

namespace HeadlessFFXI
{
    public class Client
    {
        #region Vars
        const int PacketHead = 28;
        private uint[] _startingKey = { 0x00000000, 0x00000000, 0x00000000, 0x00000000, 0xAD5DE056 };
        public bool CharData;
        public byte LogLevel;
        private readonly Lock _blowfishLock = new Lock();

        public Blowfish CurrentBlowfish
        {
            get
            {
                lock (_blowfishLock)
                {
                    return field;
                }
            }
            set
            {
                lock (_blowfishLock)
                {
                    field = value;
                }
            }
        }
        private AccountInfo _accountData;
        public MyPlayer PlayerData;
        public Entity[] EntityList = new Entity[4096];
        private readonly Zlib _zlib = new Zlib();
        public OutgoingQueue PacketQueue = new OutgoingQueue();
        private PacketSender _packetSender;
        private readonly string _loginServerIp;
        private TcpClient _lobbyViewConnection;
        private NetworkStream _viewsStream;
        private TcpClient _lobbyDataConnection;
        private NetworkStream _dataStream;
        private IPEndPoint _remoteIpEndPoint;
        private UdpClient _gameServerConnection;
        public ushort ClientPacketId = 1;
        public ushort ServerPacketId = 1;
        public event EventHandler<IncomingChatEventArgs> IncomingChat;
        public event EventHandler<IncomingPartyInviteEventArgs> IncomingPartyInvite;
        public bool Connected;
        private readonly PacketHandlerRegistry _registry = new();
        private readonly MD5 _hasher = MD5.Create();
        private CancellationTokenSource _posCts;
        private CancellationTokenSource _incCts;
        private Task _incomingTask;
        private Task _positionTask;
        public SpellRepository Spellrepo;
        public FFXINAV Nav;
        #endregion
        #region Loginproccess
        public Client(Config cfg, bool full = true, byte log = 4)
        {
            _accountData.Username = cfg.user;
            _accountData.Password = cfg.password;
            _accountData.Char_Slot = cfg.char_slot;
            _loginServerIp = cfg.server;
            CharData = full;
            LogLevel = log;
        }
        public async Task<bool> Login()
        {
            _zlib.Init();
            try
            {
                //AppContext.SetSwitch("System.Net.Security.UseNetworkFramework", true);
                var client = new TcpClient(_loginServerIp, 54231);

                await using var sslStream = new SslStream(
                client.GetStream(),
                leaveInnerStreamOpen: false,
                userCertificateValidationCallback: (sender, certificate, chain, sslPolicyErrors) => true
                );

                var jsonData = new Dictionary<string, object>
                {
                    ["username"] = _accountData.Username,
                    ["password"] = _accountData.Password,
                    ["otp"] = 0,
                    ["new_password"] = "",
                    ["version"] = new[] { 2, 0, 0 },
                    ["command"] = 0x10
                };

                var options = new JsonSerializerOptions { WriteIndented = false };
                var jsonString = JsonSerializer.Serialize(jsonData, options);

                var jsonLen = Encoding.UTF8.GetByteCount(jsonString);
                var data = new byte[jsonLen];

                Encoding.UTF8.GetBytes(jsonString, 0, jsonString.Length, data, 0);

                var ssloptions = new SslClientAuthenticationOptions
                {
                    TargetHost = _loginServerIp,
                    EnabledSslProtocols = SslProtocols.Tls13,
                    CertificateRevocationCheckMode = X509RevocationMode.NoCheck
                };

                try
                {
                    await sslStream.AuthenticateAsClientAsync(ssloptions);
                    ShowInfo($"[Login]Connected using {sslStream.SslProtocol}");
                }
                catch (Exception ex)
                {
                    ShowError($"[Login]Handshake failed: {ex.GetType().Name} - {ex.Message}");
                    if (ex.InnerException != null)
                        ShowError($"[Login]Inner: {ex.InnerException.GetType().Name} - {ex.InnerException.Message}");
                }

                await sslStream.WriteAsync(data, 0, jsonLen);
                await sslStream.FlushAsync();

                var indata = new byte[8192];
                var bytesRead = await sslStream.ReadAsync(indata.AsMemory(0, 8192));
                sslStream.Close();

                var rawResponse = Encoding.UTF8.GetString(indata, 0, bytesRead).Replace("'", "\"");
                //Console.WriteLine(rawResponse);
                Dictionary<string, JsonElement>? jsonInData = null;
                try
                {
                    jsonInData = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(rawResponse);
                }
                catch (JsonException ex)
                {
                    ShowError($"[Login]Invalid JSON: {ex.Message}");
                    throw;
                }
                catch (DecoderFallbackException ex)
                {
                    ShowError($"[Login]UTF-8 decoding error: {ex.Message}");
                    throw;
                }

                if (jsonInData == null)
                    throw new InvalidDataException("Failed to parse login response.");
                int Result = 0;
                int AccountId = 0;
                byte[] SessionHash = new byte[1];
                // Extract fields safely
                if (jsonInData.TryGetValue("result", out var resultElem))
                    Result = ((JsonElement)resultElem).GetInt32();

                if (jsonInData.TryGetValue("account_id", out var accountElem))
                    AccountId = ((JsonElement)accountElem).GetInt32();

                if (jsonInData.TryGetValue("session_hash", out var sessionElem))
                {
                    List<byte> temp = new List<byte>();
                    foreach (var item in ((JsonElement)sessionElem).EnumerateArray())
                    {
                        temp.Add(((JsonElement)item).GetByte());
                    }
                    SessionHash = temp.ToArray();
                }

                switch (Result)
                {
                    case 0x0001: //Login Success
                        _accountData.ID = (uint)AccountId;
                        ShowInfo($"[Login]Account id:{_accountData.ID}");
                        _accountData.SessionHash = SessionHash;
                        _lobbyDataConnection = new TcpClient(new IPEndPoint(IPAddress.Any, 0));
                        _lobbyDataConnection.Client.SetSocketOption(
                            SocketOptionLevel.Socket,
                            SocketOptionName.ReuseAddress,
                            true);
                        await _lobbyDataConnection.ConnectAsync(_loginServerIp, 54230);
                        _dataStream = _lobbyDataConnection.GetStream();
                        byte[] dataByte = new byte[28];
                        dataByte[0] = 0xFE;
                        Buffer.BlockCopy(_accountData.SessionHash, 0, dataByte, 12, _accountData.SessionHash.Length);
                        await _dataStream.WriteAsync(dataByte, 0, dataByte.Length);
                        await LobbyDataA1();
                        ShowInfo("After A1");
                        await LobbyView0x26();
                        ShowInfo("After 26");
                        await LobbyView0x1F();
                        ShowInfo("After 1f");
                        await LobbyData0xA1();
                        ShowInfo("After A1");
                        await LobbyView0x24();
                        ShowInfo("After 24");
                        await LobbyView0x07();
                        ShowInfo("After 07");
                        await LobbyData0xA2();
                        ShowInfo("After A2");
                        break;
                    case 0x0002:
                        ShowWarn("[Login]Login failed, Trying to create new account");
                        await AccountCreation();
                        break;
                    default:
                        ShowError($"[Login]Login failed Unsure Code:{Result}");
                        break;
                }
                client.Close();
            }
            catch (SocketException d)
            {
                switch (d.ErrorCode)
                {
                    case 10061:
                        ShowError("[Login]No responce from server");
                        break;
                    default:
                        ShowError($"[Login]SocketError received:{d.ErrorCode}, {d.Message}");
                        break;
                }
            }
            return false;
        }
        private async Task AccountCreation()
        {
            TcpClient client = new TcpClient(_loginServerIp, 54231);

            using var sslStream = new SslStream(
            client.GetStream(),
            leaveInnerStreamOpen: false,
            userCertificateValidationCallback: (sender, certificate, chain, sslPolicyErrors) => true
            );

            var jsonData = new Dictionary<string, object>
            {
                ["username"] = _accountData.Username,
                ["password"] = _accountData.Password,
                ["otp"] = 0,
                ["new_password"] = "",
                ["version"] = new int[] { 2, 0, 0 },
                ["command"] = 0x20
            };

            var options = new JsonSerializerOptions { WriteIndented = false };
            string jsonString = JsonSerializer.Serialize(jsonData, options);

            int jsonLen = Encoding.UTF8.GetByteCount(jsonString);
            byte[] data = new byte[jsonLen];

            Encoding.UTF8.GetBytes(jsonString, 0, jsonString.Length, data, 0);

            var ssloptions = new SslClientAuthenticationOptions
            {
                TargetHost = _loginServerIp,
                EnabledSslProtocols = SslProtocols.Tls13,
                CertificateRevocationCheckMode = X509RevocationMode.NoCheck
            };

            try
            {
                await sslStream.AuthenticateAsClientAsync(ssloptions);
                ShowInfo($"[Login]Connected using {sslStream.SslProtocol}");
            }
            catch (Exception ex)
            {
                ShowError($"[Login]Handshake failed: {ex.GetType().Name} - {ex.Message}");
                if (ex.InnerException != null)
                    ShowError($"[Login]Inner: {ex.InnerException.GetType().Name} - {ex.InnerException.Message}");
            }

            await sslStream.WriteAsync(data, 0, jsonLen);
            await sslStream.FlushAsync();

            byte[] indata = new byte[8192];
            int bytesRead = await sslStream.ReadAsync(indata.AsMemory(0, 8192));
            sslStream.Close();

            string rawResponse = Encoding.UTF8.GetString(indata, 0, bytesRead).Replace("'", "\"");
            //Console.WriteLine(rawResponse);
            Dictionary<string, JsonElement>? jsonInData = null;
            try
            {
                jsonInData = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(rawResponse);
            }
            catch (JsonException ex)
            {
                ShowError($"[Login]Invalid JSON: {ex.Message}");
                throw;
            }
            catch (DecoderFallbackException ex)
            {
                ShowError($"[Login]UTF-8 decoding error: {ex.Message}");
                throw;
            }

            if (jsonInData == null)
                throw new InvalidDataException("Failed to parse login response.");
            int Result = 0;
            if (jsonInData.TryGetValue("result", out var resultElem))
                Result = ((JsonElement)resultElem).GetInt32();

            switch (Result)
            {
                case 0x03: //Account creation success
                    ShowInfo("[Login]New account created");
                    Login();
                    break;
                case 0x04: //Acount already exists
                    ShowWarn("[Login]Account already exists, Check your username/password");
                    break;
                case 0x08: //Account creation disabled
                    ShowWarn("[Login]Account creation is disabled, If your account already exists check your username/password");
                    break;
                case 0x09: //Acount creation error
                    ShowWarn("[Login]Server failed to create a new account");
                    break;
                default:
                    break;
            }
        }

        //Setup connection to the lobbyData handler
        private async Task LobbyDataA1()
        {
            byte[] data = new byte[28];
            data[0] = 0xA1;
            Buffer.BlockCopy(BitConverter.GetBytes(_accountData.ID), 0, data, 1, 4);
            Buffer.BlockCopy(((IPEndPoint)_lobbyDataConnection.Client.RemoteEndPoint).Address.GetAddressBytes(), 0, data, 5, 4);
            Buffer.BlockCopy(_accountData.SessionHash, 0, data, 12, _accountData.SessionHash.Length);
            try
            {
                _dataStream.Write(data, 0, data.Length);
            }
            catch(Exception ex)
            {
                ShowError(ex.ToString());
            }
        }

        //The rest of the login process is a linary set of send receive packets with both the lobbyData and lobbyView connections
        //Client ver check, receive information about account
        private async Task LobbyView0x26()
        {
            _lobbyViewConnection = new TcpClient(new IPEndPoint(IPAddress.Any, 0));
            _lobbyViewConnection.Client.SetSocketOption(
                            SocketOptionLevel.Socket,
                            SocketOptionName.ReuseAddress,
                            true);
            await _lobbyViewConnection.ConnectAsync(_loginServerIp, 54001);
            _viewsStream = _lobbyViewConnection.GetStream();

            byte[] ver = Encoding.ASCII.GetBytes("30251000_0");
            byte[] data = new byte[152];
            data[8] = 0x26;
            Buffer.BlockCopy(ver, 0, data, 116, 10);
            Buffer.BlockCopy(_accountData.SessionHash, 0, data, 12, _accountData.SessionHash.Length);
            await _viewsStream.WriteAsync(data, 0, 152);
            data = new byte[40];
            await _viewsStream.ReadAsync(data, 0, 40);
            //Console.WriteLine("[Info]Expantion Bitmask:{0:D}", BitConverter.ToUInt16(data, 32));
            //Console.WriteLine("[Info]Feature Bitmask:{0:D}", BitConverter.ToUInt16(data, 36));
        }

        private async Task LobbyView0x1F()
        {
            byte[] data = new byte[44];
            data[8] = 0x1F;
            Buffer.BlockCopy(_accountData.SessionHash, 0, data, 12, _accountData.SessionHash.Length);
            await _viewsStream.WriteAsync(data, 0, 44);
        }

        //Request char list
        private async Task LobbyData0xA1()
        {
            byte[] data = new byte[28];
            Buffer.BlockCopy(BitConverter.GetBytes(_accountData.ID), 0, data, 1, 4);
            Buffer.BlockCopy(((System.Net.IPEndPoint)_lobbyDataConnection.Client.RemoteEndPoint).Address.GetAddressBytes(), 0, data, 5, 4);
            data[0] = 0xA1;
            //datastream.Flush();
            await _dataStream.WriteAsync(data);
            data = new byte[328];
            await _dataStream.ReadAsync(data, 0, 328);
            data = new byte[2272];
            await _viewsStream.ReadAsync(data, 0, 2272);
            if (BitConverter.ToUInt32(data, 36) != 0)
            {
                if (BitConverter.ToUInt32(data, 36 + (_accountData.Char_Slot * 140)) != 0)
                {
                    PlayerData = new MyPlayer();
                    PlayerData.ID = BitConverter.ToUInt32(data, 36 + (_accountData.Char_Slot * 140));
                    PlayerData.Name = Encoding.UTF8.GetString(data, 44 + (_accountData.Char_Slot * 140), 16).TrimEnd('\0');
                    PlayerData.Job = data[46 + 32 + (_accountData.Char_Slot * 140)];
                    PlayerData.Level = data[73 + 32 + (_accountData.Char_Slot * 140)];
                    PlayerData.zoneid = data[72 + 32 + (_accountData.Char_Slot * 140)];
                }
                else
                {
                    ShowWarn($"[Login]No charater in slot:{_accountData.Char_Slot + 1} defaulting to slot 1");
                    _accountData.Char_Slot = 0;
                    PlayerData.ID = BitConverter.ToUInt32(data, 36);
                    string name = Encoding.UTF8.GetString(data, 44, 16).TrimEnd('\0');
                    PlayerData.Name = name.Substring(0, name.IndexOfAny(new char[] { ' ', '0', '1', '2', '3', '4', '5', '6', '7', '8', '9' }));
                    PlayerData.Job = data[46 + 32];
                    PlayerData.Level = data[73 + 32];
                    PlayerData.zoneid = data[72 + 32];
                }
                ShowInfo($"[Login]Name:{PlayerData.Name} CharID:{PlayerData.ID} Job:{PlayerData.Job} Level:{PlayerData.Level}");
            }
            else
            {
                //Create a charater
                ShowWarn("[Login]No charater's on account");
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

        private void LobbyView0x22()
        {
            byte[] data = new byte[48];
            data[8] = 0x22;
            Buffer.BlockCopy(_accountData.SessionHash, 0, data, 12, _accountData.SessionHash.Length);
            byte[] input = Encoding.ASCII.GetBytes(_accountData.Username.Length > 16 ? _accountData.Username.Substring(0, 16) : _accountData.Username);
            Buffer.BlockCopy(input, 0, data, 32, input.Length);
            _viewsStream.Write(data, 0, data.Length);
            data = new byte[0x24 + 16];
            _viewsStream.Read(data, 0, data.Length);
            if (BitConverter.ToInt16(data, 32) == 313)
            {
                ShowError("[Login]Name taken or invalid");
            }
            else
            {
                LobbyView0x21();
            }
        }

        //Second part of creating a charater
        private void LobbyView0x21()
        {
            byte[] data = new byte[64];
            data[8] = 0x21;
            Buffer.BlockCopy(_accountData.SessionHash, 0, data, 12, _accountData.SessionHash.Length);
            data[48] = 1; //Race
            data[50] = 1; //Job
            data[54] = 2; //Nation
            data[57] = 1; //Size
            data[60] = 1; //Face
            _viewsStream.Write(data, 0, data.Length);
            data = new byte[64];
            _viewsStream.Read(data, 0, 64);
            if (data[0] == 0x20)
            {
                ShowInfo("[Login]Char created");
                LobbyView0x1F();
            }
            else
            {
                ShowError("[Login]Failed to create a char exiting");
            }
        }

        private async Task LobbyView0x24()
        {
            /*
            byte[] data = new byte[44];
            data[8] = 0x24;
            try
            {
                ShowInfo("[Login]0x24 out");
                viewstream.Write(data, 0, 44);
            }
            catch(Exception e)
            {
                ShowInfo("[Login]0x24 Error in write");
            }
            data = new byte[64];
            try
            {
                ShowInfo("[Login]0x24 in1");
                int bytesreturned = await viewstream.ReadAsync(data, 0, 64);
                ShowInfo("[Login]0x24 in2");
            }
            catch (Exception e)
            {
                ShowError([Login]0x24 Error in read");
            }
            ShowInfo("[Login]{0:G} Server", System.Text.Encoding.UTF8.GetString(data, 36, 16));
            */
        }

        private async Task LobbyView0x07()
        {
            byte[] data = new byte[88];
            data[8] = 0x07;
            byte[] id = BitConverter.GetBytes(PlayerData.ID);
            Buffer.BlockCopy(id, 0, data, 28, id.Length);
            Buffer.BlockCopy(Encoding.ASCII.GetBytes(PlayerData.Name), 0, data, 36, PlayerData.Name.Length);
            Buffer.BlockCopy(_accountData.SessionHash, 0, data, 12, _accountData.SessionHash.Length);
            await _viewsStream.WriteAsync(data, 0, 88);
        }

        // Packet with key for blowfish
        private async Task LobbyData0xA2()
        {
            _viewsStream.Flush();
            //Starting Key
            byte[] data = {
                0xA2, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x58, 0xE0, 0x5D, 0xAD, 0x00,
                0x00, 0x00, 0x00 };
            Thread.Sleep(2000);
            await _dataStream.WriteAsync(data, 0, 25);
            data = new byte[72];
            await _viewsStream.ReadExactlyAsync(data.AsMemory(0, 72));
            //Console.WriteLine("LobbyData0xA2 received:");
            //Console.WriteLine(BitConverter.ToString(data, 0, 72));
            uint error = BitConverter.ToUInt16(data, 32);
            switch (error)
            {
                case 305:
                case 321:
                case 201:
                    ShowWarn("[Login]Failed to pass us to the gameserver: " + error);
                    return;
                default:
                    Connected = true;
                    break;
            }
            uint zoneip = BitConverter.ToUInt32(data, 0x38);
            uint zoneport = BitConverter.ToUInt16(data, 0x3C);
            uint searchip = BitConverter.ToUInt32(data, 0x40);
            uint searchport = BitConverter.ToUInt16(data, 0x44);
            _remoteIpEndPoint = new IPEndPoint(zoneip, Convert.ToInt32(zoneport));
            ShowInfo("[Login]Handed off to gameserver " + _remoteIpEndPoint.Address + ":" + zoneport);
            GameserverStart();
        }
        #endregion

        public async Task Logout()
        {
            if (_gameServerConnection != null)
            {
                byte[] data = new byte[8];

                var packet = new P00DBuilder();
                PacketQueue.Enqueue(packet.Build());

                await Task.Delay(600); // Give packets time to send

                data = new byte[8];
                BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(0x04), (ushort)0x03);
                BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(0x06), (ushort)0x03);

                OutgoingPacket logoutPacket = new OutgoingPacket(data);
                logoutPacket.SetType(0xE7);
                logoutPacket.SetSize(0x04);
                PacketQueue.Enqueue(logoutPacket);

                ShowInfo("[Game]Log Out sent");

                await Task.Delay(600); // Give packets time to send
            }

            await CleanupGameSession();

            _dataStream?.Close();
            _viewsStream?.Close();
        }

        async Task GameserverStart()
        {
            var localEp = new IPEndPoint(IPAddress.Any, 0); // OS assigns port
            _gameServerConnection = new UdpClient(localEp);
            _gameServerConnection.Client.SetSocketOption(
                SocketOptionLevel.Socket,
                SocketOptionName.ReuseAddress,
                true
            );
            _gameServerConnection.Connect(_remoteIpEndPoint);

            // Create new cancellation token for this game session
            _posCts = new CancellationTokenSource();
            _incCts = new CancellationTokenSource();
            // Start the incoming packet parser
            _incomingTask = Task.Run(() => ParseIncomingPacket(_incCts.Token), _incCts.Token);

            Spellrepo = SpellLuaParser.ParseFile(@"D:\Windower\res\spells.lua");
            Nav = new FFXINAV();

            // Do initial zone login
            await Task.Run(() => Logintozone(true), _posCts.Token);
        }
        private void ParseIncomingPacket(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    //Console.WriteLine($"[ParseIncoming] Waiting for packet...");
                    var receiveBytes = _gameServerConnection.Receive(ref _remoteIpEndPoint);

                    //Console.WriteLine($"[ParseIncoming] Received {receiveBytes.Length} bytes");
                    var serverPacketId = BitConverter.ToUInt16(receiveBytes, 0);
                    //ushort client_packet_id = BitConverter.ToUInt16(receiveBytes, 2);
                    //uint packet_time = BitConverter.ToUInt32(receiveBytes, 8);
                    ServerPacketId = serverPacketId;
                    //Console.WriteLine("ServerPacket {0:G} Client:{1:G}", server_packet_id , client_packet_id);
                    _packetSender.UpdateServerPacketId(serverPacketId);

                    //// Raw
                    //string[] bytes = BitConverter.ToString(receiveBytes).Split('-');
                    //for (int i = 0; i < bytes.Length; i += 16)
                    //{
                    //    Console.WriteLine("Incoming: " + string.Join(" ", bytes.Skip(i).Take(16)));
                    //}

                    var deblown = new byte[receiveBytes.Length - PacketHead];
                    var currentBlowfish = CurrentBlowfish;
                    var k = 0;
                    for (var j = PacketHead; j < receiveBytes.Length && receiveBytes.Length - j >= 8; j += 8)
                    {
                        var blowhelper = new byte[8];
                        var l = BitConverter.ToUInt32(receiveBytes, j);
                        var r = BitConverter.ToUInt32(receiveBytes, j + 4);
                        currentBlowfish.Blowfish_decipher(ref l, ref r);
                        Buffer.BlockCopy(BitConverter.GetBytes(l), 0, blowhelper, 0, 4);
                        Buffer.BlockCopy(BitConverter.GetBytes(r), 0, blowhelper, 4, 4);
                        Buffer.BlockCopy(blowhelper, 0, deblown, j - PacketHead, 8);
                        k += 8;
                    }
                    Buffer.BlockCopy(deblown, 0, receiveBytes, PacketHead, k);

                    //Deblowfished
                    //bytes = BitConverter.ToString(receiveBytes).Split('-');
                    //for (int i = 0; i < bytes.Length; i += 16)
                    //{
                    //    Console.WriteLine("Decode:   " + string.Join(" ", bytes.Skip(i).Take(16)));
                    //}
                    var tomd5 = new byte[receiveBytes.Length - PacketHead - 16];
                    Buffer.BlockCopy(receiveBytes, PacketHead, tomd5, 0, tomd5.Length);
                    tomd5 = _hasher.ComputeHash(tomd5);

                    ReadOnlySpan<byte> receivedSpan = receiveBytes;
                    var tail = receivedSpan.Slice(receivedSpan.Length - 16, 16);
                    if (!tail.SequenceEqual(tomd5))
                    {
                        ShowError($"[ParseIncoming]No md5 match keyhash:{currentBlowfish.Key}");
                        continue;
                    }


                    //Zlib compress's all but header
                    var packetsize = BitConverter.ToUInt32(receiveBytes, receiveBytes.Length - 20);
                    var buffer = new byte[(int)Math.Ceiling(packetsize / 8m)];
                    Buffer.BlockCopy(receiveBytes, PacketHead + 1, buffer, 0, buffer.Length);
                    //Console.WriteLine("ToDelib:   " + BitConverter.ToString(buffer).Replace("-", " "));
                    var w = 0;
                    var pos = _zlib.jump[0];
                    var outbuf = ArrayPool<byte>.Shared.Rent(4000);
                    try
                    {
                        //Console.WriteLine(buffer.Length + ":" + packetsize);
                        for (var i = 0; i < packetsize && w < 4000; i++)
                        {
                            var s = (buffer[i / 8] >> (i & 7)) & 1;
                            pos = _zlib.jump[pos + s];
                            //Console.WriteLine("{0:G} : {1:G}  0,1 {2:G},{3:G}", s, pos, myzlib.jump[pos], myzlib.jump[pos+1]);
                            if (_zlib.jump[pos] != 0 || _zlib.jump[pos + 1] != 0)
                            {
                                //Console.WriteLine("Pos:{0:G} not both zero", pos);
                                continue;
                            }
                            //Console.WriteLine("DATA:{0:G}", myzlib.jump[pos + 3]);
                            outbuf[w++] = BitConverter.GetBytes(_zlib.jump[pos + 3])[0];
                            //Console.WriteLine(BitConverter.GetBytes(myzlib.jump[pos + 3])[0]);
                            pos = _zlib.jump[0];
                        }
                        var final = new byte[w];
                        Buffer.BlockCopy(outbuf, 0, final, 0, w);
                        //Console.WriteLine("Dezlib size:" + final.Length);
                        //bytes = BitConverter.ToString(final).Split('-');
                        //for (int i = 0; i < bytes.Length; i += 16)
                        //{
                        //    Console.WriteLine("Dezlib:   " + string.Join(" ", bytes.Skip(i).Take(16)));
                        //}

                        var index = 0;

                        while (index + 2 <= final.Length)
                        {
                            // Make sure we can at least read the type and size
                            var type = (ushort)(BitConverter.ToUInt16(final, index) & 0x1FF);
                            //Console.WriteLine("[ParseIncoming]Type:{0:G}", type);
                            var size = final[index + 1] & 0x0FE;

                            var packetLength = size * 2;

                            // Safety check
                            if (packetLength <= 0 || index + packetLength > final.Length)
                            {
                                //Console.WriteLine($"[WARN] Invalid packet {type} length at index {index}. Size={packetLength}, Remaining={final.Length - index}");
                                break; // prevent crash
                            }

                            var packetData = new ReadOnlySpan<byte>(final, index, packetLength);
                            _registry.TryHandle(this, type, packetData);

                            index += packetLength;
                        }
                    }
                    finally
                    {
                        ArrayPool<byte>.Shared.Return(outbuf);
                    }
                }
                catch (SocketException)
                {
                    if (!ct.IsCancellationRequested)
                    {
                        ShowWarn("[ParseIncoming]Connection lost or refused, Exiting");
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
                    ShowError($"[ParseIncoming]An unexpected error occurred: {ex.Message} {ex} {frame.GetFileName()}");
                }
            }
        }

        public async Task HandleZoneChange(uint ipRaw, ushort port)
        {
            var packet = new P00DBuilder();
            PacketQueue.Enqueue(packet.Build());

            var ipAddress = new IPAddress(BitConverter.GetBytes(ipRaw));
            var newEndpoint = new IPEndPoint(ipAddress, port);

            // Check if we actually need to change servers
            var needsNewConnection = !_remoteIpEndPoint.Equals(newEndpoint);

            ShowInfo($"[HandleZoneChange]Zone change requested to {ipAddress}:{port} (NewConnection: {needsNewConnection})");

            // Set zoning flag to prevent position updates from sending during transition
            PlayerData.zoning = true;

            //Clear Entity List
            Array.Clear(EntityList, 0, EntityList.Length);

            // Always stop position updates during zone change
            await _posCts?.CancelAsync()!;

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
            _packetSender?.Dispose();
            await Task.Delay(100);
            _packetSender = null;

            // Dispose cancellation token
            _posCts?.Dispose();
            await Task.Delay(100);
            _posCts = null;

            _posCts = new CancellationTokenSource();

            if (needsNewConnection)
            {
                ShowInfo($"[HandleZoneChange]Changing zone server to {newEndpoint.Address}:{port}");

                // Close old connection
                _gameServerConnection?.Dispose();

                await _incCts?.CancelAsync()!;

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
                _gameServerConnection = new UdpClient();
                _gameServerConnection.Connect(newEndpoint);
                _remoteIpEndPoint = newEndpoint;

                // Start new game session with new cancellation token
                _incomingTask = Task.Run(() => ParseIncomingPacket(_incCts.Token), _incCts.Token);
            }

            // Give the incoming task a moment to start listening
            await Task.Delay(100);

            // Always do zone login (this will restart position updates via OutGoing_O11)
            await Task.Run(() => Logintozone(false), _posCts.Token);

            LogMemoryUsage("After Zone");
        }

        private async Task CleanupGameSession()
        {
            // Dispose packet sender
            _packetSender?.Dispose();
            _packetSender = null;

            // Cancel all tasks
            await _incCts?.CancelAsync()!;
            await _posCts?.CancelAsync()!;
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
            _gameServerConnection?.Close();
        }

        private void Logintozone(bool firstLogin)
        {
            _startingKey[4] += 2;
            ClientPacketId = 1;
            ServerPacketId = 1;

            var byteArray = new byte[_startingKey.Length * sizeof(uint)];
            Buffer.BlockCopy(_startingKey, 0, byteArray, 0, byteArray.Length);
            var hashkey = _hasher.ComputeHash(byteArray, 0, 20);

            for (var i = 0; i < 16; ++i)
            {
                if (hashkey[i] == 0)
                {
                    Array.Clear(hashkey, i, 16 - i);
                    break;
                }
            }

            ShowInfo("[Logintozone]Blowfish key:" + BitConverter.ToString(byteArray).Replace("-", " "));
            ShowInfo("[Logintozone]Blowfish hash:" + BitConverter.ToString(hashkey).Replace("-", " "));

            var shashkey = new sbyte[16];
            Buffer.BlockCopy(hashkey, 0, shashkey, 0, 16);
            _ = CurrentBlowfish;
            CurrentBlowfish = new Blowfish();
            CurrentBlowfish.Init(shashkey, 16);

            // Keep PacketSender encryption in sync
            _packetSender?.UpdateBlowfish(CurrentBlowfish);

            _packetSender = new PacketSender(PacketQueue, _gameServerConnection, CurrentBlowfish, _zlib);

            // Let this send its own packet as it does not follow the normal packet rules
            #region ZoneInpackets
            var data = new byte[136];
            var input = BitConverter.GetBytes(ClientPacketId); //Packet count
            Buffer.BlockCopy(input, 0, data, 0, input.Length);
            input = BitConverter.GetBytes((ushort)0x0A); //Packet type
            Buffer.BlockCopy(input, 0, data, PacketHead, input.Length);
            input = [0x2E]; //Size
            Buffer.BlockCopy(input, 0, data, PacketHead + 0x01, input.Length);
            input = BitConverter.GetBytes(ClientPacketId); //Packet count
            Buffer.BlockCopy(input, 0, data, PacketHead + 0x02, input.Length);
            input = BitConverter.GetBytes(PlayerData.ID);
            Buffer.BlockCopy(input, 0, data, PacketHead + 0x0C, input.Length);


            byte checksum = 0;

            const int checksumOffset = PacketHead + 0x03;
            const int checksumLength = 84;

            for (var i = 0; i < checksumLength; i++)
            {
                checksum += data[checksumOffset + i];
            }
            //ShowInfo("Checksum: " + checksum);

            data[PacketHead + 0x04] = checksum;

            var tomd5 = new byte[data.Length - (PacketHead + 16)];
            Buffer.BlockCopy(data, PacketHead, tomd5, 0, tomd5.Length);
            tomd5 = _hasher.ComputeHash(tomd5);
            Buffer.BlockCopy(tomd5, 0, data, data.Length - 16, 16);

            ShowInfo("[Logintozone]Outgoing packet 0x0A, Zone in");
            try
            {
                Thread.Sleep(2000);
                _gameServerConnection.Send(data, data.Length);
                Thread.Sleep(1000);
                if (firstLogin)
                {
                    ClientPacketId++;
                    input = BitConverter.GetBytes(ClientPacketId); //Packet count
                    Buffer.BlockCopy(input, 0, data, 0, input.Length);
                    _gameServerConnection.Send(data, data.Length);
                }
            }
            catch (SocketException)
            {
                ShowWarn("[Logintozone]Failed to connect retrying");
                _startingKey[4] -= 2;
                Logintozone(firstLogin);
            }
            _packetSender.UpdateClientPacketId(ClientPacketId);

            #endregion
        }

        #region Combat Methods
        public void Attack(ushort targetIndex)
        {
            if (EntityList[targetIndex] != null)
            {
                var attackPacket = new P01ABuilder(EntityList[targetIndex].ID, targetIndex, 0x02, new uint[4]);
                PacketQueue.Enqueue(attackPacket.Build());
            }
        }

        public void Disengage(ushort targetIndex)
        {
            if (EntityList[targetIndex] != null)
            {
                var attackPacket = new P01ABuilder(EntityList[targetIndex].ID, targetIndex, 0x04, new uint[4]);
                PacketQueue.Enqueue(attackPacket.Build());
            }
        }

        public void Assist(ushort targetIndex)
        {
            if (EntityList[targetIndex] != null)
            {
                var attackPacket = new P01ABuilder(EntityList[targetIndex].ID, targetIndex, 0x0C, new uint[4]);
                PacketQueue.Enqueue(attackPacket.Build());
            }
        }

        public void RangedAttack(ushort targetIndex)
        {
            if (EntityList[targetIndex] != null)
            {
                var attackPacket = new P01ABuilder(EntityList[targetIndex].ID, targetIndex, 0x10, new uint[4]);
                PacketQueue.Enqueue(attackPacket.Build());
            }
        }

        public void WeaponSkill(ushort targetIndex, uint skillId)
        {
            if (EntityList[targetIndex] != null)
            {
                var parms = new uint[4];
                parms[0] = skillId;
                var packet = new P01ABuilder(EntityList[targetIndex].ID, targetIndex, 0x07, parms);
                PacketQueue.Enqueue(packet.Build());
            }
        }

        public void JobAbility(ushort targetIndex, uint skillId)
        {
            if (EntityList[targetIndex] != null)
            {
                var parms = new uint[4];
                parms[0] = skillId;
                var packet = new P01ABuilder(EntityList[targetIndex].ID, targetIndex, 0x09, parms);
                PacketQueue.Enqueue(packet.Build());
            }
        }

        public void CastMagic(ushort targetIndex, uint spellId)
        {
            if (EntityList[targetIndex] != null)
            {
                var parms = new uint[4];
                parms[0] = spellId;
                var castPacket = new P01ABuilder(EntityList[targetIndex].ID, targetIndex, 0x03, parms);
                PacketQueue.Enqueue(castPacket.Build());
            }
        }

        public bool CanUseSpell(uint spellId)
        {
            // Check if spell is learned
            var byteIndex = (int)(spellId / 8);
            var bitIndex = (int)(spellId % 8);
            if (byteIndex < 0 || byteIndex >= PlayerData.SpellList.Length)
                return false;

            var learned = (PlayerData.SpellList[byteIndex] & (1 << bitIndex)) != 0;

            if (!learned)
                return false;

            var spell = Spellrepo.GetById(spellId);

            var mjob = PlayerData.Job;
            var mjobLevel = PlayerData.Level;
            var sjob = PlayerData.SubJob;
            var sjobLevel = PlayerData.SubLevel;

            return spell != null && ((spell.Levels.ContainsKey(mjob) && spell.Levels[mjob] < mjobLevel) || (spell.Levels.ContainsKey(sjob) && spell.Levels[sjob] < sjobLevel));
        }

        public void Heal(HealMode mode)
        {
            var packet = new P0E8Builder(mode);
            PacketQueue.Enqueue(packet.Build());
        }
        #endregion
        #region Movement
        public void MoveTo(position_t pos)
        {
            try
            {
                Nav.FindPathToPosi(PlayerData.pos, pos, false);
                Nav.GetWaypoints();
            }
            catch (Exception ex)
            {
                ShowError(ex.ToString());
                return;
            }
            var queue = new Queue<position_t>(Nav.Waypoints);
            const float maxStepDistance = 1.0f; // Adjust this value based on what clients expect
            const float arrivalThreshold = 1.0f; // How close is "close enough"
            Task.Run(async () =>
            {
                while (queue.Count > 0)
                {
                    ShowInfo(queue.Count.ToString());
                    var targetPoint = queue.Dequeue();

                    // Keep moving towards the target point until we reach it
                    while (true)
                    {
                        // Calculate 3D distance
                        var deltaX = targetPoint.X - PlayerData.pos.X;
                        var deltaY = targetPoint.Y - PlayerData.pos.Y;
                        var deltaZ = targetPoint.Z - PlayerData.pos.Z;
                        var distance = (float)Math.Sqrt(deltaX * deltaX + deltaY * deltaY + deltaZ * deltaZ);

                        if (distance <= arrivalThreshold) // Close enough, we've arrived
                        {
                            break;
                        }

                        //Console.WriteLine("{0:G} {1:G} {2:G} {3:G}", targetPoint.X, targetPoint.Y, targetPoint.Z, distance);

                        // Update rotation to face the target
                        PlayerData.pos.Rotation = Nav.Getrotation(PlayerData.pos, targetPoint);

                        // Determine how far to move this step
                        var stepDistance = Math.Min(distance, maxStepDistance);

                        // Calculate movement ratio
                        var ratio = stepDistance / distance;

                        // Move towards the target
                        PlayerData.pos.X += deltaX * ratio;
                        PlayerData.pos.Y += deltaY * ratio;
                        PlayerData.pos.Z += deltaZ * ratio;

                        Thread.Sleep(205);
                    }

                    Thread.Sleep(50);
                }
            });
        }
        public void Mount(uint mountId)
        {
            var parms = new uint[4];
            parms[0] = mountId;
            var castPacket = new P01ABuilder(PlayerData.ID, PlayerData.Index, 0x1A, parms);
            PacketQueue.Enqueue(castPacket.Build());
        }

        public void Dismount()
        {
            var parms = new uint[4];
            var castPacket = new P01ABuilder(PlayerData.ID, PlayerData.Index, 0x12, parms);
            PacketQueue.Enqueue(castPacket.Build());
        }
        #endregion
        #region Social
        public void SendTell(string user, string message)
        {
            var tellPacket = new P0B6Builder(user, message);
            PacketQueue.Enqueue(tellPacket.Build());
        }

        public void SendPartyInvite(uint user)
        {
            var partyPacket = new P06EBuilder(user, 0, 0);
            PacketQueue.Enqueue(partyPacket.Build());
        }

        public void PartyInviteResponce(bool accept)
        {
            var partyPacket = new P074Builder(accept);
            PacketQueue.Enqueue(partyPacket.Build());
        }

       #endregion
        #region Events
        public class IncomingChatEventArgs(string name, string message, byte messageType, bool isgm, ushort zoneid) : EventArgs
        {
            public string Name { get; set; } = name;
            public string Message { get; set; } = message;
            public byte MessageType { get; set; } = messageType;
            public bool IsGm { get; set; } = isgm;
            public ushort ZoneId { get; set; } = zoneid;
        }

        public void OnIncomeChat(IncomingChatEventArgs e)
        {
            IncomingChat?.Invoke(this, e);
        }

        public class IncomingPartyInviteEventArgs(string name, uint charId, ushort charIndex) : EventArgs
        {
            public string Name { get; set; } = name;
            public uint CharId { get; set; } = charId;
            public ushort CharIndex { get; set; } = charIndex;
        }
        public void OnIncomePartyInvite(IncomingPartyInviteEventArgs e)
        {
            IncomingPartyInvite?.Invoke(this, e);
        }
        #endregion
        #region Lookup Functions
        public Entity GetEntityById(uint id)
        {
            return EntityList.FirstOrDefault(e => e != null && e.IsValid && e.ID == id);
        }
        public Entity GetEntityByName(string name)
        {
            return EntityList.FirstOrDefault(e =>
                e != null &&
                e.IsValid &&
                e.Name != null &&
                e.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        }
        public Entity GetEntityByIndex(ushort index)
        {
            if (EntityList[index] != null && EntityList[index].IsValid)
                return EntityList[index];
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

            var data = new byte[8];
            data[4] = 2;

            var zoneInPacket = new OutgoingPacket(data);
            zoneInPacket.SetType(0x11);
            zoneInPacket.SetSize(0x04);
            PacketQueue.Enqueue(zoneInPacket);

            ShowInfo("[OutGoing_O11]Outgoing packet 0x11, Zone in confirmation");

            // Clear zoning flag
            PlayerData.zoning = false;

            // Start position update task (reuses same cancellation token as game session)
            _positionTask = Task.Run(async () =>
            {
                while (!_posCts.Token.IsCancellationRequested)
                {
                    try
                    {
                        var posBuilder = new P015Builder(PlayerData);
                        PacketQueue.Enqueue(posBuilder.Build());
                    }
                    catch (Exception ex)
                    {
                        ShowError("[_positionTask] Outgoing task exception: " + ex);
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

        private void OutGoing_ZoneInData()
        {
            ShowInfo($"[OutGoing_ZoneInData]Sending Zone in data serverpacket:{ServerPacketId}");
            if (CharData)
            {

                var data = new byte[0x0C];
                var zoneInPacket = new OutgoingPacket(data);
                zoneInPacket.SetType(0x0C);
                zoneInPacket.SetSize(0x06);
                PacketQueue.Enqueue(zoneInPacket);

                data = new byte[0x08];
                var zoneInPacket2 = new OutgoingPacket(data);
                zoneInPacket2.SetType(0x61);
                zoneInPacket2.SetSize(0x04);
                PacketQueue.Enqueue(zoneInPacket2);

                data = new byte[0x1C];
                data[0x0A] = 0x14; //Action type
                var zoneInPacket3 = new OutgoingPacket(data);
                zoneInPacket3.SetType(0x01A);
                zoneInPacket3.SetSize(0x0E);
                PacketQueue.Enqueue(zoneInPacket3);

                data = new byte[0x18];
                var input = new byte[] { 0x02, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 }; //Language,Timestamp,Lengh,Start offset
                Buffer.BlockCopy(input, 0, data, 0x07, input.Length);
                var zoneInPacket4 = new OutgoingPacket(data);
                zoneInPacket4.SetType(0x4B);
                zoneInPacket4.SetSize(0x0C);
                PacketQueue.Enqueue(zoneInPacket4);

                data = new byte[0x24];
                var zoneInPacket5 = new OutgoingPacket(data);
                zoneInPacket5.SetType(0x0F);
                zoneInPacket5.SetSize(0x12);
                PacketQueue.Enqueue(zoneInPacket5);

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
                var zoneInPacket6 = new OutgoingPacket(data);
                zoneInPacket6.SetType(0x5A);
                zoneInPacket6.SetSize(0x02);
                PacketQueue.Enqueue(zoneInPacket6);

                ShowInfo("[OutGoing_ZoneInData]Outgoing packet multi,Sending Post zone data requests");
            }
        }
        #endregion

        public void ShowInfo(string outstring)
        {
            if (LogLevel > 3)
                Console.WriteLine($"[{_accountData.Username}]{outstring}");
        }
        public void ShowWarn(string outstring)
        {
            if (LogLevel > 2)
                Console.WriteLine($"[{_accountData.Username}]{outstring}");
        }
        public void ShowError(string outstring)
        {
            if (LogLevel > 1)
                Console.WriteLine($"[{_accountData.Username}]{outstring}");
        }
        public void LogMemoryUsage(string context)
        {
            var memoryUsed = GC.GetTotalMemory(false) / 1024 / 1024;
            ShowInfo($"[Memory][{context}] Current: {memoryUsed}MB, Gen0: {GC.CollectionCount(0)}, Gen1: {GC.CollectionCount(1)}, Gen2: {GC.CollectionCount(2)}");
        }
    }

    #region Structs
    public class MyPlayer
    {
        public uint ID;
        public ushort Index;
        public string Name;
        public byte Job;
        public byte SubJob;
        public byte Level;
        public byte SubLevel = 37;
        public byte zoneid;
        public ZoneInfo zone;
        public position_t pos;
        public position_t oldpos;
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
    public struct ZoneInfo
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
        public ushort moving;
        public byte Rot;

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
        public ushort TargetIndex;
        public string Name;
        public byte Hpp;
        public byte Animation;
        public byte Status;
        public position_t Pos;
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
