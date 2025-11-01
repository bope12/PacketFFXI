namespace HeadlessFFXI.Networking.Packets
{
    public interface IPacketBuilder
    {
        OutgoingPacket Build();
    }
}
