//GP_SERV_COMMAND_INSPECT_MESSAGE
//https://github.com/atom0s/XiPackets/tree/main/world/server/0x00CA
//https://github.com/LandSandBoat/server/blob/base/src/map/packets/s2c/0x0ca_inspect_message.cpp
using System;

namespace HeadlessFFXI.Packets.Incoming;

public class P0CAHandler : IPacketHandler
{
    public ushort PacketId => 0xCA;

    public void Handle(Client client, ReadOnlySpan<byte> data)
    {
        // uint8_t  sInspectMessage[123]; // PS2: sInspectMessage
        // uint8_t  BazaarFlag : 1;       // PS2: BazaarFlag
        // uint8_t  MyFlag : 1;           // PS2: MyFlag
        // uint8_t  Race : 6;             // PS2: (New; was previously padding.)
        // uint8_t  sName[16];            // PS2: sName
        // uint32_t DesignationNo;        // PS2: DesignationNo
    }
}