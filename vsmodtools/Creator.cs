using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace vsmodtools
{
    public class Creator
    {

        public static void Init()
        {
            Program.registerCommand(new AddModCommand());
        }

        public static bool IsValidModID(string str)
        {
            if (string.IsNullOrEmpty(str)) return false;
            for (var i = 0; i < str.Length; i++)
            {
                var chr = str[i];
                var isLetter = (chr >= 'a') && (chr <= 'z');
                var isDigit = (chr >= '0') && (chr <= '9');
                if (isLetter || (isDigit && (i != 0))) continue;
                return false;
            }
            return true;
        }

        public static IEnumerable<string> ReadLines(string filename, Dictionary<string, string> variables)
        {
            return ReadLines(() => Assembly.GetExecutingAssembly().GetManifestResourceStream(filename), Encoding.UTF8, variables);
        }

        public static IEnumerable<string> ReadLines(Func<Stream> streamProvider, Encoding encoding, Dictionary<string, string> variables)
        {
            using (var stream = streamProvider())
            using (var reader = new StreamReader(stream, encoding))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    foreach (var variable in variables)
                        line = line.Replace(variable.Key, variable.Value);
                    yield return line;
                }
            }
        }

    }

    public class AddModCommand : Command
    {

        public AddModCommand() : base("add", "/add <modid>", "Adds a new mod to the solution")
        {

        }

        public override bool Run(string[] args)
        {
            if(args.Length <= 1)
            {
                Console.WriteLine("Missing modid!");
                return false;
            }

            string modid = args[1];
            if (Creator.IsValidModID(modid))
            {
                Console.WriteLine("'{0}' appears to be a valid modid. Creating new mod ...", modid);
                Assembly assembly = Assembly.GetExecutingAssembly();
                string folder = Path.GetDirectoryName(assembly.Location) + Path.DirectorySeparatorChar + "mods" + Path.DirectorySeparatorChar + modid + Path.DirectorySeparatorChar;
                if (Directory.CreateDirectory(folder).Exists)
                {
                    Dictionary<string, string> variables = new Dictionary<string, string>();
                    variables.Add("$(modid)", modid);
                    variables.Add("$(gameversion)", "1.5.3");

                    string projectfile = folder + modid + ".csproj";
                    if (!File.Exists(projectfile))
                    {
                        File.WriteAllLines(projectfile, Creator.ReadLines("vsmodtools.project.template", variables));
                        File.WriteAllLines(folder + "modinfo.json", Creator.ReadLines("vsmodtools.modinfo.template", variables));
                        Directory.CreateDirectory(folder + "src");
                        Directory.CreateDirectory(folder + "assets");
                        Console.WriteLine("Created " + modid + " successfully ...");

                        string solutionfile = Path.GetDirectoryName(assembly.Location) + Path.DirectorySeparatorChar + "VSMods.sln";
                        if (File.Exists(solutionfile))
                        {
                            string projectID = "{" + Guid.NewGuid().ToString() + "}";
                            List<string> list = File.ReadLines(solutionfile).ToList();
                            int step = 0;
                            for (int i = 0; i < list.Count; i++)
                            {
                                var line = list[i];
                                switch (step)
                                {
                                    case 0:
                                        if (line.Contains("\"VSModLauncher.csproj\""))
                                            step = 1;
                                        break;
                                    case 1:
                                        if (line.Replace(" ", "").Equals("EndProject", StringComparison.OrdinalIgnoreCase))
                                        {
                                            i++;
                                            list.Insert(i, "EndProject");
                                            list.Insert(i, "Project(\"{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}\") = \"" + modid + "\", \"mods" + Path.DirectorySeparatorChar + modid + Path.DirectorySeparatorChar + modid + ".csproj\", \"" + projectID + "\"");
                                            i++;
                                            step = 2;
                                        }
                                        break;
                                    case 2:
                                        if (line.Replace(" ", "").Contains("GlobalSection(ProjectConfigurationPlatforms)=postSolution"))
                                            step = 3;
                                        break;
                                    case 3:
                                        if (line.Contains("EndGlobalSection"))
                                        {

                                            list.InsertRange(i, new string[] { projectID + ".Debug|Any CPU.ActiveCfg = Debug|Any CPU",
                                                projectID + ".Debug|Any CPU.Build.0 = Debug|Any CPU",
                                                projectID + ".Release x64|Any CPU.ActiveCfg = Release|Any CPU",
                                                projectID + ".Release x64|Any CPU.Build.0 = Release|Any CPU",
                                                projectID + ".Release|Any CPU.ActiveCfg = Release|Any CPU",
                                                projectID + ".Release|Any CPU.Build.0 = Release|Any CPU}" });
                                            step = 4;
                                            i = list.Count;
                                        }
                                        break;
                                }
                            }
                            if (step < 4)
                                Console.WriteLine("Something went wrong, could not complete solution injection (" + step + ")");
                            else
                            {
                                File.WriteAllLines(solutionfile, list);
                                Console.WriteLine("Solution injection complete ...");
                                return true;
                            }

                        }
                        else
                            Console.WriteLine("Could not find solution!");
                    }
                    else
                        Console.WriteLine("This mod exists already!");
                }

            }
            else
                Console.WriteLine("'{0} is not a valid modid!", modid);

            return false;
        }

    }
}
