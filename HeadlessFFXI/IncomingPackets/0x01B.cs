//GP_SERV_COMMAND_JOB_INFO
//https://github.com/atom0s/XiPackets/tree/main/world/server/0x001B
//https://github.com/LandSandBoat/server/blob/base/src/map/packets/s2c/0x01b_job_info.cpp
using System;
using HeadlessFFXI;

public class P01BHandler : IPacketHandler
{
    public ushort PacketId => 0x1B;

    public void Handle(Client client, ReadOnlySpan<byte> data)
    {
        //memcpy(data + (0x0C), &PChar->jobs, 22)
        //memcpy(data + (0x20), &PChar->stats, 14);
        //memcpy(data + (0x44), &PChar->jobs, 27)
        //ref<uint32>(0x3C) = PChar->health.hp;
        //ref<uint32>(0x40) = PChar->health.mp
        //ref<uint32>(0x44) = PChar->jobs.unlocked & 1;    // первый бит в unlocked отвечает за дополнительную профессию
    }
}