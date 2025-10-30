//GP_SERV_CHAR_NPC
//https://github.com/atom0s/XiPackets/tree/main/world/server/0x000E
//https://github.com/LandSandBoat/server/blob/base/src/map/packets/entity_update.cpp
using System;
using System.Linq;
using System.Text;
using HeadlessFFXI;
using static HeadlessFFXI.Client;

public class P00EHandler : IPacketHandler
{
    public ushort PacketId => 0xE;

    public void Handle(Client client, ReadOnlySpan<byte> data)
    {
        var dataReader = new PacketReader(data);
        dataReader.Skip(4); // Skip header
        uint entityId = dataReader.ReadUInt32();
        ushort entityIndex = dataReader.ReadUInt16();
        SendFlags updateFlags = dataReader.ReadEnum<SendFlags>();

        byte dir = dataReader.ReadByte();
        float x = dataReader.ReadFloat();
        float z = dataReader.ReadFloat();
        float y = dataReader.ReadFloat();
        Flags0 flags0 = dataReader.ReadStruct<Flags0>();
        byte speed = dataReader.ReadByte();
        byte speedBase = dataReader.ReadByte();
        byte hpp = dataReader.ReadByte();
        byte serverStatus = dataReader.ReadByte();
        StatusFlags flags1 = dataReader.ReadStruct<StatusFlags>();
        //flags2_t    Flags2 flags2 = dataReader.ReadStruct<Flags2>();
        dataReader.Skip(4);
        //flags3_t    Flags3 flags3 = dataReader.ReadStruct<Flags3>();
        dataReader.Skip(4);
        uint btTargetID = dataReader.ReadUInt32();

        ushort raw = dataReader.ReadUInt16();
        int subKind = raw & 0x7;
        int status = (raw >> 3) & 0x1FFF;

        if (updateFlags.HasFlag(SendFlags.Despawn))
        {
            Console.WriteLine("Entity Despawn for ID:{0:G} Index:{1:G} Name:{2:G}", entityId, entityIndex, client.Entity_List[entityIndex].Name);
            client.Entity_List[entityIndex] = new Entity();
        }
        else
        {
            EntityType entityType = flags1.MonsterFlag ? EntityType.Mob : EntityType.NPC;
            client.Entity_List[entityIndex].Type = (byte)entityType;

            if (updateFlags.HasFlag(SendFlags.Position))
            {
                client.Entity_List[entityIndex].IsValid = true;
                client.Entity_List[entityIndex].Pos.Rot = dir;
                client.Entity_List[entityIndex].Pos.X = x;
                client.Entity_List[entityIndex].Pos.Y = y;
                client.Entity_List[entityIndex].Pos.Z = z;
                var targetindex = flags0.FaceTarget;
                client.Entity_List[entityIndex].TargetIndex = targetindex;
            }

            if (updateFlags.HasFlag(SendFlags.General))
            {
                client.Entity_List[entityIndex].IsValid = true;
                client.Entity_List[entityIndex].Hpp = hpp;
                client.Entity_List[entityIndex].Animation = serverStatus;
            }

            if (updateFlags.HasFlag(SendFlags.Name))
            {
                client.Entity_List[entityIndex].IsValid = true;
                var name = Encoding.UTF8.GetString(data.Slice(0x34, 15)).TrimEnd('\0');
                client.Entity_List[entityIndex].Name = name;
            }
        }
    }
}