//GP_SERV_COMMAND_MISSION
//https://github.com/atom0s/XiPackets/tree/main/world/server/0x0056
//https://github.com/LandSandBoat/server/blob/base/src/map/packets/s2c/0x056_mission.cpp
using System;
using HeadlessFFXI;

public class P056Handler : IPacketHandler
{
    public ushort PacketId => 0x56;

    public void Handle(Client client, ReadOnlySpan<byte> data)
    {
        //quest and mission log data
    }
}