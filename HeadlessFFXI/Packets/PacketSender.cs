using System;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Threading;
using HeadlessFFXI;

public class PacketSender : IDisposable
{
    private readonly OutgoingQueue _queue;
    private readonly UdpClient _server; // whatever wrapper you use for socket/Send()
    private readonly Timer _timer;
    private readonly object _lock = new();

    private ushort _clientPacketId = 2;
    private ushort _serverPacketId; // last received from server

    private const int PacketHead = 28; // whatever fixed header you start with
    static MD5 hasher = System.Security.Cryptography.MD5.Create();
    private readonly zlib _zlib;
    private readonly Blowfish _blowfish;
    private bool _disposed = false;

    public PacketSender(OutgoingQueue queue, UdpClient server, Blowfish blowfish, zlib myzlib)
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

    private void SendTick(object? state)
    {
        if (_disposed) return;

        List<OutgoingPacket> toSend = _queue.DrainAll();
        if (toSend.Count == 0)
            return;

        lock (_lock)
        {
            _clientPacketId++;
            using var ms = new MemoryStream();

            // Header: client & server packet IDs
            ms.Write(BitConverter.GetBytes(_clientPacketId), 0, 2);
            ms.Write(BitConverter.GetBytes(_serverPacketId), 0, 2);
            byte[] padding = new byte[24];
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

            byte[] data = ms.ToArray();

            // Apply your existing transforms
            packet_Compress(ref data);
            packet_addmd5(ref data);
            packet_Encode(ref data);

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
    public void packet_Compress(ref byte[] data)
    {
        byte[] buffer = new byte[1800];
        byte[] input = new byte[data.Length - PacketHead];
        System.Buffer.BlockCopy(data, PacketHead, input, 0, data.Length - PacketHead);
        //Lets Compress this packet
        var finalsize = _zlib.ZlibCompress(input, ref buffer);
        Array.Resize(ref buffer, finalsize);
        input = new byte[PacketHead + finalsize + 16];
        Array.Copy(buffer, 0, input, PacketHead, finalsize);
        Array.Copy(data, 0, input, 0, PacketHead);
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
            _blowfish.Blowfish_encipher(ref xl, ref xr);

            // Write them back into the buffer
            Array.Copy(BitConverter.GetBytes(xl), 0, data, offset1, 4);
            Array.Copy(BitConverter.GetBytes(xr), 0, data, offset2, 4);
        }
    }
    public void packet_addmd5(ref byte[] data)
    {
        byte[] tomd5 = new byte[data.Length - (PacketHead + 16)];
        System.Buffer.BlockCopy(data, PacketHead, tomd5, 0, tomd5.Length);
        tomd5 = hasher.ComputeHash(tomd5);
        System.Buffer.BlockCopy(tomd5, 0, data, data.Length - 16, 16);
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
public class OutgoingPacket
{
    public byte[] Data { get; }

    public OutgoingPacket(byte[] payload)
    {
        Data = payload ?? throw new ArgumentNullException(nameof(payload));
    }
    public void SetType(ushort typeIn)
    {
        ushort type = (ushort)(typeIn & 0x1FF);
        Data[0] = (byte)(type & 0xFF);   // lower 8 bits of type
    }
    public void SetSize(ushort sizeIn)
    {
        byte size = (byte)(sizeIn & 0xFE);
        Data[1] = (byte)(size & 0xFE); // second byte replaced with size
    }
    public void SetPacketId(ushort idIn)
    {
        BinaryPrimitives.WriteUInt16LittleEndian(Data.AsSpan(0x02), idIn);
    }
}