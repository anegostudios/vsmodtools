using ICSharpCode.SharpZipLib.Core;
using ICSharpCode.SharpZipLib.Zip;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace vsmodtools
{
    public class Tools
    {

        public static void Init()
        {
            Program.RegisterCommand(new SetupCommand());
            Program.RegisterCommand(new AddModCommand());
            Program.RegisterCommand(new PackModCommand());
            Program.RegisterCommand(new ExistModCommand());
            Program.RegisterCommand(new ListModCommand());
            Program.RegisterCommand(new PackAllModCommand());
            Program.RegisterCommand(new DeleteModCommand());
        }

        public static string GetApplicationDirectory()
        {
            return Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + Path.DirectorySeparatorChar;
        }

        public static string GetModDirectory()
        {
            return GetApplicationDirectory() + "mods" + Path.DirectorySeparatorChar;
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

    public class SetupCommand : Command
    {

        public SetupCommand() : base("setup", "setup [optional path]", "Updates VintageStory paths.")
        {

        }

        public static bool CheckPath(string path)
        {
            if (path.Contains("$(AppData)"))
                path = path.Replace("$(AppData)", Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData));

            Console.WriteLine("Searching for VS in \"{0}\" ... ", path);

            if (File.Exists(path + Path.DirectorySeparatorChar + "VintagestoryAPI.dll"))
            {
                //Loading VintageStoryAPI.dll
                Assembly apiDLL = Assembly.LoadFile(path + Path.DirectorySeparatorChar + "VintagestoryAPI.dll");
                Type gameVersionClass = apiDLL.GetType("Vintagestory.API.Config.GameVersion");

                FieldInfo shortGameVersion = gameVersionClass.GetField("ShortGameVersion");
                string gameVersion = (string)shortGameVersion.GetValue(null);

                Console.WriteLine("VintageStory v{0} detected!", gameVersion);

                string newestVersion = null;
                try
                {
                    using (var wc = new System.Net.WebClient())
                    {
                        newestVersion = wc.DownloadString("http://api.vintagestory.at/lateststable.txt");
                    }
                }
                catch (Exception)
                {
                    newestVersion = null;
                }

                bool requiresUpdate = (bool)gameVersionClass.GetMethod("IsNewerVersionThan", new Type[] { typeof(string), typeof(string) }).Invoke(null, new object[] { newestVersion, gameVersion });
                if (requiresUpdate)
                {
                    ConsoleColor before = Console.ForegroundColor;
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("Outdated VintageStory version detected. New version v{0} available!", newestVersion);
                    Console.ForegroundColor = before;
                    Console.WriteLine("Continuing anyway ...");
                }
                else
                    Console.WriteLine("VintageStory is up-to-date!");

                return true;
            }

            return false;
        }

        public override bool Run(string[] args)
        {
            Console.WriteLine("Setting up workspace ...");
            List<string> possiblePaths = new List<string>();

            if (args.Length == 1)
            {
                possiblePaths.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Vintagestory"));
                
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    Console.WriteLine("Detecting Linux ...");

                    possiblePaths.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "ApplicationData", "Vintagestory"));
                    possiblePaths.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "Vintagestory"));
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    Console.WriteLine("Detecting MacOS ...");

                    possiblePaths.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "Vintagestory"));
                }
                else
                {
                    Console.WriteLine("Detecting Windows ...");

                    possiblePaths.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonProgramFiles), "Vintagestory"));
                    possiblePaths.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonProgramFilesX86), "Vintagestory"));
                    possiblePaths.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms), "Vintagestory"));
                }
            }
            else
                possiblePaths.Add(args[1]);

            string vspath = null;

            foreach (var path in possiblePaths)
            {
                if (CheckPath(path))
                {
                    vspath = path;
                    break;
                }
            }

            if (vspath == null)
            {
                Console.WriteLine("Could not find VintageStory!");
                Console.Write("Please enter the installtion path: ");
                vspath = Console.ReadLine();

                if (!CheckPath(vspath))
                    return false;
            }

            vspath = vspath.Replace(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "$(AppData)");
            if (!vspath.EndsWith("" + Path.DirectorySeparatorChar))
                vspath += Path.DirectorySeparatorChar;

            Console.WriteLine("\nGenerating path cache file ...");
            string tempFile = Tools.GetApplicationDirectory() + "modtools.temp";
            if (File.Exists(tempFile))
                File.Delete(tempFile);
            File.WriteAllText(tempFile, vspath);

            Console.WriteLine("\nStarting to inject paths patches ...");

            vspath = vspath.Replace('/', '\\');

            new Patcher(new LinePatch("<StartProgram>", "<StartProgram>" + vspath + "Vintagestory.exe</StartProgram>"),
                new LinePatch("<StartWorkingDirectory>", "<StartWorkingDirectory>" + vspath + "</StartWorkingDirectory>"),
                new LinePatch("<ReferencePath>", "<ReferencePath>" + vspath + ";" + vspath + "Lib\\</ReferencePath>")
            ).Patch(Tools.GetApplicationDirectory() + "VSModLauncher.csproj.user");

            string copyCommand = "copy";
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) || RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                copyCommand = "cp";
            
            //also the mod template should be a .net v4.5.2 class library and not a .netcore standard 2 (but this might open up even more issues)

            new Patcher(new ConditionedLinePatch("<Reference Include=\"protobuf-net\">", "<HintPath>", "<HintPath>" + vspath + "Lib\\protobuf-net.dll</HintPath>", "</Reference>"),
                new ConditionedLinePatch("<Reference Include=\"VintagestoryAPI", "<HintPath>", "<HintPath>" + vspath + "VintagestoryAPI.dll</HintPath>", "</Reference>"),
                new ConditionedLinePatch("<Reference Include=\"VSSurvivalMod\">", "<HintPath>", "<HintPath>" + vspath + "Mods\\VSSurvivalMod.dll</HintPath>", "</Reference>"),
                new InBetweenPatch("<PostBuildEvent>", "</PostBuildEvent>", copyCommand + " \"$(TargetPath)\" \"" + vspath.Replace("$(AppData)", "%25appdata%25") + "Mods\\\"", copyCommand + " \"$(TargetDir)\\$(TargetName).pdb\" \"" + vspath.Replace("$(AppData)", "%25appdata%25") + "Mods\\\"")
            ).Patch(Tools.GetApplicationDirectory() + "VSModLauncher.csproj");

            Patcher modProjectFilePatcher = new Patcher(new ConditionedLinePatch("<Reference Include=\"VintagestoryAPI\">", "<HintPath>", "<HintPath>" + vspath + "VintagestoryAPI.dll</HintPath>", "</Reference>"));
            string directory = Tools.GetModDirectory();
            if (Directory.Exists(directory))
            {
                foreach (var mod in Directory.GetDirectories(directory, "*", SearchOption.TopDirectoryOnly))
                {
                    string modid = mod.Replace(directory, "");
                    if (File.Exists(mod + modid + ".csproj"))
                        modProjectFilePatcher.Patch(mod + modid + ".csproj");
                }
            }

            Console.WriteLine("Setup complete!");
            return true;
        }
    }

    public abstract class VSCommand : Command
    {
        public VSCommand(string name, string syntax, string description) : base(name, syntax, description)
        {
        }

        public VSCommand(string name, string description) : base(name, description)
        {
        }

        public override bool Run(string[] args)
        {
            Console.WriteLine("== Loading VintageStory ==");
            string tempFile = Tools.GetApplicationDirectory() + "modtools.temp";
            if (File.Exists(tempFile))
            {
                string vspath = File.ReadAllText(tempFile);
                if (SetupCommand.CheckPath(vspath))
                {
                    Console.WriteLine("\n== Running Task ==");
                    return Run(args, vspath);
                }
                Console.WriteLine("Path=\"{0}\" is not available anymore. Please setup your workspace again!");
            }
            else
                Console.WriteLine("Please type in \"setup [optional:path]\" to setup the workspace first!");

            return false;
        }

        public abstract bool Run(string[] args, string vspath);
    }

    public class AddModCommand : VSCommand
    {

        public AddModCommand() : base("add", "add <modid>", "Adds a new mod to the solution")
        {

        }

        public override bool Run(string[] args, string vspath)
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

            string projectID = "{" + Guid.NewGuid().ToString() + "}";

            Dictionary<string, string> variables = new Dictionary<string, string>
            {
                { "$(modid)", modid },
                { "$(gameversion)", "1.5.3" },
                { "$(vspath)", vspath },
                { "$(projectguid)", projectID }
            };

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

    public class DeleteModCommand : VSCommand
    {

        public DeleteModCommand() : base("delete", "delete <modid>", "Deletes a mod (irreversible)")
        {

        }

        public override bool Run(string[] args, string vspath)
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

            if (projectID == null)
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


    public class ExistModCommand : VSCommand
    {

        public ExistModCommand() : base("check", "check <modid>", "Checks whether a mod exists or not")
        {

        }

        public override bool Run(string[] args, string vspath)
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

    public class ListModCommand : VSCommand
    {
        public ListModCommand() : base("list", "lists all available mods")
        {

        }

        public override bool Run(string[] args, string vspath)
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

    public class PackModCommand : VSCommand
    {

        public PackModCommand() : base("pack", "pack <modid>", "Packs a mod of the solution into '/releases/<modid>/")
        {

        }

        public override bool Run(string[] args, string vspath)
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
            ZipOutputStream archive = new ZipOutputStream(File.Create(zipFilePath));
            archive.SetLevel(3);
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
                string filename = ZipEntry.CleanName(file.Replace(modFolder, "").ToLower());
                Console.WriteLine("Adding '{0}' ...", filename);

                FileInfo fi = new FileInfo(file);
                ZipEntry newEntry = new ZipEntry(filename);
                newEntry.DateTime = fi.LastWriteTime;
                newEntry.Size = fi.Length;

                archive.PutNextEntry(newEntry);

                byte[] buffer = new byte[4096];
                using (FileStream streamReader = File.OpenRead(file))
                {
                    StreamUtils.Copy(streamReader, archive, buffer);
                }
                archive.CloseEntry();
            }

            archive.IsStreamOwner = true;
            archive.Close();

            Console.WriteLine("Release of '{0}' has been created successfully in '/releases/" + modid + "/" + modid + "_v" + version + ".zip'", modid);
            return true;

        }

    }

    public class PackAllModCommand : VSCommand
    {

        public PackAllModCommand() : base("pack-all", "Packs all mods of the solution into '/releases/<modid>/")
        {

        }

        public override bool Run(string[] args, string vspath)
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
