using System;

namespace HeadlessFFXI.Networking.Packets
{
    public class P015Builder : IPacketBuilder
    {
        public ushort Type => 0x015;
        public ushort Size => 0x10;
        private My_Player _Player;

        public P015Builder(My_Player player)
        {
            _Player = player;
        }

        public OutgoingPacket Build()
        {
            byte[] data = new byte[0x20];

            // Position
            BitConverter.GetBytes(_Player.pos.X).CopyTo(data, 0x04);
            BitConverter.GetBytes(_Player.pos.Y).CopyTo(data, 0x08);
            BitConverter.GetBytes(_Player.pos.Z).CopyTo(data, 0x0C);

            // Movement tracking
            if (_Player.pos.HasChanged(_Player.oldpos))
            {
                _Player.pos.moving = (ushort)(_Player.pos.moving + 7);
                _Player.oldpos = _Player.pos;
            }
            else
            {
                _Player.pos.moving = 0;
            }

            BitConverter.GetBytes(_Player.pos.moving).CopyTo(data, 0x12);
            data[0x14] = _Player.pos.Rot;

            var packet = new OutgoingPacket(data);
            packet.SetType(Type);
            packet.SetSize(Size);

            return packet;
        }
    }
}
