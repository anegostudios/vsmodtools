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
            Program.RegisterCommand(new AddDLLModCommand());
            Program.RegisterCommand(new PackModCommand());
            Program.RegisterCommand(new ExistModCommand());
            Program.RegisterCommand(new ListModCommand());
            Program.RegisterCommand(new PackAllModCommand());
            Program.RegisterCommand(new DeleteModCommand());
            Program.RegisterCommand(new UpdateModCommand());
            Program.RegisterCommand(new UpdateAllModCommand());
        }

        public static string GetApplicationDirectory()
        {
            return Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + Path.DirectorySeparatorChar;
        }

        public static string GetModDirectory()
        {
            return GetApplicationDirectory() + "mods" + Path.DirectorySeparatorChar;
        }

        public static string GetDLLModDirectory()
        {
            return GetApplicationDirectory() + "mods-dll" + Path.DirectorySeparatorChar;
        }

        public static string GetModPath(string modid, bool dll)
        {
            if(dll)
                return GetDLLModDirectory() + modid + Path.DirectorySeparatorChar;
            return GetModDirectory() + modid + Path.DirectorySeparatorChar;
        }

        public static bool IsDLLMod(string modid)
        {
            return !File.Exists(GetModPath(modid, false) + modid + ".csproj");
        }

        public static bool DoesModExist(string modid, bool dll)
        {
            return File.Exists(GetModPath(modid, dll) + modid + ".csproj");
        }

        public static Tuple<string, bool>[] GetModPaths()
        {
            return new Tuple<string, bool>[] { Tuple.Create<string, bool>(Tools.GetModDirectory(), false), Tuple.Create<string, bool>(Tools.GetDLLModDirectory(), true) };
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
                    possiblePaths.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "ApplicationData", "vintagestory"));
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
            foreach (var directory in Tools.GetModPaths())
            {
                if (Directory.Exists(directory.Item1))
                {
                    foreach (var mod in Directory.GetDirectories(directory.Item1, "*", SearchOption.TopDirectoryOnly))
                    {
                        string modid = mod.Replace(directory.Item1, "");
                        if (File.Exists(mod + modid + ".csproj"))
                            modProjectFilePatcher.Patch(mod + modid + ".csproj");
                    }
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

        public AddModCommand(string name, string syntax, string description) : base(name, syntax, description)
        {

        }

        public virtual void ModifyVariables(Dictionary<string, string> variables)
        {

        }

        public virtual void CreateProjectFiles(string folder, Dictionary<string, string> variables)
        {
            File.WriteAllLines(folder + "modinfo.json", Tools.ReadLines("vsmodtools.modinfo.template", variables));
            Directory.CreateDirectory(folder + "src");
            Directory.CreateDirectory(folder + "assets");
        }

        public virtual bool IsDLL()
        {
            return false;
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
            string folder = Tools.GetModPath(modid, IsDLL());
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
                { "$(projectguid)", projectID },
                { "$(projectguidwithout)", projectID.Replace("{", "").Replace("}", "") },
                { "$(AssetFiles)", "<Folder Include=\"assets\\\" />" },
                { "$(SrcFiles)", "<Folder Include=\"src\\\" />\n    <Content Include=\"modinfo.json\" />" },
                { "$(binpathdebug)", "..\\..\\bin\\Debug\\" + modid + "\\" },
                { "$(binpathrelease)", "..\\..\\bin\\Release\\" + modid + "\\" }
            };

            ModifyVariables(variables);

            string projectfile = folder + modid + ".csproj";

            if (File.Exists(projectfile))
            {
                Console.WriteLine("This mod exists already!");
                return false;
            }
            File.WriteAllLines(projectfile, Tools.ReadLines("vsmodtools.project.template", variables));

            CreateProjectFiles(folder, variables);

            Console.WriteLine("Created " + modid + " successfully ...");

            string solutionfile = Path.GetDirectoryName(assembly.Location) + Path.DirectorySeparatorChar + "VSMods.sln";
            if (!File.Exists(solutionfile))
            {
                Console.WriteLine("Could not find solution!");
                return false;
            }

            
            List<string> list = new List<string>(File.ReadLines(solutionfile));
            int step = 0;
            string launcherGUID = "";
            for (int i = 0; i < list.Count; i++)
            {
                var line = list[i];
                switch (step)
                {
                    case 0:
                        if (line.Contains("\"VSModLauncher.csproj\""))
                        {
                            string[] parts = line.Split('"');
                            if (parts.Length < 8)
                                Console.WriteLine("Something went wrong: " + parts + " line: " + line);
                            if (parts[3].Equals("VSModLauncher", StringComparison.OrdinalIgnoreCase) && parts[5].Equals("VSModLauncher.csproj", StringComparison.OrdinalIgnoreCase))
                                launcherGUID = parts[7];
                            step = 1;
                        }
                        break;
                    case 1:
                        if (Patch.StartsWith(line, "ProjectSection(ProjectDependencies) = postProject"))
                        {
                            list.Insert(i + 1, "		" + projectID + " = " + projectID + "");
                            step = 2;
                        }
                        else if (Patch.StartsWith(line, "EndProject"))
                        {
                            list.Insert(i, "	EndProjectSection");
                            list.Insert(i, "		" + projectID + " = " + projectID + "");
                            list.Insert(i, "	ProjectSection(ProjectDependencies) = postProject");
                            i--;
                            step = 2;
                        }
                        break;
                    case 2:
                        if (line.Replace(" ", "").Equals("EndProject", StringComparison.OrdinalIgnoreCase))
                        {
                            i++;
                            list.Insert(i, "EndProject");
                            list.Insert(i, "Project(\"{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}\") = \"" + modid + "\", \"" + (IsDLL() ? "mods-dll" : "mods") + Path.DirectorySeparatorChar + modid + Path.DirectorySeparatorChar + modid + ".csproj\", \"" + projectID + "\"");
                            i++;
                            step = 3;
                        }
                        break;
                    case 3:
                        if (line.Replace(" ", "").Contains("GlobalSection(ProjectConfigurationPlatforms)=postSolution"))
                            step = 4;
                        break;
                    case 4:
                        if (line.Contains("EndGlobalSection"))
                        {

                            list.InsertRange(i, new string[] { projectID + ".Debug|Any CPU.ActiveCfg = Debug|Any CPU",
                                projectID + ".Debug|Any CPU.Build.0 = Debug|Any CPU",
                                projectID + ".Release x64|Any CPU.ActiveCfg = Release|Any CPU",
                                projectID + ".Release x64|Any CPU.Build.0 = Release|Any CPU",
                                projectID + ".Release|Any CPU.ActiveCfg = Release|Any CPU",
                                projectID + ".Release|Any CPU.Build.0 = Release|Any CPU}" });
                            step = 5;
                            i = list.Count;
                        }
                        break;
                }
            }

            if (step < 5)
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

    public class AddDLLModCommand : AddModCommand
    {

        public AddDLLModCommand() : base("add-dll", "add-dll <modid>", "Adds a new dll mod to the solution")
        {

        }

        public override void ModifyVariables(Dictionary<string, string> variables)
        {
            base.ModifyVariables(variables);
            variables["$(binpathdebug)"] = "..\\..\\mods\\";
            variables["$(binpathrelease)"] = "..\\..\\mods\\";
            variables["$(AssetFiles)"] = "";
            variables["$(SrcFiles)"] = "<Compile Include=\"Properties\\AssemblyInfo.cs\" />";
        }

        public override void CreateProjectFiles(string folder, Dictionary<string, string> variables)
        {
            Directory.CreateDirectory(folder + Path.DirectorySeparatorChar + "Properties" + Path.DirectorySeparatorChar);
            File.WriteAllLines(folder + Path.DirectorySeparatorChar + "Properties" + Path.DirectorySeparatorChar + "AssemblyInfo.cs", Tools.ReadLines("vsmodtools.assemblyinfo.template", variables));
        }

        public override bool IsDLL()
        {
            return true;
        }
    }

    public class UpdateModCommand : VSCommand
    {

        public UpdateModCommand() : base("update", "update <modid>", "Updates the mod project file, special configuration will be lost.")
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
            bool dll = Tools.IsDLLMod(modid);
            if (!Tools.DoesModExist(modid, dll))
            {
                Console.WriteLine("'{0}' does not exist!", modid);
                return false;
            }

            string folder = Tools.GetModPath(modid, dll);
            string solutionfile = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + Path.DirectorySeparatorChar + "VSMods.sln";
            if (!File.Exists(solutionfile))
            {
                Console.WriteLine("Could not find solution.");
                return false;
            }

            List<string> list = new List<string>(File.ReadLines(solutionfile));
            string projectID = null;
            bool dependencies = false;
            for (int i = 0; i < list.Count; i++)
            {
                var line = list[i];
                if (projectID == null && Patch.StartsWith(line, "Project("))
                {
                    string[] parts = line.Split('"');
                    if (parts.Length < 8)
                        Console.WriteLine("Something went wrong: " + parts + " line: " + line);
                    if (parts[3].Equals(modid, StringComparison.OrdinalIgnoreCase) && parts[5].Equals((dll ? "mods-dll" : "mods") + Path.DirectorySeparatorChar + modid + Path.DirectorySeparatorChar + modid + ".csproj", StringComparison.OrdinalIgnoreCase))
                    {
                        projectID = parts[7];
                        list[i] = line.Replace("{9A19103F-16F7-4668-BE54-9A1E7A4F7556}", "{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}");
                    }
                }
                else if (projectID != null)
                {
                    if (dependencies)
                    {
                        list.RemoveAt(i);
                        i--;
                        if (Patch.StartsWith(line, "EndProjectSection"))
                            break;
                    }
                    else if (Patch.StartsWith(line, "ProjectSection(ProjectDependencies) = postProject"))
                    {
                        list.RemoveAt(i);
                        i--;
                        dependencies = true;
                    }
                }
            }

            int step = 0;
            for (int i = 0; i < list.Count; i++)
            {
                var line = list[i];
                switch (step)
                {
                    case 0:
                        if (Patch.StartsWith(line, "Project("))
                        {
                            string[] parts = line.Split('"');
                            if (parts.Length < 8)
                                Console.WriteLine("Something went wrong: " + parts + " line: " + line);
                            if (parts[3].Equals("VSModLauncher", StringComparison.OrdinalIgnoreCase) && parts[5].Equals("VSModLauncher.csproj", StringComparison.OrdinalIgnoreCase))
                                step = 1;
                        }
                        break;
                    case 1:
                        if (Patch.StartsWith(line, "ProjectSection(ProjectDependencies) = postProject"))
                            step = 2;
                        else if (Patch.StartsWith(line, "EndProject"))
                        {
                            list.Insert(i, "	EndProjectSection");
                            list.Insert(i, "		" + projectID + " = " + projectID + "");
                            list.Insert(i, "	ProjectSection(ProjectDependencies) = postProject");
                            step = 3;
                        }
                        break;
                    case 2:
                        if(Patch.StartsWith(line, projectID))
                            step = 3;
                        else if(Patch.StartsWith(line, "EndProjectSection"))
                        {
                            list.Insert(i, "		" + projectID + " = " + projectID + "");
                            step = 3;
                        }
                        break;
                }
                if (step == 3)
                    break;
            }

            if (projectID == null)
            {
                Console.WriteLine("Could not find project guid!");
                return false;
            }

            File.WriteAllLines(solutionfile, list);
            Console.WriteLine("Successfully updated solution ...");

            string assetFiles = "";
            string srcFiles;
            if (dll)
            {
                srcFiles = "";
                foreach (var file in Directory.GetFiles(folder, "*.cs", SearchOption.AllDirectories))
                {
                    srcFiles += "<Compile Include=\"" + file.Replace(folder, "") + "\" />\n";
                }
            }
            else
            {
                assetFiles = "<Folder Include=\"assets\\\" />";
                foreach (var file in Directory.GetFiles(Path.Combine(folder, "assets"), "*", SearchOption.AllDirectories))
                {
                    assetFiles += "<Content Include=\"" + file.Replace(folder, "") + "\" />\n";
                }

                srcFiles = "<Folder Include=\"src\\\" />\n    <Content Include=\"modinfo.json\" />";
                foreach (var file in Directory.GetFiles(Path.Combine(folder, "src"), "*.cs", SearchOption.AllDirectories))
                {
                    srcFiles += "<Compile Include=\"" + file.Replace(folder, "") + "\" />\n";
                }

                foreach (var file in Directory.GetFiles(Path.Combine(folder, "src"), "*.dll", SearchOption.TopDirectoryOnly))
                {
                    srcFiles += "<Compile Include=\"" + file.Replace(folder, "") + "\" />\n";
                }
            }

            Dictionary<string, string> variables = new Dictionary<string, string>
            {
                { "$(modid)", modid },
                { "$(gameversion)", "1.5.3" },
                { "$(vspath)", vspath },
                { "$(projectguid)", projectID },
                { "$(AssetFiles)", assetFiles },
                { "$(SrcFiles)", srcFiles },
                { "$(binpathdebug)", "..\\..\\bin\\Debug\\" + modid + "\\" },
                { "$(binpathrelease)", "..\\..\\bin\\Release\\" + modid + "\\" }
            };

            if (dll)
            {
                variables["$(binpathdebug)"] = "..\\..\\mods\\";
                variables["$(binpathrelease)"] = "..\\..\\mods\\";
            }

            string projectfile = folder + modid + ".csproj";
            File.Delete(projectfile);
            File.WriteAllLines(projectfile, Tools.ReadLines("vsmodtools.project.template", variables));

            try
            {
                Directory.Delete(Path.Combine(folder, "bin"), true);
                Directory.Delete(Path.Combine(folder, "obj"), true);
            }
            catch (Exception)
            {

            }

            Console.WriteLine("Updated '{0}' successfully!", modid);
            return true;
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

            bool dll = Tools.IsDLLMod(modid);

            if (!Tools.DoesModExist(modid, dll))
            {
                Console.WriteLine("'{0}' does not exist!", modid);
                return false;
            }

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
                if (projectID == null && Patch.StartsWith(line, "Project("))
                {
                    string[] parts = line.Split('"');
                    if (parts[3].Equals(modid, StringComparison.OrdinalIgnoreCase) && parts[5].Equals((dll ? "mods-dll" : "mods") + Path.DirectorySeparatorChar + modid + Path.DirectorySeparatorChar + modid + ".csproj", StringComparison.OrdinalIgnoreCase))
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
                        break;
                }
            }

            

            if (projectID == null)
            {
                Console.WriteLine("Could not find project in solution.");
                return false;
            }

            for (int i = 0; i < list.Count; i++)
            {
                var line = list[i];
                if (line.Contains(projectID))
                {
                    list.RemoveAt(i);
                    i--;
                }
            }

            File.WriteAllLines(solutionfile, list);
            Console.WriteLine("Successfully removed project from solution ...");

            string path = Tools.GetModPath(modid, dll);
            Directory.Delete(path, true);
            Console.WriteLine("Deleted '{0}' ...", path);

            if (File.Exists(Tools.GetModDirectory() + modid + ".dll"))
            {
                File.Delete(Tools.GetModDirectory() + modid + ".dll");
                Console.WriteLine("Deleted '{0}' ...", modid + ".dll");
            }

            if (File.Exists(Tools.GetModDirectory() + modid + ".pdb"))
            {
                File.Delete(Tools.GetModDirectory() + modid + ".pdb");
                Console.WriteLine("Deleted '{0}' ...", modid + ".pdb");
            }

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

            bool dll = Tools.IsDLLMod(modid);

            if (Tools.DoesModExist(modid, dll))
                Console.WriteLine("'{0}' does exist! Modtype: {1}", modid, dll ? "dll" : "folder");
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
            foreach (var directory in new string[] { Tools.GetModDirectory(), Tools.GetDLLModDirectory() })
            {
                if (Directory.Exists(directory))
                {
                    foreach (var mod in Directory.GetDirectories(directory, "*", SearchOption.TopDirectoryOnly))
                    {
                        string modid = mod.Replace(directory, "");
                        bool dll = Tools.IsDLLMod(modid);
                        if (Tools.DoesModExist(modid, dll))
                            mods.Add(modid);
                        else
                            Console.WriteLine("Found invalid directory in mods folder '{0}'", modid);
                    }
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

            if(Tools.IsDLLMod(modid) && Tools.DoesModExist(modid, true))
            {
                Console.WriteLine("'{0} is a dll mod!");
                return false;
            }

            if (!Tools.DoesModExist(modid, false))
            {
                Console.WriteLine("'{0}' does not exist!", modid);
                return false;
            }

            string modFolder = Tools.GetModPath(modid, false);
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

    public class UpdateAllModCommand : VSCommand
    {

        public UpdateAllModCommand() : base("update-all", "Updates each project file of every mod, all special configuration will be lost.")
        {

        }

        public override bool Run(string[] args, string vspath)
        {
            List<string> mods = new List<string>();
            foreach (var directory in Tools.GetModPaths())
            {
                if (Directory.Exists(directory.Item1))
                {
                    foreach (var mod in Directory.GetDirectories(directory.Item1, "*", SearchOption.TopDirectoryOnly))
                    {
                        string modid = mod.Replace(directory.Item1, "");
                        if (Tools.DoesModExist(modid, directory.Item2))
                            mods.Add(modid);
                        else
                            Console.WriteLine("Found invalid directory in mods folder '{0}'", modid);
                    }
                }
            }

            Console.WriteLine("Updating {0} mod(s) in total ...", mods.Count);

            int i = 0;
            foreach (var mod in mods)
            {
                Console.WriteLine("\n==Update {0}==", mod);
                Program.RunCommand(new string[] { "update", mod });
                i++;
                Console.WriteLine("Finished {0}/{1} ...", i, mods.Count);
            }

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
                    if (Tools.DoesModExist(modid, false))
                        mods.Add(modid);
                    else
                        Console.WriteLine("Found invalid directory in mods folder '{0}'", modid);
                }
            }

            Console.WriteLine("Packing {0} mod(s) in total ...", mods.Count);

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
