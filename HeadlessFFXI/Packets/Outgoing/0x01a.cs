using System;

//https://github.com/atom0s/XiPackets/tree/main/world/client/0x001A
namespace HeadlessFFXI.Packets.Outgoing
{
    public class P01ABuilder : IPacketBuilder
    {
        public ushort Type => 0x1A;
        public ushort Size => 0x0E;
        private readonly uint _targetId;
        private readonly ushort _targetIndex;
        private readonly ushort _actionId;
        public uint[] ActionBuf = new uint[4];



        public P01ABuilder(uint targetId, ushort targetIndex, ushort actionId, uint[] param)
        {
            _targetId = targetId;
            _targetIndex = targetIndex;
            _actionId = actionId;
            param.CopyTo(ActionBuf);
        }

        public OutgoingPacket Build()
        {
            var data = new byte[0x1C];

            BitConverter.GetBytes(_targetId).CopyTo(data, 0x04);
            BitConverter.GetBytes(_targetIndex).CopyTo(data, 0x08);
            BitConverter.GetBytes(_actionId).CopyTo(data, 0x0A);
            Buffer.BlockCopy(ActionBuf, 0, data, 0x0C, ActionBuf.Length * 4);

            // Wrap it in your packet object
            var packet = new OutgoingPacket(data);
            packet.SetType(Type);
            packet.SetSize(Size);

            return packet;
        }
    }
}
