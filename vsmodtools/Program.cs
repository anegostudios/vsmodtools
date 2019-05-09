using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace vsmodtools
{
    class Program
    {
        internal static List<Command> commands = new List<Command>();
        internal static Command currentCommand;

        public const string version = "1.5.2";

        static void Init()
        {
            // Register commands
            RegisterCommand(new HelpCommand());
            RegisterCommand(new ExitCommand());
            RegisterCommand(new UpdateCommand());

            Tools.Init();

            commands = commands.OrderBy(x => x).ToList();

            Console.WriteLine("VintageStory ModTools v{0}", version);

            CheckForUpdate();
        }

        public static bool CheckForUpdate()
        {
            using (var wc = new System.Net.WebClient())
            {
                var current = new Version(version);

                JObject latest = JObject.Parse(wc.DownloadString("http://api.vintagestory.at/devtools/latest.json"));
                JToken versions = latest["versions"];
                
                foreach (var version in versions)
                {
                    var otherVersion = new Version(version.Value<string>("id"));

                    if(current.CompareTo(otherVersion) < 0)
                    {
                        ConsoleColor before = Console.ForegroundColor;
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine("New update available. Please type in 'install-update' to get started!");
                        Console.ForegroundColor = before;
                        return true;
                    }
                }
            }
            return false;
        }

        public static void RegisterCommand(Command command)
        {
            commands.Add(command);
        }

        public static bool RunCommand(string[] args)
        {
            Command command = GetCommand(args);
            return command != null ? command.Run(args) : false;
        }

        public static string GetCommandName(string[] args)
        {
            if (args == null || args.Length == 0)
                return "";
            return args[0];
        }

        public static Command GetCommand(string[] args)
        {
            if (args.Length == 0)
                return null;

            string name = args[0];
            foreach(var command in commands)
                if (command.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                    return command;

            if(!String.IsNullOrEmpty(name))
                Console.WriteLine("Command '{0}' could not be found!", name);

            return null;
        }

        static void Main(string[] args)
        {
            Init();

            bool hasInitalCommands = args.Length > 0;

            start:
            if(args == null || args.Length == 0)
            {
                Console.Write("> ");
                string text = Console.ReadLine();
                args = Program.ParseArguments(text);
            }

            try
            {
                currentCommand = GetCommand(args);
                string name = GetCommandName(args);
                if (currentCommand == null || !currentCommand.Run(args) || (!hasInitalCommands && !currentCommand.ForceExit))
                {
                    args = null;
                    goto start;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("\nSomething went wrong!");
                Console.WriteLine(e);
            }

            Console.WriteLine("\nPress any key to continue ...");
            Console.ReadKey();
        }

        public static string[] ParseArguments(string commandLine)
        {
            char[] parmChars = commandLine.ToCharArray();
            bool inQuote = false;
            for (int index = 0; index < parmChars.Length; index++)
            {
                if (parmChars[index] == '"')
                    inQuote = !inQuote;
                if (!inQuote && parmChars[index] == ' ')
                    parmChars[index] = '\n';
            }
            string[] result = (new string(parmChars)).Split('\n');
            for(int i = 0; i < result.Length; i++)
            {
                var line = result[i];
                if (line.StartsWith("\"") && line.EndsWith("\""))
                    result[i] = line.Substring(1, line.Length - 2);
            }
            return result;
        }
    }

    public abstract class Command : IComparable
    {
        public readonly string Name;
        public readonly string Syntax;
        public readonly string Description;

        public Command(string name, string syntax, string description)
        {
            this.Name = name;
            this.Syntax = syntax;
            this.Description = description;
        }

        public Command(string name, string description) : this(name, name, description)
        {
            
        }

        public int CompareTo(object obj)
        {
            if (obj is Command)
                return Name.CompareTo((obj as Command).Name);
            return Name.CompareTo(obj);
        }

        public abstract bool Run(string[] args);

        public virtual bool ForceExit { get { return false; } }
    }

    class ExitCommand : Command
    {
        public ExitCommand() : base("exit", "Terminates the application")
        {

        }

        public override bool Run(string[] args)
        {
            return true;
        }

        public override bool ForceExit => true;
    }

    class HelpCommand : Command
    {

        public HelpCommand() : base("help", "Lists all available commands")
        {

        }

        public override bool Run(string[] args)
        {
            Console.WriteLine("List of all commands:");
            foreach(var command in Program.commands)
                Console.WriteLine(command.Syntax + " - " + command.Description);
            return false;
        }
    }

    class UpdateCommand : Command
    {

        public UpdateCommand() : base("install-update", "Opens a website to download the update if available")
        {

        }

        public override bool Run(string[] args)
        {
            using (var wc = new System.Net.WebClient())
            {
                var current = new Version(Program.version);

                JObject latest = JObject.Parse(wc.DownloadString("http://api.vintagestory.at/devtools/latest.json"));
                JToken versions = latest["versions"];

                foreach (var version in versions)
                {
                    var otherVersion = new Version(version.Value<string>("id"));

                    if (current.CompareTo(otherVersion) < 0)
                    {
                        Console.WriteLine("New version available v{0}!", otherVersion);
                        System.Diagnostics.Process.Start(version.Value<string>("url"));
                        return true;
                    }
                }
            }
            Console.WriteLine("No new version available.");
            return false;
        }

    }
    
}
