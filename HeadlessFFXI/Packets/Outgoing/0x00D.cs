namespace HeadlessFFXI.Networking.Packets
{
    public class P00DBuilder : IPacketBuilder
    {
        public ushort Type => 0x0D;
        public ushort Size => 0x08;


        public P00DBuilder()
        {
        }

        public OutgoingPacket Build()
        {
            byte[] data = new byte[8];

            // Wrap it in your packet object
            var packet = new OutgoingPacket(data);
            packet.SetType(Type);
            packet.SetSize(Size);

            return packet;
        }
    }
}
