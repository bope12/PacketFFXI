//GP_SERV_COMMAND_MAPSCHEDULOR
//https://github.com/atom0s/XiPackets/tree/main/world/server/0x0039
//
using System;

namespace HeadlessFFXI.Packets.Incoming;

public class P039Handler : IPacketHandler
{
    public ushort PacketId => 0x39;

    public void Handle(Client client, ReadOnlySpan<byte> data)
    {
        //Console.WriteLine("[P039Handler] Handler not yet implemented. Size: {data.Length}");
        // TODO: Implement handler logic here
    }
}