//GP_SERV_COMMAND_BLACK_LIST
//https://github.com/atom0s/XiPackets/tree/main/world/server/0x0041
//https://github.com/LandSandBoat/server/blob/base/src/map/packets/s2c/0x041_black_list.cpp
using System;

namespace HeadlessFFXI.Packets.Incoming;

public class P041Handler : IPacketHandler
{
    public ushort PacketId => 0x41;

    public void Handle(Client client, ReadOnlySpan<byte> data)
    {
        //SAVE_BLACK List[12];  // PS2: List
        //int8_t     Stat;      // PS2: Stat
        //int8_t     Num;       // PS2: Num
        //uint16_t   padding00; // PS2: (New; did not exist.)
    }
}