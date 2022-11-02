using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.CSharp;
using ICSharpCode.Decompiler.CSharp.ProjectDecompiler;
using ICSharpCode.Decompiler.Metadata;
using Newtonsoft.Json;
using System.Diagnostics;
using System.Reflection;

namespace LwDecomp
{
    internal class Program
    {
        static void Main(string[] args)
        {
            var ourFolder = new FileInfo(Assembly.GetExecutingAssembly().Location).DirectoryName!;
            string LWPathCacheFile = Path.Combine(ourFolder, ".lwInstallPath");

            Console.WriteLine("Checking Logicworld install path...");
            var lwInstallPath = File.Exists(LWPathCacheFile) ? File.ReadAllText(LWPathCacheFile) : null;

            bool pathChanged = false;
            bool pathok = false;
            do
            {
                if (string.IsNullOrEmpty(lwInstallPath))
                {
                    Console.WriteLine("Looks like the Logicworld install path hasnt been set yet.");
                }
                else if (!Directory.Exists(lwInstallPath))
                {
                    Console.WriteLine("Looks like the cached path no longer exists.");
                }
                else if (!Directory.Exists(Path.Combine(lwInstallPath, "Logic_World_Data")))
                {
                    Console.WriteLine("The Logic_World_Data folder cannot be found inside the given install dir");
                }
                else if (!Directory.Exists(Path.Combine(lwInstallPath, "Server")))
                {
                    Console.WriteLine("The Server folder cannot be found inside the given install dir");
                }
                else
                {
                    pathok = true;
                }


                if (!pathok)
                {
                    Console.WriteLine("Please enter the path to the topmost Logicworld directory and then press ENTER. (The folder that contains the Logic_World.exe file along with subfolders such as Logic_World_Data):");
                    lwInstallPath = Console.ReadLine()?.Trim() ?? "";
                    pathChanged = true;
                }
            } while (!pathok);

            if (pathChanged)
                File.WriteAllText(LWPathCacheFile, lwInstallPath);

            Console.WriteLine("Install path OK.");

            Console.WriteLine("Do you want to decompile the LW-server and LW-client dlls? [Y/n]");

            var input = Console.ReadLine()?.Trim()?.ToLowerInvariant() ?? "n";
            if (input != "" && input != "y")
            {
                Console.WriteLine("Decompilation aborted by user.");
                return;
            }

            var serverSubpath = "Server";
            var clientSubpath = Path.Combine("Logic_World_Data", "Managed");

            Console.WriteLine("\nDecompiling server...");

            var decompRootFolder = Path.Combine(ourFolder, "decompiled");

            var serverDecompKeywords = "SUCC;Jimmy;LICC;Lidgren;Logic;TypeFinder;SECCS".Split(";");
            var serverDecompExplicitNames = "Server.dll".Split(";");
            DecompDirectory(Path.Combine(decompRootFolder, "LWServer"), Path.Combine(lwInstallPath, serverSubpath), files => files.Where(f =>
                f.EndsWith(".dll") && (serverDecompExplicitNames.Contains(f) || serverDecompKeywords.Any(keyword => f.Contains(keyword)))));


            Console.WriteLine("\nDecompiling client...");
            var clientDecompKeywords = "SUCC;Jimmy;LICC;Lidgren;Logic;TypeFinder;SECCS;FancyInput;FancyPantsConsole;GameDataAccess".Split(";");
            var clientDecompExplicitNames = "KnifeOutline.dll".Split(";");
            DecompDirectory(Path.Combine(decompRootFolder, "LWClient"), Path.Combine(lwInstallPath, clientSubpath), files => files.Where(f =>
                f.EndsWith(".dll") && (clientDecompExplicitNames.Contains(f) || clientDecompKeywords.Any(keyword => f.Contains(keyword)))));

            Console.WriteLine("Finished successfully.");
        }

