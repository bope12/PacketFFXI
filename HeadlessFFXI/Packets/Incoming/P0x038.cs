//
//https://github.com/atom0s/XiPackets/tree/main/world/server/0x0038
//
using System;

namespace HeadlessFFXI.Packets.Incoming;

public class P038Handler : IPacketHandler
{
    public ushort PacketId => 0x38;

    public void Handle(Client client, ReadOnlySpan<byte> data)
    {
        Console.WriteLine("[P038Handler] Handler not yet implemented. Size: {data.Length}");
        // TODO: Implement handler logic here
    }
}