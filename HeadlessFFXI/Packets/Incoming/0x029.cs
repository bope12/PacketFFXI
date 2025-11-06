//GP_SERV_COMMAND_BATTLE_MESSAGE
//https://github.com/atom0s/XiPackets/tree/main/world/server/0x0029
//
using System;
using HeadlessFFXI;

public class P029Handler : IPacketHandler
{
    public ushort PacketId => 0x29;

    public void Handle(Client client, ReadOnlySpan<byte> data)
    {
        //Console.WriteLine("[P029Handler] Handler not yet implemented. Size: {data.Length}");
        // TODO: Implement handler logic here
    }
}