        private static void DecompDirectory(string outputFolder, string dllSourceFolder, Func<string[], IEnumerable<string>> decompFileNameSelector)
        {
            var totalSw = new Stopwatch();
            totalSw.Start();

            if (Directory.Exists(outputFolder))
                Directory.Delete(outputFolder, true);
            Directory.CreateDirectory(outputFolder);

            var allFiles = Directory.GetFiles(dllSourceFolder, "*", SearchOption.TopDirectoryOnly).Select(x => x.Split(Path.DirectorySeparatorChar).Last()).ToArray();
            var filesToDecomp = decompFileNameSelector.Invoke(allFiles).Where(x => x.EndsWith(".dll")).Select(x => Path.Combine(dllSourceFolder, x));

            var ds = new DecompilerSettings(LanguageVersion.Latest)
            {

            };
            var decSw = new Stopwatch();
            foreach (var f in filesToDecomp)
            {
                decSw.Restart();
                var decDllName = f.Split(Path.DirectorySeparatorChar).Last();

                Console.Write($"Decompiling {decDllName}... ");

                var decProjName = decDllName.Replace(".dll", "") + ".csproj";

                var module = new PEFile(f);
                var assemblyResolver = new UniversalAssemblyResolver(f, true, module.DetectTargetFrameworkId());
                var cd = new WholeProjectDecompiler(ds, assemblyResolver, assemblyResolver, null); // maybe add PDB provider
                
                var decDllName_NoExt = decDllName.Replace(".dll", "");
                var decOutputFolder = Path.Combine(outputFolder, decDllName_NoExt);
                Directory.CreateDirectory(decOutputFolder);
                var projectFileName = Path.Combine(decOutputFolder, decProjName);
                using (var projectFileWriter = new StreamWriter(File.OpenWrite(projectFileName)))
                {
                    try
                    {
                        var decProjectId = cd.DecompileProject(module, Path.GetDirectoryName(projectFileName), projectFileWriter);
                        decSw.Stop();
                    }
                    catch
                    {
                        Console.WriteLine("error!");
                        throw;
                    }
                    finally
                    {
                        Console.WriteLine($"done in {decSw.ElapsedMilliseconds}ms!");
                    }
                }

                ReplaceDotInPathWithSlash(decOutputFolder);

                void ReplaceDotInPathWithSlash(string rootFolder)
                {
                    foreach (var folder in Directory.GetDirectories(rootFolder, "*", SearchOption.TopDirectoryOnly))
                    {
                        var folderParent = Path.Join(folder.Split(Path.DirectorySeparatorChar).SkipLast(1).ToArray());
                        var folderName = folder.Split(Path.DirectorySeparatorChar).Last();
                        if (folderName.Contains("."))
                        {
                            //var splitted = folderName.Split(".");

                            //var lastFolderName = splitted.Last();
                            //var notLastFolderPath = Path.Combine(splitted.SkipLast(1).ToArray());
                            var destFolder = Path.Combine(rootFolder, folderName.Replace('.', Path.DirectorySeparatorChar));
                            Directory.CreateDirectory(string.Join(Path.DirectorySeparatorChar, destFolder.Split(Path.DirectorySeparatorChar).SkipLast(1)));
                            Directory.Move(Path.Combine(rootFolder,folderName), destFolder);
                        }
                    }

                    foreach (var folder in Directory.GetDirectories(rootFolder, "*", SearchOption.TopDirectoryOnly))
                    {
                        ReplaceDotInPathWithSlash(folder);
                    }
                }

                var references = module.AssemblyReferences.Select(x => x.Name).ToArray();
                var jcontent = JsonConvert.SerializeObject(new UnityAsmDef(decDllName_NoExt, references, true), Formatting.Indented);
                File.WriteAllText(Path.Combine(decOutputFolder, $"{decDllName_NoExt}.asmdef"), jcontent);
            }
            totalSw.Stop();
            Console.WriteLine($"Decompilation done in {totalSw.ElapsedMilliseconds}ms!");
        }
    }

    class UnityAsmDef
    {
        public UnityAsmDef(string name, string[] references, bool allowUnsafeCode)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            References = references ?? throw new ArgumentNullException(nameof(references));
            AllowUnsafeCode = allowUnsafeCode;
        }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("references")]
        public string[] References { get; set; }

        [JsonProperty("allowUnsafeCode")]
        public bool AllowUnsafeCode { get; set; }
    }
}