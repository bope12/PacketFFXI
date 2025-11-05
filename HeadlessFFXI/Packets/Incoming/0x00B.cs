//GP_SERV_COMMAND_LOGOUT
//https://github.com/atom0s/XiPackets/tree/main/world/server/0x000B
//https://github.com/LandSandBoat/server/blob/base/src/map/packets/s2c/0x00b_logout.cpp
using System;
using System.Net;
using HeadlessFFXI;

public class P00BHandler : IPacketHandler
{
    public ushort PacketId => 0x0B;

    public void Handle(Client client, ReadOnlySpan<byte> data)
    {
        var dataReader = new PacketReader(data);
        dataReader.Skip(4); // Skip header
        byte LogoutState = dataReader.ReadByte();
        dataReader.Skip(3); // Skip padding to align to next uint32
        uint ipRaw = dataReader.ReadUInt32();
        uint portRaw = dataReader.ReadUInt32();
        ushort port = (ushort)(portRaw & 0xFFFF); // Get lower 2 bytes
        //uint8_t  padding00[8];
        //GP_GAME_ECODE        cliErrCode;  // PS2: cliErrCode only ever GP_GAME_ECODE::NOERR; on lsb

        if (LogoutState == 2)
            client.HandleZoneChange(ipRaw, port);

    }
    // PS2: GP_GAME_LOGOUT_STATE
    //enum class GP_GAME_LOGOUT_STATE : uint8_t
    //{
    //    NONE           = 0,
    //    LOGOUT         = 1,
    //    ZONECHANGE     = 2,
    //    MYROOM         = 3,
    //    CANCELL        = 4,
    //    POLEXIT        = 5,
    //    JOBEXIT        = 6,
    //    POLEXIT_MYROOM = 7,
    //    TIMEOUT        = 8,
    //    GMLOGOUT       = 9,
    //    END            = 10,
    //};
}