using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace vsmodtools
{
    public class Tools
    {

        public static void Init()
        {
            Program.RegisterCommand(new AddModCommand());
            Program.RegisterCommand(new PackModCommand());
            Program.RegisterCommand(new ExistModCommand());
            Program.RegisterCommand(new ListModCommand());
            Program.RegisterCommand(new PackAllModCommand());
            Program.RegisterCommand(new DeleteModCommand());
        }

        public static string GetModDirectory()
        {
            return Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + Path.DirectorySeparatorChar + "mods" + Path.DirectorySeparatorChar;
        }

        public static string GetModPath(string modid)
        {
            return GetModDirectory() + modid + Path.DirectorySeparatorChar;
        }

        public static bool DoesModExist(string modid)
        {
            return File.Exists(GetModPath(modid) + "modinfo.json");
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

        public AddModCommand() : base("add", "add <modid>", "Adds a new mod to the solution")
        {

        }

        public override bool Run(string[] args)
        {
            if (args.Length <= 1)
            {
                Console.WriteLine("Missing modid!");
                return false;
            }

            string modid = args[1];
            if (!Tools.IsValidModID(modid))
            {
                Console.WriteLine("'{0}' is not a valid modid!", modid);
                return false;
            }

            Console.WriteLine("'{0}' appears to be a valid modid. Creating new mod ...", modid);
            Assembly assembly = Assembly.GetExecutingAssembly();
            string folder = Tools.GetModPath(modid);
            if (!Directory.CreateDirectory(folder).Exists)
            {
                Console.WriteLine("Could not create mod directory.");
                return false;
            }
            Dictionary<string, string> variables = new Dictionary<string, string>();
            variables.Add("$(modid)", modid);
            variables.Add("$(gameversion)", "1.5.3");

            string projectfile = folder + modid + ".csproj";

            if (File.Exists(projectfile))
            {
                Console.WriteLine("This mod exists already!");
                return false;
            }

            File.WriteAllLines(projectfile, Tools.ReadLines("vsmodtools.project.template", variables));
            File.WriteAllLines(folder + "modinfo.json", Tools.ReadLines("vsmodtools.modinfo.template", variables));
            Directory.CreateDirectory(folder + "src");
            Directory.CreateDirectory(folder + "assets");
            Console.WriteLine("Created " + modid + " successfully ...");

            string solutionfile = Path.GetDirectoryName(assembly.Location) + Path.DirectorySeparatorChar + "VSMods.sln";
            if (!File.Exists(solutionfile))
            {
                Console.WriteLine("Could not find solution!");
                return false;
            }

            string projectID = "{" + Guid.NewGuid().ToString() + "}";
            List<string> list = new List<string>(File.ReadLines(solutionfile));
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


            return false;
        }

    }

    public class DeleteModCommand : Command
    {

        public DeleteModCommand() : base("delete", "delete <modid>", "Deletes a mod (irreversible)")
        {

        }

        public override bool Run(string[] args)
        {
            if (args.Length <= 1)
            {
                Console.WriteLine("Missing modid!");
                return false;
            }

            string modid = args[1];
            if (!Tools.IsValidModID(modid))
            {
                Console.WriteLine("'{0}' is not a valid modid!", modid);
                return false;
            }

            if (!Tools.DoesModExist(modid))
                Console.WriteLine("'{0}' does not exist!", modid);

            Console.WriteLine("Do you really want to delete '{0}'? Cannot be undone!!!", modid);

            Console.Write("Confirm it by typing in the modid: ");
            if (!Console.ReadLine().Equals(modid))
                return false;

            Console.WriteLine("Confirmed!");

            string solutionfile = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + Path.DirectorySeparatorChar + "VSMods.sln";
            if (!File.Exists(solutionfile))
            {
                Console.WriteLine("Could not find solution.");
                return false;
            }

            List<string> list = new List<string>(File.ReadLines(solutionfile));
            string projectID = null;
            bool insideProject = false;
            for (int i = 0; i < list.Count; i++)
            {
                var line = list[i];
                if (projectID == null && line.StartsWith("Project", StringComparison.OrdinalIgnoreCase))
                {
                    string[] parts = line.Split('"');
                    if (parts[3].Equals(modid, StringComparison.OrdinalIgnoreCase) && parts[5].Equals("mods" + Path.DirectorySeparatorChar + modid + Path.DirectorySeparatorChar + modid + ".csproj", StringComparison.OrdinalIgnoreCase))
                    {
                        projectID = parts[7];
                        insideProject = true;
                    }
                }

                if (insideProject)
                {
                    list.RemoveAt(i);
                    i--;
                    if (line.Contains("EndProject"))
                        insideProject = false;
                }
                else if (projectID != null && line.Contains(projectID))
                {
                    list.RemoveAt(i);
                    i--;
                }
            }

            if(projectID == null)
            {
                Console.WriteLine("Could not find project in solution.");
                return false;
            }

            File.WriteAllLines(solutionfile, list);
            Console.WriteLine("Successfully removed project from solution ...");

            string path = Tools.GetModPath(modid);
            Directory.Delete(path, true);
            Console.WriteLine("Deleted '{0}' ...", path);

            Console.WriteLine("'{0}' has been deleted.", modid);
            return true;
        }
    }


    public class ExistModCommand : Command
    {

        public ExistModCommand() : base("check", "check <modid>", "Checks whether a mod exists or not")
        {

        }

        public override bool Run(string[] args)
        {
            if (args.Length <= 1)
            {
                Console.WriteLine("Missing modid!");
                return false;
            }

            string modid = args[1];
            if (!Tools.IsValidModID(modid))
            {
                Console.WriteLine("'{0}' is not a valid modid!", modid);
                return false;
            }

            if (Tools.DoesModExist(modid))
                Console.WriteLine("'{0}' does exist!", modid);
            else
                Console.WriteLine("'{0}' does not exist!", modid);

            return false;
        }

    }

    public class ListModCommand : Command
    {
        public ListModCommand() : base("list", "lists all available mods")
        {

        }

        public override bool Run(string[] args)
        {
            List<string> mods = new List<string>();
            string directory = Tools.GetModDirectory();
            if (Directory.Exists(directory))
            {
                foreach (var mod in Directory.GetDirectories(directory, "*", SearchOption.TopDirectoryOnly))
                {
                    string modid = mod.Replace(directory, "");
                    if (Tools.DoesModExist(modid))
                        mods.Add(modid);
                    else
                        Console.WriteLine("Found invalid directory in mods folder '{0}'", modid);
                }
            }

            Console.WriteLine("Found {0} mod(s) in total ...", mods.Count);
            foreach (var mod in mods)
                Console.WriteLine(mod);

            return false;
        }
    }

    public class PackModCommand : Command
    {

        public PackModCommand() : base("pack", "pack <modid>", "Packs a mod of the solution into '/Releases/<modid>/")
        {

        }

        public override bool Run(string[] args)
        {
            if (args.Length <= 1)
            {
                Console.WriteLine("Missing modid!");
                return false;
            }

            string modid = args[1];
            if (!Tools.IsValidModID(modid))
            {
                Console.WriteLine("'{0}' is not a valid modid!", modid);
                return false;
            }

            if (!Tools.DoesModExist(modid))
            {
                Console.WriteLine("'{0}' does not exist!", modid);
                return false;
            }

            string modFolder = Tools.GetModPath(modid);
            Console.WriteLine("Collecting modinfo ...");
            JObject modinfo = JObject.Parse(File.ReadAllText(modFolder + "modinfo.json"));

            if (modinfo.Property("modid") == null)
                modinfo.Add("modid", JToken.FromObject(modid));

            if (!modinfo["modid"].ToString().Equals(modid, StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("Modid mismatch in 'modinfo.json'!");
                return false;
            }

            string version = modinfo.ContainsKey("version") ? modinfo["version"].ToString() : "1.0.0";

            Console.WriteLine("Creating v" + version + " ...");

            string releaseFolder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + Path.DirectorySeparatorChar + "releases" + Path.DirectorySeparatorChar + modid + Path.DirectorySeparatorChar;
            Console.WriteLine("Creating '{0}' directory ...", releaseFolder);
            if (!Directory.CreateDirectory(releaseFolder).Exists)
            {
                Console.WriteLine("Could not create release directory!");
                return false;
            }

            string zipFilePath = releaseFolder + modid + "_v" + version + ".zip";
            if (File.Exists(zipFilePath))
                File.Delete(zipFilePath);
            ZipArchive archive = ZipFile.Open(zipFilePath, ZipArchiveMode.Create);
            List<string> files = new List<string>();
            files.AddRange(Directory.GetFiles(modFolder, "*.dll", SearchOption.TopDirectoryOnly));
            files.AddRange(Directory.GetFiles(modFolder + "assets", "*", SearchOption.AllDirectories));
            files.AddRange(Directory.GetFiles(modFolder + "src", "*", SearchOption.AllDirectories));
            files.Add(modFolder + "modinfo.json");
            if (File.Exists(modFolder + "modicon.png"))
                files.Add(modFolder + "modicon.png");

            if (Directory.GetFiles(modFolder, "*.cs", SearchOption.TopDirectoryOnly).Length > 0)
                Console.WriteLine("Find invalid *.cs files in top directory. Please move them to '/src/' otherwise they will be ignored!");

            Console.WriteLine("Creating zip archive ...");
            foreach (var file in files)
            {
                string filename = file.Replace(modFolder, "").ToLower();
                Console.WriteLine("Adding '{0}' ...", filename);
                archive.CreateEntryFromFile(file, filename);
            }

            archive.Dispose();

            Console.WriteLine("Release of '{0}' has been created successfully in '/releases/" + modid + "/" + modid + "_v" + version + ".zip'", modid);
            return true;

        }

    }

    public class PackAllModCommand : Command
    {

        public PackAllModCommand() : base("pack-all", "Packs all mods of the solution into '/Releases/<modid>/")
        {

        }

        public override bool Run(string[] args)
        {
            List<string> mods = new List<string>();
            string directory = Tools.GetModDirectory();

            if (Directory.Exists(directory))
            {
                foreach (var mod in Directory.GetDirectories(directory, "*", SearchOption.TopDirectoryOnly))
                {
                    string modid = mod.Replace(directory, "");
                    if (Tools.DoesModExist(modid))
                        mods.Add(modid);
                    else
                        Console.WriteLine("Found invalid directory in mods folder '{0}'", modid);
                }
            }

            Console.WriteLine("Packing {0} mod(s) in total ..." + mods);

            int i = 0;
            foreach (var mod in mods)
            {
                Console.WriteLine("\n==Packing {0}==", mod);
                Program.RunCommand(new string[] { "pack", mod });
                i++;
                Console.WriteLine("Finished {0}/{1} ...", i, mods.Count);
            }

            return true;
        }
    }
}
