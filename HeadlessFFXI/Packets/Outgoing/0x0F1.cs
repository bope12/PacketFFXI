namespace HeadlessFFXI.Networking.Packets
{
    public class P0F1Builder : IPacketBuilder
    {
        public ushort Type => 0xF1;
        public ushort Size => 0x04;
        public ushort BuffId;

        public P0F1Builder(ushort buffId)
        {
            BuffId = buffId;
        }

        public OutgoingPacket Build()
        {
            byte[] data = new byte[8];
            BitConverter.GetBytes(BuffId).CopyTo(data, 0x04);
            // Wrap it in your packet object
            var packet = new OutgoingPacket(data);
            packet.SetType(Type);
            packet.SetSize(Size);

            return packet;
        }
    }
}
