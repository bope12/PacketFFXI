namespace HeadlessFFXI.Packets.Outgoing
{
    public class P074Builder(bool accept) : IPacketBuilder
    {
        public ushort Type => 0x74;
        public ushort Size => 0x02;
        private readonly bool _accept = accept;

        public OutgoingPacket Build()
        {
            var data = new byte[6];
            data[0x04] = (byte)(_accept ? 0x01 : 0x00);

            // Wrap it in your packet object
            var packet = new OutgoingPacket(data);
            packet.SetType(Type);
            packet.SetSize(Size);

            return packet;
        }
    }
}
