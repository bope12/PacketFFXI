//GP_SERV_COMMAND_ENTERZONE
//https://github.com/atom0s/XiPackets/tree/main/world/server/0x0008
//https://github.com/LandSandBoat/server/blob/base/src/map/packets/s2c/0x008_enterzone.cpp
using System;
using HeadlessFFXI;

public class P008Handler : IPacketHandler
{
    public ushort PacketId => 0x08;

    public void Handle(Client client, ReadOnlySpan<byte> data)
    {
        //uint8_t EnterZoneTbl[48]; // PS2: EnterZoneTbl
    }
}