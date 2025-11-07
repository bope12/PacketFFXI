//
//https://github.com/atom0s/XiPackets/tree/main/world/server/0x0009
//
using System;

namespace HeadlessFFXI.Packets.Incoming;

public class P009Handler : IPacketHandler
{
    public ushort PacketId => 0x9;

    public void Handle(Client client, ReadOnlySpan<byte> data)
    {
        Console.WriteLine("[P009Handler] Handler not yet implemented. Size: {data.Length}");
        // TODO: Implement handler logic here
    }
}