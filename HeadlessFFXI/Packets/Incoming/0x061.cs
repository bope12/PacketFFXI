//GP_SERV_COMMAND_CLISTATUS
//https://github.com/atom0s/XiPackets/tree/main/world/server/0x0061
//https://github.com/LandSandBoat/server/blob/base/src/map/packets/s2c/0x061_clistatus.cpp
using System;

namespace HeadlessFFXI.Packets.Incoming;

public class P061Handler : IPacketHandler
{
    public ushort PacketId => 0x61;

    public void Handle(Client client, ReadOnlySpan<byte> data)
    {
        // Stat menu infomation
        var dataReader = new PacketReader(data);
        dataReader.Skip(4);
        int maxHp = dataReader.ReadInt32();
        int maxMp = dataReader.ReadInt32();
        byte mjob = dataReader.ReadByte();
        byte mjob_level = dataReader.ReadByte();
        byte sjob = dataReader.ReadByte();
        byte sjob_level = dataReader.ReadByte();
        short exp_now = dataReader.ReadInt16();
        short exp_nex = dataReader.ReadInt16();
        ushort str = dataReader.ReadUInt16();
        ushort dex = dataReader.ReadUInt16();
        ushort vit = dataReader.ReadUInt16();
        ushort agi = dataReader.ReadUInt16();
        ushort _int = dataReader.ReadUInt16();
        ushort mnd = dataReader.ReadUInt16();
        ushort chr = dataReader.ReadUInt16();
        short astr = dataReader.ReadInt16();
        short adex = dataReader.ReadInt16();
        short avit = dataReader.ReadInt16();
        short aagi = dataReader.ReadInt16();
        short aint = dataReader.ReadInt16();
        short amnd = dataReader.ReadInt16();
        short achr = dataReader.ReadInt16();
        int atk = dataReader.ReadInt16();
        int def = dataReader.ReadInt16();
        short fire_eve = dataReader.ReadInt16();
        short ice_eve = dataReader.ReadInt16();
        short wind_eve = dataReader.ReadInt16();
        short earth_eve = dataReader.ReadInt16();
        short thunder_eve = dataReader.ReadInt16();
        short water_eve = dataReader.ReadInt16();
        short light_eve = dataReader.ReadInt16();
        short dark_eve = dataReader.ReadInt16();
        ushort designation = dataReader.ReadUInt16();
        ushort rank = dataReader.ReadUInt16();
        ushort rankbar = dataReader.ReadUInt16();
        ushort BindZoneNo = dataReader.ReadUInt16();
        uint MonsterBuster = dataReader.ReadUInt32();
        byte nation = dataReader.ReadByte();
        byte myroom = dataReader.ReadByte();
        byte su_lv = dataReader.ReadByte();
        dataReader.Skip(1); // Padding4f
        byte highest_ilvl = dataReader.ReadByte();
        byte ilvl = dataReader.ReadByte();
        byte ilvl_mhand = dataReader.ReadByte();
        byte ilvl_ranged = dataReader.ReadByte();
        uint unity_info = dataReader.ReadUInt32();//unityinfo_t   unity_info;
        ushort unity_points1 = dataReader.ReadUInt16();
        ushort unity_points2 = dataReader.ReadUInt16();
        uint unity_chat_color_flag = dataReader.ReadUInt32();
        uint mastery_info = dataReader.ReadUInt32();//masteryinfo_t mastery_info;
        uint mastery_exp_now = dataReader.ReadUInt32();
        uint mastery_exp_next = dataReader.ReadUInt32();
        client.PlayerData.SubJob = sjob;
        client.PlayerData.SubLevel = sjob_level;
    }
}