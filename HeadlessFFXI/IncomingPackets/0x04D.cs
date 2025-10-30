//GP_SERV_COMMAND_FRAGMENTS
//https://github.com/atom0s/XiPackets/tree/main/world/server/0x004D
//https://github.com/LandSandBoat/server/blob/base/src/map/packets/s2c/0x04d_fragments_servmes.cpp
using System;
using System.Buffers.Binary;
using HeadlessFFXI;

public class P04DHandler : IPacketHandler
{
    public ushort PacketId => 0x4D;

    public void Handle(Client client, ReadOnlySpan<byte> data)
    {
        // This handles more then just server messages, but for now we only care about those.
        byte handlerType = data[0x04];
        if (handlerType == 0x01 || handlerType == 0x02) // 0x01 = Server Message
        {
            uint length = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(0x14, 4));
            if (!client.silient)
                Console.WriteLine("[SMES]" + System.Text.Encoding.UTF8.GetString(data.Slice(0x18, (int)length)).TrimEnd('\0'));
        }
    }
}