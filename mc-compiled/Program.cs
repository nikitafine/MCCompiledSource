﻿using mc_compiled.Commands.Native;
using mc_compiled.Json;
using mc_compiled.MCC;
using mc_compiled.MCC.Compiler;
using mc_compiled.Modding;
using mc_compiled.NBT;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace mc_compiled
{
    class Program
    {
        public const string APP_ID = "Microsoft.MinecraftUWP_8wekyb3d8bbwe";
        public static bool NO_PAUSE = false;
        public static bool DECORATE = false;
        public static bool DEBUG = false;
        public static bool CLEAN = false;
        public static bool REGOLITH = false;
        static void Help()
        {
            Console.Write("\nmc-compiled.exe --help\n");
            Console.Write("\tShow the help menu for this application.\n\n");
            Console.Write("mc-compiled.exe --jsonbuilder\n");
            Console.Write("\tOpen a user-interface to build JSON rawtext.\n\n");
            Console.Write("mc-compiled.exe --manifest <projectName>\n");
            Console.Write("\tGenerate a behavior pack manifest with valid GUIDs.\n\n");
            Console.Write("mc-compiled.exe --search [options...]\n");
            Console.Write("\tSearch for MCC files in all subdirectories.\n\n");
            Console.Write("mc-compiled.exe <file> [options...]\n");
            Console.Write("\tCompile a .mcc file into the resulting .mcfunction files.\n\n");
            Console.Write("\tOptions:\n");
            Console.Write("\t  -dm | --daemon\tInitialize to allow background compilation of the same file every time it is modified.\n");
            Console.Write("\t  -db | --debug\t\tDebug information during compilation.\n");
            Console.Write("\t  -dc | --decorate\tDecorate the compiled file with original source code (doesn't look great).\n");
            Console.Write("\t  -np | --nopause\tDoes not wait for user input to close application.\n");
            Console.Write("\t  [-obp | --outputbp] <directory>\tOutput behaviors to a specific directory. Use ?project to denote file name.\n");
            Console.Write("\t  [-orp | --outputrp] <directory>\tOutput resources to a specific directory. Use ?project to denote file name.\n");
            Console.Write("\t  -od | --outputdevelopment\tOutput files to the com.mojang development_x_packs directory.\n");
        }
        [STAThread]
        static void Main(string[] args)
        {
            if(args.Length < 1 || args[0].Equals("--help"))
            {
                Help();
                return;
            }

            string[] files = new string[] { args[0] };
            bool debug = false;
            bool search = false;
            bool daemon = false;
            string obp = "?project\\BP";
            string orp = "?project\\RP";

            for (int i = 0; i < args.Length; i++)
            {
                string word = args[i].ToUpper();
                switch(word)
                {
                    case "--DEBUG":
                    case "-DB":
                        debug = true;
                        break;
                    case "--NOPAUSE":
                    case "-NP":
                        NO_PAUSE = true;
                        break;
                    case "--DECORATE":
                    case "-DC":
                        DECORATE = true;
                        break;
                    case "--DAEMON":
                    case "-DM":
                        daemon = true;
                        NO_PAUSE = true;
                        break;
                    case "-OUTPUTBP":
                    case "-OBP":
                        obp = args[++i];
                        break;
                    case "-OUTPUTRP":
                    case "-ORP":
                        orp = args[++i];
                        break;
                    case "-OUTPUTDEVELOPMENT":
                    case "-OD":
                        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                        string comMojang = Path.Combine(localAppData, "Packages", APP_ID, "LocalState", "games", "com.mojang");
                        obp = Path.Combine(comMojang, "development_behavior_packs") + "\\?project";
                        orp = Path.Combine(comMojang, "development_resource_packs") + "\\?project";
                        break;
                }
            }

            string fileUpper = files[0].ToUpper();
            if (fileUpper.Equals("--JSONBUILDER"))
            {
                new Definitions(debug);
                RawTextJsonBuilder builder = new RawTextJsonBuilder();
                builder.ConsoleInterface();
                return;
            }
            if (fileUpper.Equals("--TESTITEMS"))
            {
                new Definitions(debug);
                ItemStack item = new ItemStack()
                {
                    id = "minecraft:stick",
                    count = 1,
                    displayName = "§bSuper Stick",
                    lore = new string[]
                    {
                        "This is the super stick.",
                        "§lHe knok bak"
                    },
                    enchantments = new EnchantmentEntry[]
                    {
                        new EnchantmentEntry(Commands.Enchantment.knockback, 50)
                    }
                };
                StructureNBT nbt = StructureNBT.SingleItem(item);
                StructureFile itemFile = new StructureFile("stick", nbt);
                File.WriteAllBytes("testitem0.mcstructure", itemFile.GetOutputData());

                item = new ItemStack()
                {
                    id = "minecraft:written_book",
                    count = 1,
                    bookData = new ItemTagBookData()
                    {
                        author = "lukecreator",
                        title = "The MCCompiled Wiki",
                        pages = new string[]
                        {
                            "AAAAAPErheikuhrfewughreeeee\n\n-end of page 1",
                            "WHWWwhoooahhhhahhh mc compiled whw\n\n-end of page 2",
                            "The End"
                        }
                    }
                };
                nbt = StructureNBT.SingleItem(item);
                itemFile = new StructureFile("testitem", nbt);
                File.WriteAllBytes("testitem1.mcstructure", itemFile.GetOutputData());

                item = new ItemStack()
                {
                    id = "minecraft:leather_chestplate",
                    count = 1,
                    enchantments = new EnchantmentEntry[]
                    {
                        new EnchantmentEntry(Commands.Enchantment.protection, 20)
                    },
                    customColor = new ItemTagCustomColor()
                    {
                        r = 255,
                        g = 0,
                        b = 0
                    }
                };
                nbt = StructureNBT.SingleItem(item);
                itemFile = new StructureFile("testitem", nbt);
                File.WriteAllBytes("testitem2.mcstructure", itemFile.GetOutputData());

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("Written test items to 'testitemX.mcstructure'");
                Console.ForegroundColor = ConsoleColor.White;
                return;
            }
            if (fileUpper.Equals("--TESTLOOT"))
            {
                new Definitions(debug);
                LootTable table = new LootTable("test");
                table.pools.Add(new LootPool(6, new LootEntry[]
                {
                    new LootEntry(LootEntry.EntryType.item, "minecraft:iron_sword")
                        .WithFunction(new LootFunctionEnchant(new EnchantmentEntry("sharpness", 20)))
                        .WithFunction(new LootFunctionDurability(0.5f))
                        .WithFunction(new LootFunctionName("§lSuper Sword"))
                        .WithFunction(new LootFunctionLore(
                            "§cHi! This is a line of lore.",
                            "§6Here's another line.")),
                    new LootEntry(LootEntry.EntryType.item, "minecraft:book")
                        .WithFunction(new LootFunctionBook("Test Book", "lukecreator",
                            "yo welcome to the first page!\nSecond line.",
                            "Second page!")),
                    new LootEntry(LootEntry.EntryType.item, "minecraft:leather_chestplate")
                        .WithFunction(new LootFunctionName("Random Enchant"))
                        .WithFunction(new LootFunctionRandomEnchant(true))
                        .WithFunction(new LootFunctionRandomDye()),
                    new LootEntry(LootEntry.EntryType.item, "minecraft:leather_leggings")
                        .WithFunction(new LootFunctionName("Simulated Enchant"))
                        .WithFunction(new LootFunctionSimulateEnchant(20, 40)),
                    new LootEntry(LootEntry.EntryType.item, "minecraft:leather_boots")
                        .WithFunction(new LootFunctionName("Gear Enchant"))
                        .WithFunction(new LootFunctionRandomEnchantGear(1.0f)),
                    new LootEntry(LootEntry.EntryType.item, "minecraft:cooked_beef")
                        .WithFunction(new LootFunctionCount(2, 64))
                }));

                File.WriteAllBytes("testloot.json", table.GetOutputData());
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("Written test table to 'testloot.json'");
                Console.ForegroundColor = ConsoleColor.White;
                return;
            }
            if (fileUpper.Equals("--MANIFEST"))
            {
                string rest = string.Join(" ", args).Substring(11);
                Manifest manifest = new Manifest(OutputLocation.b_ROOT, Guid.NewGuid(), rest, "TODO set description")
                    .WithModule(Manifest.Module.BehaviorData(rest));
                File.WriteAllBytes("manifest.json", manifest.GetOutputData());
                Console.WriteLine("Wrote a new 'manifest.json' to current directory.");
                return;
            }

            if (fileUpper.Equals("--SEARCH"))
            {
                REGOLITH = false;
                search = true;
                NO_PAUSE = true;
                files = Directory.GetFiles(Directory.GetCurrentDirectory(), "*.mcc", SearchOption.AllDirectories);

                if (files.Length == 0)
                {
                    Console.WriteLine("No MCC files found.");
                    return;
                }
            }
            if (fileUpper.Equals("--REGOLITH"))
            {
                obp = "BP";
                orp = "RP";
                REGOLITH = true;
                search = true;
                NO_PAUSE = true;
                files = Directory.GetFiles(Directory.GetCurrentDirectory(), "*.mcc", SearchOption.AllDirectories);

                if (files.Length == 0)
                {
                    Console.WriteLine("No MCC files found. Skipping filter.");
                    return;
                }
            }

            if (debug)
            {
                DEBUG = true;
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("Debug Enabled");
                Console.ForegroundColor = ConsoleColor.White;
            }
            if (daemon & !REGOLITH)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                if(search)
                    Console.WriteLine($"[daemon] watching directory: {Directory.GetCurrentDirectory()}");
                else
                    Console.WriteLine($"[daemon] watching file: {files}");
                Console.ForegroundColor = ConsoleColor.White;
            }

            // initialize definitions resolver
            new Definitions(debug);
            // initialize enum constants
            Commands.CommandEnumParser.Init();

            bool firstRun = true;
            if (daemon & !REGOLITH)
            {
                FileSystemWatcher watcher;
                if (search)
                {
                    watcher = new FileSystemWatcher(Directory.GetCurrentDirectory());
                    watcher.NotifyFilter = NotifyFilters.LastWrite;
                    watcher.Filter = "*.mcc";
                } else
                {
                    watcher = new FileSystemWatcher(Directory.GetCurrentDirectory());
                    watcher.NotifyFilter = NotifyFilters.LastWrite;
                    watcher.Filter = $"{Path.GetFileNameWithoutExtension(files[0])}.mcc";
                }

                Console.TreatControlCAsInput = true;
                string changedFile = null;

                while(true)
                {
                    if (firstRun)
                    {
                        PrepareToCompile();
                        firstRun = false;
                        foreach (string file in files)
                        {
                            CleanDirectory(obp, file);
                            CleanDirectory(orp, file);
                            RunMCCompiled(file, obp, orp);
                        }
                    }
                    else
                    {
                        Console.Clear();
                        PrepareToCompile();
                        CleanDirectory(obp, changedFile);
                        CleanDirectory(orp, changedFile);
                        RunMCCompiled(changedFile, obp, orp);
                    }

                    ConsoleColor oldColor = Console.ForegroundColor;
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("[daemon] listening for next update...");
                    Console.ForegroundColor = oldColor;
                    while (true)
                    {
                        var e = watcher.WaitForChanged(WatcherChangeTypes.Changed, 500);

                        // flush stdin
                        while (Console.KeyAvailable)
                        {
                            ConsoleKeyInfo key = Console.ReadKey();
                            if (key.Modifiers == ConsoleModifiers.Control &&
                                key.Key == ConsoleKey.C)
                                goto compileEnd;
                        }

                        if (e.TimedOut)
                            continue;
                        else
                        {
                            changedFile = e.Name;
                            break;
                        }
                    }
                    System.Threading.Thread.Sleep(100);
                }
            compileEnd:
                watcher.Dispose();
                return;
            }

            PrepareToCompile();
            bool silent = REGOLITH;

            if(REGOLITH)
            {
                foreach (string file in files)
                    if (RunMCCompiled(file, obp, orp, silent))
                        File.Delete(file); // delete if compilation succeeded, otherwise might be another format
            } else
            {
                foreach (string file in files)
                {
                    CleanDirectory(obp, file);
                    CleanDirectory(orp, file);
                    RunMCCompiled(file, obp, orp, silent);
                }
            }
        }
        public static void PrepareToCompile()
        {
            // reset all that icky static stuff
            Executor.ResetGeneratedFiles();
            Commands.Command.ResetState();
            Tokenizer.CURRENT_LINE = 0;
            DirectiveImplementations.ResetState();
        }
        public static void CleanDirectory(string cleanFolder, string file)
        {
            cleanFolder = cleanFolder.Replace("?project", Path.GetFileNameWithoutExtension(file));

            if (Directory.Exists(cleanFolder))
            {
                List<string> files = new List<string>();
                files.AddRange(Directory.GetFiles(cleanFolder, "*.mcstructure", SearchOption.AllDirectories));
                files.AddRange(Directory.GetFiles(cleanFolder, "*.mcfunction", SearchOption.AllDirectories));

                foreach (string del in files)
                    File.Delete(del);
            }
        }
        /// <summary>
        /// Compile a file with MCCompiled using the existing options.
        /// </summary>
        /// <param name="file">The file to compile.</param>
        /// <param name="outputBP">The root location that the BP content will be written to.</param>
        /// <param name="outputRP">The root location that the RP content will be written to.</param>
        /// <param name="silentErrors">Whether to silently throw away errors.</param>
        /// <returns>If the compilation succeeded.</returns>
        public static bool RunMCCompiled(string file, string outputBP, string outputRP, bool silentErrors = false)
        {
            string project = Path.GetFileNameWithoutExtension(file);
            outputBP = outputBP.Replace("?project", project);
            outputRP = outputRP.Replace("?project", project);

            bool hasBehaviorManifest = File.Exists(Path.Combine(outputBP, "manifest.json"));
            bool hasResourceManifest = File.Exists(Path.Combine(outputRP, "manifest.json"));

            try
            {
                Stopwatch stopwatch = Stopwatch.StartNew();
                Token[] tokens = Tokenizer.TokenizeFile(file);

                if (DEBUG)
                {
                    Console.WriteLine("\tA detailed overview of the tokenization results follows:");
                    Console.WriteLine(string.Join("", from t in tokens select t.DebugString()));
                    Console.WriteLine();
                    Console.WriteLine("\tReconstruction of the processed code through tokens:");
                    Console.WriteLine(string.Join(" ", from t in tokens select t.AsString()));
                    Console.WriteLine();
                }

                Statement[] statements = Assembler.AssembleTokens(tokens);

                if (DEBUG)
                {
                    Console.WriteLine("\tThe overview of assembled statements is as follows:");
                    Console.WriteLine(string.Join("\n", from s in statements select s.ToString()));
                    Console.WriteLine();
                }

                Executor executor = new Executor(statements, project, outputBP, outputRP);
                executor.Execute();

                Console.WriteLine("Writing files...");
                executor.project.WriteAllFiles();
                stopwatch.Stop();

                Console.WriteLine($"Completed in {stopwatch.Elapsed.TotalSeconds} seconds.");

                if (!NO_PAUSE)
                    Console.ReadLine();

                return true;
            }
            catch (TokenizerException exc)
            {
                if (silentErrors)
                    return false;

                int _line = exc.line;
                string line = _line == -1 ? "??" : _line.ToString();
                string message = exc.Message;
                ConsoleColor oldColor = Console.ForegroundColor;
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Problem encountered during tokenization of file:\n" +
                    $"\t{Path.GetFileName(file)}:{line} -- {message}\n\nTokenization cannot be continued.");
                Console.ForegroundColor = oldColor;
                if (!NO_PAUSE)
                    Console.ReadLine();
                return false;
            }
            catch (StatementException exc)
            {
                if (silentErrors)
                    return false;

                Statement thrower = exc.statement;
                string message = exc.Message;
                int _line = thrower.Line;
                string line = _line == -1 ? "??" : _line.ToString();

                ConsoleColor oldColor = Console.ForegroundColor;
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("An error has occurred during compilation:\n" +
                    $"\t{Path.GetFileName(file)}:{line} -- {thrower.ToString()}:\n\t\t{message}\n\nCompilation cannot be continued.");
                Console.ForegroundColor = oldColor;
                if(!NO_PAUSE)
                    Console.ReadLine();
                return false;
            }
        }
    }
}