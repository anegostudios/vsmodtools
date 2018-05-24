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

        public const string version = "1.0";

        static void Init()
        {
            // Register commands
            registerCommand(new HelpCommand());
            registerCommand(new ExitCommand());

            Creator.Init();

            commands = commands.OrderBy(x => x).ToList();

            Console.WriteLine("VintageStory ModTools v{0}", version);
        }

        public static void registerCommand(Command command)
        {
            commands.Add(command);
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

            start:
            if(args == null || args.Length == 0)
            {
                Console.Write("> ");
                string text = Console.ReadLine();
                args = Program.ParseArguments(text);
            }

            currentCommand = GetCommand(args);
            string name = GetCommandName(args);
            if (currentCommand == null || !currentCommand.Run(args))
            {
                args = null;
                goto start;
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
            return (new string(parmChars)).Split('\n');
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

        public Command(string name, string description) : this(name, "/" + name, description)
        {
            
        }

        public int CompareTo(object obj)
        {
            if (obj is Command)
                return Name.CompareTo((obj as Command).Name);
            return Name.CompareTo(obj);
        }

        public abstract bool Run(string[] args);

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
    
}
