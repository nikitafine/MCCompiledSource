﻿using mc_compiled.Commands;
using mc_compiled.Commands.Native;
using mc_compiled.MCC.Compiler;
using mc_compiled.Modding;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace mc_compiled.MCC
{
    /// <summary>
    /// (old) Runs a set of compiled tokens.
    /// </summary>
    public class LegacyExecutor
    {
        public const float MCC_VERSION = 0.60f;             // _compilerversion
        public static string MINECRAFT_VERSION = "1.17.41"; // _mcversion
        public long HaltFunctionCount
        {
            get
            {
                if(ppv.TryGetValue("functionCommandLimit", out LegacyDynamic d))
                {
                    return d.data.i;
                }
                return 10000L;
            }
        }

        // Command Related
        public const string MATH_TEMP = "_mcc_math";            // Used for multistep scoreboard operations.
        public const string MATH_TEMP2 = "_mcc_math2";          // Used for multistep scoreboard operations.
        public const string MATH_INVERTER = "_mcc_invert";      // Used for inverting block check results.
        public const string DECIMAL_UNIT = "_mcc_dec_unit";     // Unit for fixed-point decimal operations.
        public const string DECIMAL_SUB_CARRY = "dec_carry_";   // Prefix used for decimal subtraction carry functions.
        public const string SCATTER_RAND = "_mcc_scatter";      // Random number for  
        public const string GHOST_TAG = "_gst";                 // Used for ghost armor stands.
        public const string HALT_FUNCTION = "halt_execution";   // Function that halts execution.

        private readonly List<string> createdTemplates = new List<string>();
        public bool HasCreatedTemplate(string templateName) => createdTemplates.Contains(templateName);
        public void CreateTemplate(string name, string[] code, bool file = false)
        {
            if (HasCreatedTemplate(name))
                return;

            createdTemplates.Add(name);

            if (!file)
            {
                for (int i = code.Length - 1; i >= 0; i--)
                    AddLineTop(code[i]);
            }
            else
            {
                List<string> nfile = new List<string>(code);
                functionsToBeWritten.Add(new LegacyMCFunction(name, null, nfile));
            }
        }

        public int currentMacroHash = 0;
        /// <summary>
        /// Describes if the executor is currently located at
        /// the base of the scope hierarchy, meaning functions
        /// should be prefixed with "baseFileName-"
        /// </summary>
        public bool AtBaseScope
        {
            get { return fileStack.Count <= 1; }
        }
        /// <summary>
        /// The current scope, or depth of this executor.
        /// </summary>
        public int CurrentFunctionScope
        {
            get { return fileStack.Count - 1; }
        }

        public readonly bool debug;
        public readonly bool decorate;
        public readonly Dictionary<string, LegacyDynamic> ppv;
        public readonly Dictionary<string, LegacyMacro> macros;
        public readonly LegacyTokenFeeder tokens;

        public LegacyValueManager values;
        public Selector.Core selection;
        

        int _reader;
        public int ReaderLocation
        {
            get { return _reader; }
            private set { _reader = value; }
        }
        public bool TargetPositionAligned
        {
            get
            {
                return CurrentFunctionScope > 0;
            }
        }
        /// <summary>
        /// Context-aware selector for the currently selected entity.
        /// Compensates for if-statements changing @s.
        /// </summary>
        public Selector.Core SelectionReference
        {
            get
            {
                if(TargetPositionAligned)
                {
                    if (selection == Selector.Core.e || selection == Selector.Core.a)
                        return Selector.Core.s;
                }
                return selection;
            }
        }

        internal List<LegacyMCFunction> functionsToBeWritten = new List<LegacyMCFunction>();
        internal List<Tuple<string, ItemStack>> itemsToBeWritten = new List<Tuple<string, ItemStack>>();
        public List<LegacyFunctionDefinition> functionsDefined = new List<LegacyFunctionDefinition>();

        public string projectName = "DefaultProject";
        public string projectDesc = "Default project description.";
        public string baseFileName;     // The base file name for all the functions.
        Stack<LegacyFileWriter> fileStack; // Files the executor will write to.

        /// <summary>
        /// Finish the current line and append it to the current file.
        /// </summary>
        /// <param name="line"></param>
        public void FinishRaw(string line, bool modifyBuffer = true)
        {
            LegacyFileWriter writer = fileStack.Peek();
            writer.ApplyBuffer(line, modifyBuffer);
        }
        /// <summary>
        /// Sets the text in the current line but doesn't finish it.
        /// </summary>
        /// <param name="text"></param>
        public void SetRaw(string text)
        {
            fileStack.Peek().SetBuffer(text);
        }
        /// <summary>
        /// Add a line to the top of the file.
        /// </summary>
        /// <param name="line"></param>
        public void AddLineTop(string line)
        {
            fileStack.Peek().InsertLine(0, line);
        }

        /// <summary>
        /// Push a new file to the top of the stack to be written to.
        /// </summary>
        /// <param name="fileName"></param>
        /// <param name="fileFolder"></param>
        /// <returns></returns>
        public LegacyFileWriter PushFile(string fileName, string fileFolder = null)
        {
            if (AtBaseScope && fileFolder == null)
                fileName = baseFileName + '-' + fileName;

            LegacyFileWriter toPush = new LegacyFileWriter(fileName, fileFolder);
            fileStack.Push(toPush);
            return toPush;
        }
        /// <summary>
        /// Pop a file off the stack and add it to the functions-to-be-written list.
        /// </summary>
        public void PopFile()
        {
            LegacyFileWriter apply = fileStack.Pop();
            LegacyMCFunction final = apply.Finalize();
            functionsToBeWritten.Add(final);
        }

        /// <summary>
        /// Replace preprocessor variables in a piece code with their respective values.
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public string ReplacePPV(string input)
        {
            var all = ppv.AsEnumerable();

            foreach (var entry in all)
                input = input.Replace('$' + entry.Key, entry.Value.data.s);
            return input;
        }
        public bool HasPPV(string name)
        {
            if (name.StartsWith("$"))
                name = name.Substring(1);
            return ppv.ContainsKey(name);
        }
        public bool TryGetPPV(string name, out LegacyDynamic value)
        {
            if (name.StartsWith("$"))
                name = name.Substring(1);
            return ppv.TryGetValue(name, out value);
        }
        public LegacyExecutor(LegacyToken[] tokens, bool debug, bool decorate, string baseFileName)
        {
            this.debug = debug;
            this.decorate = decorate;
            this.baseFileName = baseFileName;
            projectName = baseFileName;
            fileStack = new Stack<LegacyFileWriter>();
            fileStack.Push(new LegacyFileWriter(baseFileName));
            macros = new Dictionary<string, LegacyMacro>();

            ppv = new Dictionary<string, LegacyDynamic>();
            selection = Selector.Core.s;
            values = new LegacyValueManager();

            this.tokens = new LegacyTokenFeeder(tokens);

            ppv["_compilerversion"] = new LegacyDynamic(MCC_VERSION);
            ppv["_mcversion"] = new LegacyDynamic(MCC_VERSION);
            ppv["_lines"] = new LegacyDynamic(tokens.Length);
        }

        /// <summary>
        /// Run the entire file from start to finish.
        /// </summary>
        public void Run()
        {
            RunSection(tokens);
            PopFile();
        }
        /// <summary>
        /// Run a set of tokens.
        /// </summary>
        /// <param name="tokens"></param>
        public void RunSection(LegacyTokenFeeder tokens)
        {
            LegacyToken token = null;
            try
            {
                while(tokens.HasNext())
                {
                    token = tokens.Next();
                    token.Execute(this, tokens);
                }
            } catch(LegacyTokenException texc)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Managed Exception:\n" +
                    $"\tLine Number: {texc.token.line}\n" +
                    $"\tLine Code: {texc.token}\n" +
                    $"\tMessage:\n\t\t{texc.desc}\n");
                Console.ReadLine();
                Environment.Exit(0);
                return;
            }/* catch(Exception exc)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Unmanaged Exception:\n" +
                    $"\tLine Code: {token}\n" +
                    $"\tMessage: {exc.Message}\n\n" +
                    $"\tFor the Developers:\n{exc}");
                Console.ReadLine();
                throw exc;
            }*/
        }
        /// <summary>
        /// Get the compiled files after execution.
        /// </summary>
        /// <returns></returns>
        public LegacyMCFunction[] GetFiles()
        {
            if (functionsToBeWritten == null || functionsToBeWritten.Count < 1)
                return new LegacyMCFunction[0];

            return functionsToBeWritten.ToArray();
        }
        /// <summary>
        /// Get this executor's results as a proper BehaviorPack.
        /// </summary>
        /// <returns></returns>
        public BehaviorPack GetAsPack()
        {
            BehaviorPack pack = new BehaviorPack()
            {
                packName = projectName,
                manifest = new Manifest(Guid.NewGuid(), 
                    Guid.NewGuid(), projectName, projectDesc),
                functions = GetFiles(),
                structures = null // Support not implemented yet
            };

            return pack;
        }
        /// <summary>
        /// Get the custom item drops defined during execution.
        /// </summary>
        /// <returns></returns>
        public Tuple<string, ItemStack>[] GetItemDefinitions()
        {
            if (itemsToBeWritten == null || itemsToBeWritten.Count < 1)
                return new Tuple<string, ItemStack>[0];

            return itemsToBeWritten.ToArray();
        }
    }
}