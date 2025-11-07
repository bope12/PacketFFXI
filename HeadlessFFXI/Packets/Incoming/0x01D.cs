//GP_SERV_COMMAND_ITEM_SAME
//https://github.com/atom0s/XiPackets/tree/main/world/server/0x001D
//https://github.com/LandSandBoat/server/blob/base/src/map/packets/s2c/0x01d_item_same.cpp
using System;

namespace HeadlessFFXI.Packets.Incoming;

public class P01DHandler : IPacketHandler
{
    public ushort PacketId => 0x1D;

    public void Handle(Client client, ReadOnlySpan<byte> data)
    {
        var doneLoading = data[0x04]; // 0 loading , 1 doneloading
        var containerId = data[0x06]; // if done will be maxcontainerID
        //UInt32 flags = 0x0C
    }
}