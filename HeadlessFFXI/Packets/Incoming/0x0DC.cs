//GP_SERV_COMMAND_GROUP_SOLICIT_REQ
//https://github.com/atom0s/XiPackets/tree/main/world/server/0x00DC
//https://github.com/LandSandBoat/server/blob/base/src/map/packets/s2c/0x0dc_group_solicit_req.cpp
using System;
using HeadlessFFXI;
using static HeadlessFFXI.Client;

public class P0DCHandler : IPacketHandler
{
    public ushort PacketId => 0xDC;

    public void Handle(Client client, ReadOnlySpan<byte> data)
    {
        var dataReader = new PacketReader(data);
        dataReader.Skip(0x04); // skip header
        uint partyLeaderId = dataReader.ReadUInt32();
        ushort partyLeaderIndex = dataReader.ReadUInt16();
        byte anonFlag = dataReader.ReadByte();
        byte kind = dataReader.ReadByte();
        string partyLeaderName = dataReader.ReadString(16);
        ushort partyLeaderRace = dataReader.ReadUInt16();
        client.OnIncomePartyInvite(new IncomingPartyInviteEventArgs(partyLeaderName, partyLeaderId, partyLeaderIndex));
    }
}