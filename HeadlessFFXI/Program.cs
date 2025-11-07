using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using HeadlessFFXI.Packets.Outgoing;
using static HeadlessFFXI.Client;

namespace HeadlessFFXI
{
    class Program
    {
        public static Client User;

        private static async Task Main(string[] args)
        {
            #region Settings
            Console.SetOut(new TimestampTextWriter(Console.Out));
            var settings = new Config
            {
                server = "127.0.0.1"
            };
            if (args.Length == 6 || args.Length == 8)
            {
                for (var i = 0; i <= args.Length / 2; i += 2)
                {
                    var s = args[i];
                    switch (s)
                    {
                        case "-user":
                            settings.user = args[i + 1];
                            break;
                        case "-pass":
                            settings.password = args[i + 1];
                            break;
                        case "-slot":
                            settings.char_slot = short.Parse(args[i + 1]) - 1;
                            break;
                        case "-server":
                            settings.server = args[i + 1];
                            break;
                    }
                    Console.WriteLine("[Cfg] {0:G}:{1:G}", args[i].TrimStart('-'), args[i + 1]);
                }
            }
            else if (File.Exists("config.cfg"))
            {
                var cfg = new StreamReader("config.cfg");
                while (await cfg.ReadLineAsync() is { } line)
                {
                    var setting = line.Split(":");
                    switch (setting[0])
                    {
                        case "username":
                            settings.user = setting[1];
                            break;
                        case "password":
                            settings.password = setting[1];
                            setting[1] = "********";
                            break;
                        case "char_slot":
                            settings.char_slot = Int16.Parse(setting[1]) - 1;
                            break;
                        case "server":
                            settings.server = setting[1];
                            break;
                    }
                    Console.WriteLine("[Cfg]{0:G}:{1:G}", setting[0], setting[1]);
                }
            }
            else
            {
                Console.WriteLine("[Cfg]No login information provided, Move config file into folder with exe or add launch args with -user user -pass pass -slot #");
                //Exit();
            }
            if (settings.user == null)
            {
                Console.WriteLine("[Cfg]No username set");
                Console.Write("Enter a username:");
                settings.user = Console.ReadLine();
            }
            if (settings.password == null)
            {
                Console.WriteLine("[Cfg]No password set");
                Console.Write("Enter a password:");
                settings.password = Console.ReadLine();
            }
            #endregion
            //User = new Client(settings, true, 4);
            //await User.Login();
            //User.IncomingChat += YourObject_IncomingChat;
            //User.IncomingPartyInvite += YourObject_IncomingPartyInvite;

            //Thread.Sleep(4000);
            //var config = new Config();
            //config.user = "hjhjhjhjhj";
            //config.password = "jhjhjhjh";
            //config.server = "127.0.0.1";
            //config.char_slot = 0;
            //var user2 = new Client(config, true, 4);
            //await user2.Login();
            //user2.IncomingChat += YourObject_IncomingChat;
            //user2.IncomingPartyInvite += YourObject_IncomingPartyInvite;
            var config1 = new Config
            {
                user = "party1",
                password = "pass",
                server = "127.0.0.1",
                char_slot = 0
            };

            var user1 = new Client(config1);
            config1.user = "party2";
            var user2 = new Client(config1);
            config1.user = "party3";
            var user3 = new Client(config1);
            config1.user = "party4";
            var user4 = new Client(config1);
            config1.user = "party5";
            var user5 = new Client(config1);
            config1.user = "party6";
            var user6 = new Client(config1);

            _ = user1.Login();
            Thread.Sleep(4000);
            _ = user2.Login();
            Thread.Sleep(4000);
            _ = user3.Login();
            Thread.Sleep(4000);
            _ = user4.Login();
            Thread.Sleep(4000);
            _ = user5.Login();
            Thread.Sleep(4000);
            await user6.Login();
            Thread.Sleep(4000);
            user1.IncomingChat += YourObject_IncomingChat;
            user1.IncomingPartyInvite += YourObject_IncomingPartyInvite;
            user2.IncomingChat += YourObject_IncomingChat;
            user2.IncomingPartyInvite += YourObject_IncomingPartyInvite;
            user3.IncomingChat += YourObject_IncomingChat;
            user3.IncomingPartyInvite += YourObject_IncomingPartyInvite;
            user4.IncomingChat += YourObject_IncomingChat;
            user4.IncomingPartyInvite += YourObject_IncomingPartyInvite;
            user5.IncomingChat += YourObject_IncomingChat;
            user5.IncomingPartyInvite += YourObject_IncomingPartyInvite;
            user6.IncomingChat += YourObject_IncomingChat;
            user6.IncomingPartyInvite += YourObject_IncomingPartyInvite;

            Thread.Sleep(4000);
            user1.SendPartyInvite(user2.PlayerData.ID);
            user1.SendPartyInvite(user3.PlayerData.ID);
            user1.SendPartyInvite(user4.PlayerData.ID);
            user1.SendPartyInvite(user5.PlayerData.ID);
            user1.SendPartyInvite(user6.PlayerData.ID);
            Thread.Sleep(2000000);
            Exit();
        }

        private static void Exit()
        {
            Environment.Exit(1);
        }

        // Event handler method
        private static void YourObject_IncomingChat(object sender, IncomingChatEventArgs e)
        {
            var client = (Client)sender;
            //Console.WriteLine($"Chat from {e.Name}: {e.Message}");
            //Console.WriteLine($"Type: {e.MessageType}, IsGM: {e.IsGM}, Zone: {e.ZoneID}");
            if (e.Message.Contains("logout", StringComparison.CurrentCultureIgnoreCase))
            {
                _ = client.Logout();
            }
            else if (e.Message.Contains("attack", StringComparison.CurrentCultureIgnoreCase))
            {
                var targetIndexarg = e.Message[7..];
                var targetindex = ushort.Parse(targetIndexarg);
                client.Attack(targetindex);
            }
            else if (e.Message.Contains("cast", StringComparison.CurrentCultureIgnoreCase))
            {
                var cure = client.Spellrepo.GetByName("cure");
                if (cure != null) client.CastMagic(1024, cure.Id);
                Console.WriteLine(client.CanUseSpell(1) + " " + client.CanUseSpell(128));
            }
            else if (e.Message.Contains("heal", StringComparison.CurrentCultureIgnoreCase))
                client.Heal(HealMode.Toggle);
            else if (e.Message.Contains("move", StringComparison.CurrentCultureIgnoreCase))
            {
                var ent = client.GetEntityByName("Test");
                if(ent != null)
                    client.MoveTo(ent.Pos);
            }
            else
                client.SendTell(e.Name, e.Message);
            // Do whatever you need with the data
        }

        private static void YourObject_IncomingPartyInvite(object sender, IncomingPartyInviteEventArgs e)
        {
            var client = (Client)sender;
            client.PartyInviteResponce(true);
        }
    }

    public class TimestampTextWriter(TextWriter originalOut) : TextWriter
    {
        public override Encoding Encoding => originalOut.Encoding;

        public override void WriteLine(string value)
        {
            var timestamp = $"[{DateTime.Now:HH:mm:ss.fff}] ";
            originalOut.WriteLine(timestamp + value);
        }
    }
}
