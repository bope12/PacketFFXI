//GP_SERV_COMMAND_BATTLE2
//https://github.com/atom0s/XiPackets/tree/main/world/server/0x0028
//
using System;

namespace HeadlessFFXI.Packets.Incoming;

public class P028Handler : IPacketHandler
{
    public ushort PacketId => 0x28;

    public void Handle(Client client, ReadOnlySpan<byte> data)
    {
        // TODO: Implement handler logic here
        var dataReader = new PacketReader(data);
        dataReader.Skip(4); // Skip header
        /*uint entityId = dataReader.ReadUInt32();
        uint commandNum = dataReader.ReadUInt32();

        //    0 - None
        //    1 - Basic Attack
        //    2 - Range Attack (Finish)
        //    3 - Skill (Finish) (Weapon Skills)
        //    4 - Magic (Finish) (This is also sent if a weapon skill fails due to being too far from the target.)
        //    5 - Item (Finish)
        //    6 - Ability (Finish) (Dancer Flourish)
        //    7 - Skill (Start) (Monster Skills, Weapon Skills)
        //    8 - Magic (Start)
        //    9 - Item (Start) (Also sent if the item use is interrupted.)
        //    10 - Ability (Start)
        //    11 - Monster Skill (Finish), Trust Attacks (ie. Shantotto Melee Attack)
        //    12 - Range Attack (Start)
        //    13 - Unknown
        //    14 - Dancer Ability (Flourish, Jig, Samba, Step, Waltz, etc.)
        //    15 - Rune Fencer Effusion/Ward
        //
        //Note: DNC job abilities are a mix between cmd_no 6 and cmd_no 14, depending on the type, such as Flourishes.

        uint commandArg = dataReader.ReadUInt32();
        uint info = dataReader.ReadUInt32();
        uint res_sum = dataReader.ReadUInt32();
        uint trg_sum= dataReader.ReadUInt32();
    */
    }
}