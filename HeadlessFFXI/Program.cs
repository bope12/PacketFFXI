using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using static HeadlessFFXI.Client;

namespace HeadlessFFXI
{
    class Program
    {
        public static Client User;
        static async Task Main(string[] args)
        {
            #region Settings
            Config settings = new Config();
            settings.server = "127.0.0.1";
            if (args.Length == 6 || args.Length == 8)
            {
                for (int i = 0; i <= args.Length / 2; i += 2)
                {
                    string s = args[i];
                    switch (s)
                    {
                        case "-user":
                            settings.user = args[i + 1];
                            break;
                        case "-pass":
                            settings.password = args[i + 1];
                            break;
                        case "-slot":
                            settings.char_slot = Int16.Parse(args[i + 1]) - 1;
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
                string line;
                System.IO.StreamReader cfg = new System.IO.StreamReader("config.cfg");
                while ((line = cfg.ReadLine()) != null)
                {
                    string[] setting = line.Split(":");
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
            User = new Client(settings, true, false);
            await User.Login();
            User.IncomingChat += YourObject_IncomingChat;
            User.IncomingPartyInvite += YourObject_IncomingPartyInvite;
            Thread.Sleep(2000000);
            await Exit();
            return;
        }
        static async Task Exit()
        {
            System.Environment.Exit(1);
        }

        // Event handler method
        private static void YourObject_IncomingChat(object sender, IncomingChatEventArgs e)
        {
            var client = (Client)sender;
            //Console.WriteLine($"Chat from {e.Name}: {e.Message}");
            //Console.WriteLine($"Type: {e.MessageType}, IsGM: {e.IsGM}, Zone: {e.ZoneID}");
            if (e.Message.ToLower().Contains("logout"))
            {
                client.Logout();
            }
            else if (e.Message.ToLower().Contains("invite"))
                client.SendPartyInvite(e.Name);
            else if (e.Message.ToLower().Contains("attack"))
            {
                var targetIndexarg = e.Message[7..];
                var targetindex = UInt16.Parse(targetIndexarg);
                client.Attack(targetindex);
            }
            else if (e.Message.ToLower().Contains("cast"))
            {
                var cure = client.Spellrepo.GetByName("cure");
                client.CastMagic(1024, cure.Id);
                Console.WriteLine(client.CanUseSpell(1).ToString() + " " + client.CanUseSpell(128).ToString());
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
}
