//GP_SERV_COMMAND_ENTERZONE
//https://github.com/atom0s/XiPackets/tree/main/world/server/0x000A
//https://github.com/LandSandBoat/server/blob/base/src/map/packets/s2c/0x00a_login.cpp
using System;
using System.Buffers.Binary;
using System.Threading.Tasks;
using HeadlessFFXI;

public class P00AHandler : IPacketHandler
{
    public ushort PacketId => 0x0A;

    public void Handle(Client client, ReadOnlySpan<byte> data)
    {
        var ExpectedSize = 0x104;
        if (data.Length == ExpectedSize)
        {
            var dataReader = new PacketReader(data);
            dataReader.Skip(4); //skip header
            client.Player_Data.ID = dataReader.ReadUInt32();
            client.Player_Data.Index = dataReader.ReadUInt16();
            dataReader.Skip(1); //skip some unk bytes
            client.Player_Data.pos.Rot = dataReader.ReadByte();
            client.Player_Data.pos.X = dataReader.ReadFloat();
            client.Player_Data.pos.Y = dataReader.ReadFloat();
            client.Player_Data.pos.Z = dataReader.ReadFloat();
            client.Player_Data.pos.moving = 0;
            client.Player_Data.Job = data[0xB4];
            client.Player_Data.SubJob = data[0xB7];
            client.Player_Data.MaxHP = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(0xE8, 4));
            client.Player_Data.MaxMP = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(0xEC, 4));
            client.Player_Data.Str = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice( 0xCC, 2));
            client.Player_Data.Dex = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice( 0xCC + 2, 2));
            client.Player_Data.Vit = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice( 0xCC + 4, 2));
            client.Player_Data.Agi = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice( 0xCC + 6, 2));
            client.Player_Data.Int = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice( 0xCC + 8, 2));
            client.Player_Data.Mnd = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice( 0xCC + 10, 2));
            client.Player_Data.Chr = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice( 0xCC + 12, 2));
            //if (!client.silient)
            //    Console.WriteLine("Id:{0:G} Rot:{1:G} {2:G},{3:G},{4:G} {5:G}/{6:G} MaxHP:{7:G} MaxMP:{8:G} Str:{9:G} Chr:{10:G}", client.Player_Data.ID, client.Player_Data.pos.Rot, client.Player_Data.pos.X, client.Player_Data.pos.Y, client.Player_Data.pos.Z, client.Player_Data.Job, client.Player_Data.SubJob, client.Player_Data.MaxHP, client.Player_Data.MaxMP, client.Player_Data.Str, client.Player_Data.Chr);
            client.Player_Data.zone.ID = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(0x30, 2));
            client.Player_Data.zone.Weather = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice( 0x68, 2));
            client.Player_Data.zone.Weather_time = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice( 0x6A, 4));
            client.Player_Data.zone.Music_BG_Day = data[0x56];
            client.Player_Data.zone.Music_BG_Night = data[0x58];
            client.Player_Data.zone.Music_Battle_Solo = data[0x5A];
            client.Player_Data.zone.Music_Battle_Party = data[0x5C];
        }
        else
        {
            client.ShowWarn($"[P00AHandler]Incorect size in incoming 00A Packet Expected {ExpectedSize} Got {data.Length}");
        }

        Task.Run(() => client.OutGoing_O11()); // Send Zone in confirmation
    }
}