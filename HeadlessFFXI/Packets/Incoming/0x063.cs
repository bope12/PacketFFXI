//GP_SERV_COMMAND_MISCDATA
////https://github.com/atom0s/XiPackets/tree/main/world/server/0x0063
//https://github.com/LandSandBoat/server/blob/base/src/map/packets/s2c/0x063_miscdata.h
using System;

namespace HeadlessFFXI.Packets.Incoming;

public class P063Handler : IPacketHandler
{
    public ushort PacketId => 0x63;

    public void Handle(Client client, ReadOnlySpan<byte> data)
    {
        var dataReader = new PacketReader(data);
        dataReader.Skip(0x04); // skip header
        var miscType = dataReader.ReadUInt16();
        // Merits       = 0x02, // Merit menu info not merits themselves
        // Monstrosity1 = 0x03, // Monstrosity Garbage
        // Monstrosity2 = 0x04, // Monstrosity Garbage
        // JobPoints    = 0x05, // Job point info for each Job
        // Homepoints   = 0x06, // Teleport access Masks
        // Unity        = 0x07, // Unity Garbage
        // StatusIcons  = 0x09, // Gives StatusEffect icons and expiration timestamps
        // Unknown      = 0x0A, // All zeros atm
    }
}