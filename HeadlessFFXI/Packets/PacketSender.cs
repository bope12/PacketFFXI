using System;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Threading;

namespace HeadlessFFXI.Packets;

public class PacketSender : IDisposable
{
    private readonly OutgoingQueue _queue;
    private readonly UdpClient _server; // whatever wrapper you use for socket/Send()
    private readonly Timer _timer;
    private readonly Lock _lock = new();

    private ushort _clientPacketId = 1;
    private ushort _serverPacketId = 1; // last received from server

    private const int PacketHead = 28; // whatever fixed header you start with
    private readonly MD5 _hasher = MD5.Create();
    private readonly Zlib _zlib;
    private Blowfish _blowfish;
    private bool _disposed;

    public PacketSender(OutgoingQueue queue, UdpClient server, Blowfish blowfish, Zlib myzlib)
    {
        _queue = queue;
        _server = server;
        _blowfish = blowfish;
        _zlib = myzlib;
        _timer = new Timer(SendTick, null, 400, 400); // 400ms repeating
    }

    public void UpdateServerPacketId(ushort id)
    {
        lock (_lock)
            _serverPacketId = id;
    }
    public void UpdateClientPacketId(ushort id)
    {
        lock (_lock)
            _clientPacketId = id;
    }
    public void UpdateBlowfish(Blowfish blowfish)
    {
        _blowfish = blowfish;
    }

    private void SendTick(object? state)
    {
        if (_disposed) return;

        var toSend = _queue.DrainAll();
        if (toSend.Count == 0)
            return;

        lock (_lock)
        {
            _clientPacketId++;
            using var ms = new MemoryStream();

            // Header: client & server packet IDs
            ms.Write(BitConverter.GetBytes(_clientPacketId), 0, 2);
            ms.Write(BitConverter.GetBytes(_serverPacketId), 0, 2);
            var padding = new byte[24];
            ms.Write(padding, 0, padding.Length);

            // Append all queued small packets
            foreach (var packet in toSend)
            {
                packet.SetPacketId(_clientPacketId);
                ms.Write(packet.Data, 0, packet.Data.Length);
            }

            // Add 16 bytes of empty space for the MD5 footer
            padding = new byte[16];
            ms.Write(padding, 0, padding.Length);

            var data = ms.ToArray();

            // Apply your existing transforms
            Packet_Compress(ref data);
            Packet_AddMd5(ref data);
            Packet_Encode(ref data);

            //Console.WriteLine("Sending packet with ClientPacketId: {0}, ServerPacketId: {1}, Size: {2}", _clientPacketId, _serverPacketId, data.Length);

            _server.Send(data, data.Length);
        }
    }
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        if (disposing)
        {
            // Dispose managed resources
            _timer?.Dispose();
        }

        _disposed = true;
    }

    ~PacketSender()
    {
        Dispose(false);
    }

    #region Packet Helpers
    public void Packet_Compress(ref byte[] data)
    {
        var buffer = new byte[1800];
        var input = new byte[data.Length - PacketHead];
        Buffer.BlockCopy(data, PacketHead, input, 0, data.Length - PacketHead);

        var finalsize = _zlib.ZlibCompress(input, ref buffer);
        Array.Resize(ref buffer, finalsize);
        input = new byte[PacketHead + finalsize + 16];
        Array.Copy(buffer, 0, input, PacketHead, finalsize);
        Array.Copy(data, 0, input, 0, PacketHead);
        data = input;

        input = BitConverter.GetBytes(finalsize);
        Buffer.BlockCopy(input, 0, data, data.Length - 20, input.Length);
    }

    public void Packet_Encode(ref byte[] data)
    {
        //Lets encipher this packet
        var cypherSize = (data.Length / 4) & ~1; // same as & -2

        Span<byte> dataSpan = data;

        for (var j = 0; j < cypherSize; j += 2)
        {
            var offset1 = 4 * (j + 7);
            var offset2 = 4 * (j + 8);

            // If not enough bytes remain for a full 64-bit block, skip
            if (offset2 + 4 > data.Length)
                break;

            // Read two 32-bit words from buff
            var xl = BinaryPrimitives.ReadUInt32LittleEndian(dataSpan.Slice(offset1, 4));
            var xr = BinaryPrimitives.ReadUInt32LittleEndian(dataSpan.Slice(offset2, 4));

            // Encrypt the pair
            _blowfish.Blowfish_encipher(ref xl, ref xr);

            // Write them back into the buffer
            BinaryPrimitives.WriteUInt32LittleEndian(dataSpan.Slice(offset1, 4), xl);
            BinaryPrimitives.WriteUInt32LittleEndian(dataSpan.Slice(offset2, 4), xr);
        }
    }
    public void Packet_AddMd5(ref byte[] data)
    {
        var tomd5 = new byte[data.Length - (PacketHead + 16)];
        Buffer.BlockCopy(data, PacketHead, tomd5, 0, tomd5.Length);
        tomd5 = _hasher.ComputeHash(tomd5);
        Buffer.BlockCopy(tomd5, 0, data, data.Length - 16, 16);
    }
    #endregion
}

public class OutgoingQueue
{
    private readonly ConcurrentQueue<OutgoingPacket> _queue = new();

    public void Enqueue(OutgoingPacket packet) => _queue.Enqueue(packet);

    public List<OutgoingPacket> DrainAll()
    {
        var list = new List<OutgoingPacket>();
        while (_queue.TryDequeue(out var p))
            list.Add(p);
        return list;
    }
}
public class OutgoingPacket(byte[] payload)
{
    public byte[] Data { get; } = payload ?? throw new ArgumentNullException(nameof(payload));

    public void SetType(ushort typeIn)
    {
        var type = (ushort)(typeIn & 0x1FF);
        Data[0] = (byte)(type & 0xFF);   // lower 8 bits of type
    }
    public void SetSize(ushort sizeIn)
    {
        var size = (byte)(sizeIn & 0xFE);
        Data[1] = (byte)(size & 0xFE); // second byte replaced with size
    }
    public void SetPacketId(ushort idIn)
    {
        BinaryPrimitives.WriteUInt16LittleEndian(Data.AsSpan(0x02), idIn);
    }
}