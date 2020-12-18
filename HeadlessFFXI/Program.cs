using System;
using System.Text.Encodings;
using System.Net.Sockets;

namespace HeadlessFFXI
{
    class Program
    {
        static TcpClient lobbyview;
        static NetworkStream viewstream;
        static TcpClient lobbydata;
        static NetworkStream datastream;
        static uint actid;
        static uint charid;
        static void Main(string[] args)
        {
            Console.WriteLine("Attempting to send login request");
            try
            {
                TcpClient client = new TcpClient("127.0.0.1", 54231);
                NetworkStream stream = client.GetStream();
                Byte[] data = new Byte[33];
                string username = "lgck";
                System.Buffer.BlockCopy(System.Text.Encoding.ASCII.GetBytes(username), 0, data, 0, username.Length);
                string password = "qsd123";
                System.Buffer.BlockCopy(System.Text.Encoding.ASCII.GetBytes(password), 0, data, 16, password.Length);
                data[32] = 0x10;
                stream.Write(data, 0, 33);
                data = new Byte[16];
                stream.Read(data, 0, 16);
                switch (data[0])
                {
                    case 0x0001:
                        Console.WriteLine("Info Received and accepted");
                        actid = BitConverter.ToUInt32(data, 1);
                        Console.WriteLine("Account id:" + actid);
                        LobbyData();
                        LobbyView0x26();
                        break;
                    default:
                        Console.WriteLine("Unsure Code:" + data[0]);
                        break;
                }
                stream.Close();
                client.Close();
            }
            catch (SocketException d)
            {
                Console.WriteLine("Error received:" + d.Message);
            }
        }
        static void LobbyView0x26()
        {
            lobbyview = new TcpClient("127.0.0.1", 54001);
            viewstream = lobbyview.GetStream();
            Byte[] ver = System.Text.Encoding.ASCII.GetBytes("30201004_0");
            Byte[] data = new byte[152];
            data[8] = 0x26;
            System.Buffer.BlockCopy(ver, 0, data, 116, 10);
            viewstream.Write(data, 0, 152);
            data = new Byte[40];
            viewstream.Read(data, 0, 40);
            Console.WriteLine("Expantion Bitmask:" + BitConverter.ToUInt16(data, 32));
            Console.WriteLine("Feature Bitmask:" + BitConverter.ToUInt16(data, 36));
            LobbyView0x1F();
        }
        static void LobbyView0x1F()
        {
            Byte[] data = new byte[44];
            data[8] = 0x1F;
            viewstream.Write(data, 0, 44);
            LobbyData0xA1();
        }
        static void LobbyView0x24()
        {
            Byte[] data = new byte[44];
            data[8] = 0x24;
            viewstream.Write(data, 0, 44);
            data = new byte[64];
            viewstream.Read(data, 0, 64);
            Console.WriteLine("Server Name:" + System.Text.Encoding.UTF8.GetString(data, 36, 16));
            LobbyView0x07();
        }
        static void LobbyView0x07()
        {
            Byte[] data = new byte[88];
            data[8] = 0x07;
            Byte[] id = BitConverter.GetBytes(charid);
            System.Buffer.BlockCopy(id, 0, data, 28, id.Length);
            viewstream.Write(data, 0, 88);
            LobbyData0xA2();
        }
        static void LobbyData0xA2()
        {
            byte[] data = {0xA2,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x58,0xE0,0x5D,0xAD,0x00,0x00,0x00,0x00};
            datastream.Write(data,0,25);
        }
            static void LobbyData()
        {
            lobbydata = new TcpClient("127.0.0.1", 54230);
            datastream = lobbydata.GetStream();
            Byte[] dat = new Byte[1];
            dat[0] = 0xA1;
            Byte[] id = BitConverter.GetBytes(actid);
            Byte[] data = new Byte[dat.Length + id.Length];
            System.Buffer.BlockCopy(dat, 0, data,0,dat.Length);
            System.Buffer.BlockCopy(id, 0, data, dat.Length, id.Length);
            datastream.Write(data, 0, data.Length);
        }
        static void LobbyData0xA1()
        {
            Byte[] data = new Byte[13];
            Byte[] ip = BitConverter.GetBytes(((uint)16777343));
            System.Buffer.BlockCopy(ip, 0, data, 0, ip.Length);
            //data[0] = 0xA1;
            datastream.Flush();
            datastream.Write(data);
            data = new byte[328];
            datastream.Read(data, 0, 328);           
            /*for(int i = 0;i<328; i++)
            {
                if(data[i] != 0)
                Console.WriteLine("i:"+i+" "+data[i]);
            }*/
            data = new byte[2272];
            viewstream.Read(data,0,2272);
            /*for (int i = 0; i < 2272; i++)
            {
                if (data[i] != 0)
                    Console.WriteLine("i:" + i + " " + data[i]);
            }*/
            Console.WriteLine("Charid:" + BitConverter.ToUInt32(data,36));
            charid = BitConverter.ToUInt32(data, 36);
            Console.WriteLine("Name:" +System.Text.Encoding.UTF8.GetString(data,44,5));
            LobbyView0x24();
        }
    }
}
