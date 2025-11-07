//GP_SERV_COMMAND_ASSIST
//https://github.com/atom0s/XiPackets/tree/main/world/server/0x0058
//https://github.com/LandSandBoat/server/blob/base/src/map/packets/s2c/0x058_assist.cpp
using System;

namespace HeadlessFFXI.Packets.Incoming;

public class P058Handler : IPacketHandler
{
    public ushort PacketId => 0x58;

    public void Handle(Client client, ReadOnlySpan<byte> data)
    {
        client.ShowInfo(data.Length.ToString());
        var dataReader = new PacketReader(data);
        dataReader.Skip(4); // Skip header

        var charId = dataReader.ReadUInt32();
        var targetId = dataReader.ReadUInt32();
        var Index = dataReader.ReadUInt16();
        if(charId == client.PlayerData.ID)
        {
            client.PlayerData.TargetId = targetId;
        }
    }
}