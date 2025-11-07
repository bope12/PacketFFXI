//GP_SERV_COMMAND_GROUP_SOLICIT_REQ
//https://github.com/atom0s/XiPackets/tree/main/world/server/0x00DC
//https://github.com/LandSandBoat/server/blob/base/src/map/packets/s2c/0x0dc_group_solicit_req.cpp
using System;
using static HeadlessFFXI.Client;

namespace HeadlessFFXI.Packets.Incoming;

public class P0DCHandler : IPacketHandler
{
    public ushort PacketId => 0xDC;

    public void Handle(Client client, ReadOnlySpan<byte> data)
    {
        var dataReader = new PacketReader(data);
        dataReader.Skip(0x04); // skip header
        var partyLeaderId = dataReader.ReadUInt32();
        var partyLeaderIndex = dataReader.ReadUInt16();
        var anonFlag = dataReader.ReadByte();
        var kind = dataReader.ReadByte();
        var partyLeaderName = dataReader.ReadString(16);
        var partyLeaderRace = dataReader.ReadUInt16();
        client.OnIncomePartyInvite(new IncomingPartyInviteEventArgs(partyLeaderName, partyLeaderId, partyLeaderIndex));
    }
}