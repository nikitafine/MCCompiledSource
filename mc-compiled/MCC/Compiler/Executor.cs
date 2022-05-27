﻿using mc_compiled.Commands;
using mc_compiled.Json;
using mc_compiled.Modding;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace mc_compiled.MCC.Compiler
{
    /// <summary>
    /// The final stage of the compilation process. Runs statements and holds state on 
    /// </summary>
    public class Executor
    {
        public const string FSTRING_REGEX = "({([a-zA-Z0-9-:._]{1,16})})|({(@[psea](\\[.+\\])?)})";
        public static readonly Regex FSTRING_FMT = new Regex(FSTRING_REGEX);
        public static readonly Regex FSTRING_FMT_SPLIT = new Regex(FSTRING_REGEX, RegexOptions.ExplicitCapture);
        public const float MCC_VERSION = 1.02f;              // compilerversion
        public static string MINECRAFT_VERSION = "x.xx.xxx"; // mcversion
        public const string MCC_GENERATED_FOLDER = "compiler"; // folder that generated functions go into

        /// <summary>
        /// Display a success message regardless of debug setting.
        /// </summary>
        /// <param name="message">The warning to display.</param>
        public static void Good(string message)
        {
            ConsoleColor old = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine(message);
            Console.ForegroundColor = old;
        }
        /// <summary>
        /// Display a warning regardless of debug setting.
        /// </summary>
        /// <param name="warning">The warning to display.</param>
        public static void Warn(string warning, Statement source = null)
        {
            ConsoleColor old;

            if (source == null)
            {
                old = Console.ForegroundColor;
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("<!> {0}", warning);
                Console.ForegroundColor = old;
                return;
            }

            old = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("<L{0}> {1}", source.Line, warning);
            Console.ForegroundColor = old;
        }

        internal readonly EntityManager entities;
        internal readonly ProjectManager project;
        public string lastStatementSource;

        Statement[] statements;
        int readIndex = 0;
        int unreachableCode = -1;

        readonly Dictionary<int, object> loadedFiles;
        readonly List<int> definedStdFiles;
        readonly List<Macro> macros;
        readonly List<Function> functions;
        readonly bool[] lastPreprocessorCompare;
        readonly Token[][] lastActualCompare;
        readonly Dictionary<string, dynamic[]> ppv;
        readonly StringBuilder prependBuffer;
        readonly Stack<CommandFile> currentFiles;
        readonly Stack<Selector> selections;
        readonly Stack<StructDefinition> definingStructs;
        public readonly ScoreboardManager scoreboard;
        
        /// <summary>
        /// Resolve an FString into rawtext terms. Also adds all setup commands for variables.
        /// </summary>
        /// <param name="fstring"></param>
        /// <returns></returns>
        public List<JSONRawTerm> FString(string fstring, out bool advanced)
        {
            MatchCollection matches = FSTRING_FMT.Matches(fstring);

            advanced = false;
            if (matches.Count < 1)
                return new List<JSONRawTerm>() { new JSONText(fstring) };

            List<JSONRawTerm> terms = new List<JSONRawTerm>();
            IEnumerable<string> piecesReversed = FSTRING_FMT_SPLIT.Split(fstring).Reverse();
            Stack<string> pieces = new Stack<string>(piecesReversed);

            int index = 0;
            foreach (Match match in matches)
            {
                int mindex = match.Index;
                if (mindex != 0 && pieces.Count > 0)
                    terms.Add(new JSONText(pieces.Pop()));
                else
                    pieces.Pop();

                string src = match.Value;
                string varAccessor = match.Groups[2].Value;
                string selector = match.Groups[4].Value.Trim('{', '}');

                if (!string.IsNullOrEmpty(varAccessor))
                {
                    if (scoreboard.TryGetByAccessor(varAccessor, out ScoreboardValue value, true))
                    {
                        advanced = true;
                        // only allow one of them to increment the actual count
                        int indexCopy = index;
                        AddCommandsClean(value.CommandsRawTextSetup(varAccessor, "@s", ref indexCopy), "string" + value.baseName);
                        terms.AddRange(value.ToRawText(varAccessor, "@s", ref index));
                        index++;
                    }
                    else
                        terms.Add(new JSONText(src));
                }
                else if (!string.IsNullOrEmpty(selector))
                {
                    advanced = true;
                    terms.Add(new JSONSelector(selector));
                }
                else
                    terms.Add(new JSONText(src));
            }

            while (pieces.Count > 0)
            {
                string text = pieces.Pop();
                if(!string.IsNullOrEmpty(text))
                    terms.Add(new JSONText(text));
            }

            return terms;
        }
        /// <summary>
        /// Append these terms to the end of this command. Will resolve JSONVariant's and construct the command combinations.
        /// </summary>
        /// <param name="terms">The terms constructed by FString.</param>
        /// <param name="command">The command to append the terms to.</param>
        /// <param name="root">If this is the root call.</param>
        /// <param name="currentSelector">The current selector that holds all the scores checks. Set to null for default behavior.</param>
        /// <param name="commands">Used for recursion, set to null.</param>
        /// <param name="copy">The existing terms to copy from.</param>
        /// <returns></returns>
        public string[] ResolveRawText(List<JSONRawTerm> terms, string command, bool root = true,
            Selector currentSelector = null, List<string> commands = null, RawTextJsonBuilder copy = null)
        {
            RawTextJsonBuilder jb = new RawTextJsonBuilder(copy);

            if (currentSelector == null)
                currentSelector = new Selector(Selector.Core.s);
            if(commands == null)
                commands = new List<string>();

            for(int i = 0; i < terms.Count; i++)
            {
                JSONRawTerm term = terms[i];
                if (term is JSONVariant)
                {
                    // calculate both variants
                    JSONVariant variant = term as JSONVariant;
                    Selector checkA = variant.ConstructSelectorA(currentSelector);
                    Selector checkB = variant.ConstructSelectorB(currentSelector);
                    List<JSONRawTerm> restA = terms.Skip(i + 1).ToList();
                    List<JSONRawTerm> restB = terms.Skip(i + 1).ToList();
                    restA.InsertRange(0, variant.a);
                    restB.InsertRange(0, variant.b);
                    ResolveRawText(restA, command, false, checkA, commands, jb);
                    ResolveRawText(restB, command, false, checkB, commands, jb);
                    break;
                }
                else
                    jb.AddTerm(term);
            }

            bool hasVariant = terms.Any(t => t is JSONVariant);
            if (!root && !hasVariant)
                commands.Add(Command.Execute(currentSelector.ToString(),
                    Coord.here, Coord.here, Coord.here, command + jb.BuildString()));
            else if(root && !hasVariant)
                commands.Add(command + jb.BuildString());

            if (root)
                return commands.ToArray();

            return null; // return value isn't used in this case
        }
        public void UnreachableCode() =>
            unreachableCode = 1;
        /// <summary>
        /// Throw a StatementException if a feature is not enabled.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="feature"></param>
        internal void RequireFeature(Statement source, Feature feature)
        {
            if (project.HasFeature(feature))
                return;

            string name = feature.ToString();
            throw new StatementException(source, $"Feature not enabled: {name}. Enable using the command 'feature {name.ToLower()}' at the top of the file.");
        }
        void CheckUnreachable(Statement current)
        {
            if (unreachableCode > 0)
                unreachableCode--;
            else if (unreachableCode == 0)
                throw new StatementException(current, "Unreachable code detected.");
            else
                unreachableCode = -1;
        }

        /// <summary>
        /// Load JSON file with caching for next use.
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public JObject LoadJSONFile(string path)
        {
            int hash = path.GetHashCode();

            // cached file, dont read again
            if(loadedFiles.TryGetValue(hash, out object value))
                if (value is JObject)
                    return value as JObject;

            string contents = File.ReadAllText(path);
            JObject json = JObject.Parse(contents);
            loadedFiles[hash] = json;
            return json;
        }
        /// <summary>
        /// Load file with caching for next use.
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public string LoadFileString(string path)
        {
            int hash = path.GetHashCode();

            // cached file, dont read again
            if (loadedFiles.TryGetValue(hash, out object value))
                if (value is string)
                    return value as string;

            string contents = File.ReadAllText(path);
            loadedFiles[hash] = contents;
            return contents;
        }
        /// <summary>
        /// Load file with caching for next use.
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public byte[] LoadFileBytes(string path)
        {
            int hash = path.GetHashCode();

            // cached file, dont read again
            if (loadedFiles.TryGetValue(hash, out object value))
            {
                if (value is string)
                    return Encoding.UTF8.GetBytes(value as string);
                else if (value is byte[])
                    return value as byte[];
            }

            byte[] contents = File.ReadAllBytes(path);
            loadedFiles[hash] = contents;
            return contents;
        }

        /// <summary>
        /// Define a file that sort-of equates to a "standard library." Will only be added once.
        /// </summary>
        /// <param name="id"></param>
        /// <param name="file"></param>
        public void DefineSTDFile(CommandFile file)
        {
            if (definedStdFiles.Contains(file.GetHashCode()))
                return;
            definedStdFiles.Add(file.GetHashCode());
            AddExtraFile(file);
        }
        public bool HasSTDFile(CommandFile file)
        {
            return definedStdFiles.Contains(file.GetHashCode());
        }

        /// <summary>
        /// Get or set the active selector.
        /// Get peeks the stack.
        /// Set pushes then pops a new selector to the stack.
        /// </summary>
        public Selector ActiveSelector
        {
            get => selections.Peek();
            set
            {
                selections.Pop();
                selections.Push(value);
            }
        }
        /// <summary>
        /// The currently active selector represented as a string.
        /// </summary>
        public string ActiveSelectorStr
        {
            get => selections.Peek().ToString();
        }
        /// <summary>
        /// The currently active selector core represented as a string.
        /// </summary>
        public string ActiveSelectorCore
        {
            get => '@' + selections.Peek().core.ToString();
        }
        /// <summary>
        /// Push a copy of the current selector to the stack. If doesAlign is set, then the selector is reset to '@s'.
        /// </summary>
        /// <param name="doesAlign"></param>
        public void PushSelector(bool doesAlign)
        {
            if (doesAlign)
                selections.Push(new Selector(Selector.Core.s));
            else
                selections.Push(ActiveSelector);
        }
        /// <summary>
        /// Push a selector to the stack.
        /// </summary>
        /// <param name="now"></param>
        public void PushSelector(Selector now)
        {
            selections.Push(now);
        }
        /// <summary>
        /// Alias for PushSelector(true). Pushes a new selector representing '@s' to the stack and prepends the
        /// necessary execute command so that the command run through it will be aligned to the selected entity(s).
        /// </summary>
        public void PushSelectorExecute(Selector now)
        {
            if (now.NeedsAlign)
            {
                AppendCommandPrepend(Command.Execute(now.ToString(), Coord.here, Coord.here, Coord.here, ""));
                PushSelector(true);
                return;
            }

            PushSelector(false);
        }
        /// <summary>
        /// Alias for PushSelector(true). Pushes a new selector representing '@s' to the stack and prepends the
        /// necessary execute command so that the command run through it will be aligned to the selected entity(s).
        /// </summary>
        public void PushSelectorExecute(Selector now, Coord offsetX, Coord offsetY, Coord offsetZ)
        {
            if (now.NeedsAlign)
            {
                AppendCommandPrepend(Command.Execute(now.ToString(), offsetX, offsetY, offsetZ, ""));
                PushSelector(true);
                return;
            }

            PushSelector(false);
        }
        /// <summary>
        /// Pushes a new selector representing '@s' to the stack and prepends the
        /// necessary execute command so that the command run through it will be aligned to the selected entity(s).
        /// </summary>
        public void PushSelectorExecute()
        {
            Selector active = ActiveSelector;
            if (active.NeedsAlign)
            {
                AppendCommandPrepend(Command.Execute(active.ToString(), Coord.here, Coord.here, Coord.here, ""));
                PushSelector(true);
                return;
            }

            PushSelector(false);
        }

        /// <summary>
        /// Pushes a new selector representing '@s' to the stack and prepends the
        /// necessary execute command so that the command run through it will be aligned to the selected entity(s).
        /// 
        /// This variant offsets the position of the execution relative to each entity.
        /// </summary>
        public void PushSelectorExecute(Coord offsetX, Coord offsetY, Coord offsetZ)
        {
            Selector active = ActiveSelector;
            if (active.NeedsAlign)
            {
                AppendCommandPrepend(Command.Execute(active.ToString(), offsetX, offsetY, offsetZ, ""));
                PushSelector(true);
                return;
            }

            PushSelector(false);
        }
        /// <summary>
        /// Pop a selector off the stack and return to the previous.
        /// </summary>
        public void PopSelector()
        {
            unreachableCode = -1;
            selections.Pop();
        }

        
        /// <summary>
        /// The number of statements which will run before a selector is automatically popped.
        /// </summary>
        int popSelectorsAfterNext = 0;
        /// <summary>
        /// Schedules a selector pop after the next statement is run.
        /// </summary>
        public void PopSelectorAfterNext()
        {
            popSelectorsAfterNext = 2;
        }

        public bool HasNext
        {
            get => readIndex < statements.Length;
        }
        public Statement Peek() => statements[readIndex];
        public Statement Next() => statements[readIndex++];
        public T Next<T>() where T : Statement => statements[readIndex++] as T;
        public T Peek<T>() where T : Statement => statements[readIndex] as T;
        public T Peek<T>(int skip) where T : Statement => statements[readIndex + skip] as T;
        public bool NextIs<T>() where T : Statement => statements[readIndex] is T;
        /// <summary>
        /// Return an array of the next x statements.
        /// </summary>
        /// <param name="amount"></param>
        /// <returns></returns>
        public Statement[] Peek(int amount)
        {
            Statement[] ret = new Statement[amount];

            int write = 0;
            for (int i = readIndex; i < statements.Length && i < readIndex + amount; i++)
                ret[write++] = statements[i];

            return ret;
        }
        /// <summary>
        /// Gets the next statement, or set of statements if it is a block.
        /// </summary>
        /// <returns></returns>
        public Statement[] NextExecutionSet()
        {
            Statement current = statements[readIndex - 1];

            if(NextIs<StatementOpenBlock>())
            {
                StatementOpenBlock block = Next<StatementOpenBlock>();
                int statements = block.statementsInside;
                if (statements < 1)
                    throw new StatementException(current, "No valid statements inside block.");
                Statement[] code = Peek(statements);
                readIndex += statements;
                readIndex++; // block closer
                return code;
            }
            return new[] { Next() };
        }

        /// <summary>
        /// Pop the prepend buffer's contents and return it.
        /// </summary>
        /// <returns></returns>
        private string PopPrepend()
        {
            string ret = prependBuffer.ToString();
            prependBuffer.Clear();
            return ret;
        }

        internal Executor(Statement[] statements, Program.InputPPV[] inputPPVs,
            string projectName, string bpBase, string rpBase)
        {
            this.statements = statements;
            this.project = new ProjectManager(projectName, bpBase, rpBase);
            this.entities = new EntityManager(this);

            definedStdFiles = new List<int>();
            ppv = new Dictionary<string, dynamic[]>();
            macros = new List<Macro>();
            functions = new List<Function>();
            selections = new Stack<Selector>();

            if (inputPPVs != null && inputPPVs.Length > 0)
                foreach (Program.InputPPV ppv in inputPPVs)
                    SetPPV(ppv.name, new object[] { ppv.value });

            // support up to 100 levels of scope before blowing up
            lastPreprocessorCompare = new bool[100];
            lastActualCompare = new Token[100][];

            loadedFiles = new Dictionary<int, object>();
            definingStructs = new Stack<StructDefinition>();
            currentFiles = new Stack<CommandFile>();
            prependBuffer = new StringBuilder();
            scoreboard = new ScoreboardManager(this);

            PushSelector(true);
            SetCompilerPPVs();
            currentFiles.Push(new CommandFile(projectName));
        }
        /// <summary>
        /// Setup the default preprocessor variables.
        /// </summary>
        void SetCompilerPPVs()
        {
            ppv["minecraftversion"] = new dynamic[] { MINECRAFT_VERSION };
            ppv["compilerversion"] = new dynamic[] { MCC_VERSION };
            ppv["_true"] = new dynamic[] { "true" };
            ppv["_false"] = new dynamic[] { "false" };
        }
        /// <summary>
        /// Run this executor start to finish.
        /// </summary>
        public void Execute()
        {
            readIndex = 0;

            while (HasNext)
            {
                Statement unresolved = Next();
                Statement statement = unresolved.ClonePrepare(this);
                statement.SetExecutor(this);
                statement.Run0(this);
                scoreboard.PopTempState();

                if (statement is StatementComment)
                    continue; // ignore this statement

                // check for unreachable code due to halt directive
                CheckUnreachable(statement);

                if(Program.DEBUG)
                    Console.WriteLine("COMPILE LN{0}: {1}", statement.Line, statement.ToString());

                if (popSelectorsAfterNext >= 0)
                {
                    popSelectorsAfterNext--;
                    if (popSelectorsAfterNext == 0)
                        PopSelector();
                }
            }

            while (currentFiles.Count > 0)
                PopFile();
        }
        /// <summary>
        /// Temporarily run another subsection of statements then resume this executor.
        /// </summary>
        public void ExecuteSubsection(Statement[] section)
        {
            scoreboard.PushTempState();
            Statement[] restore0 = statements;
            int restore1 = readIndex;

            statements = section;
            readIndex = 0;
            while (HasNext)
            {
                Statement unresolved = Next();
                Statement statement = unresolved.ClonePrepare(this);
                statement.SetExecutor(this);
                statement.Run0(this);
                scoreboard.PopTempState();

                // check for unreachable code due to halt directive
                CheckUnreachable(statement);

                if (popSelectorsAfterNext >= 0)
                {
                    popSelectorsAfterNext--;
                    if (popSelectorsAfterNext == 0)
                        PopSelector();
                }
            }

            // now its done, so restore state
            scoreboard.PopTempState();
            statements = restore0;
            readIndex = restore1;
        }

        /// <summary>
        /// Set the result of the last preprocessor-if comparison in this scope.
        /// </summary>
        /// <param name="value"></param>
        public void SetLastIfResult(bool value) => lastPreprocessorCompare[ScopeLevel] = value;
        /// <summary>
        /// Get the result of the last preprocessor-if comparison in this scope.
        /// </summary>
        /// <returns></returns>
        public bool GetLastIfResult() => lastPreprocessorCompare[ScopeLevel];

        /// <summary>
        /// Set the last if-statement tokens used at this scope.
        /// </summary>
        /// <param name="selector"></param>
        public void SetLastCompare(Token[] inputTokens) =>
            lastActualCompare[ScopeLevel] = inputTokens;
        /// <summary>
        /// Get the last if-statement tokens used at this scope.
        /// </summary>
        /// <returns></returns>
        public Token[] GetLastCompare() =>
            lastActualCompare[ScopeLevel];

        /// <summary>
        /// Add a macro to be looked up later.
        /// </summary>
        /// <param name="macro"></param>
        public void RegisterMacro(Macro macro) =>
            macros.Add(macro);
        /// <summary>
        /// Add a function to be looked up later. Its commands can be written to by simply PushFile()ing to this executor.
        /// </summary>
        /// <param name="function"></param>
        public void RegisterFunction(Function function) =>
            functions.Add(function);
        public Macro? LookupMacro(string name)
        {
            foreach (Macro macro in macros)
                if (macro.Matches(name))
                    return macro;
            return null;
        }
        public Function LookupFunction(string name)
        {
            foreach (Function function in functions)
                if (function.Matches(name))
                    return function;
            return null;
        }
        public bool TryLookupMacro(string name, out Macro? macro)
        {
            macro = LookupMacro(name);
            return macro.HasValue;
        }
        public bool TryLookupFunction(string name, out Function function)
        {
            function = LookupFunction(name);
            return function != null;
        }

        /// <summary>
        /// Get the current file that should be written to.
        /// </summary>
        public CommandFile CurrentFile { get => currentFiles.Peek(); }
        /// <summary>
        /// Get the main .mcfunction file for this project.
        /// </summary>
        public CommandFile HeadFile { get => currentFiles.Last(); }

        /// <summary>
        /// Get the current scope level.
        /// </summary>
        public int ScopeLevel { get => currentFiles.Count - 1; }
        /// <summary>
        /// Get if the base file (projectName.mcfunction) is the active file.
        /// </summary>
        public bool IsScopeBase { get => currentFiles.Count <= 1; }

        /// <summary>
        /// Add a command to the current file, with prepend buffer.
        /// </summary>
        /// <param name="command"></param>
        public void AddCommand(string command) =>
            CurrentFile.Add(PopPrepend() + command);
        /// <summary>
        /// Add a set of commands into a new branching file unless inline is set.
        /// </summary>
        /// <param name="friendlyName">The friendly name to give the generated file, if any.</param>
        /// <param name="inline">Force the commands to be inlined rather than sent to a generated file.</param>
        /// <param name="commands"></param>
        public void AddCommands(IEnumerable<string> commands, string friendlyName, bool inline = false)
        {
            int count = commands.Count();
            if (count < 1)
                return;

            if (inline)
            {
                string buffer = PopPrepend();
                CurrentFile.Add(from c in commands select buffer + c);
                return;
            }

            if (count == 1)
            {
                AddCommand(commands.First());
                return;
            }

            CommandFile file = Executor.GetNextGeneratedFile(friendlyName);
            file.Add(commands);

            AddExtraFile(file);
            AddCommand(Command.Function(file));
        }
        /// <summary>
        /// Add a command to the current file, not modifying the prepend buffer.
        /// </summary>
        /// <param name="command"></param>
        public void AddCommandClean(string command)
        {
            string prepend = prependBuffer.ToString();
            CurrentFile.Add(prepend + command);
        }
        /// <summary>
        /// Add a set of commands into a new branching file, not modifying the prepend buffer.
        /// If inline is set, no branching file will be made.
        /// </summary>
        /// <param name="friendlyName">The friendly name to give the generated file, if any.</param>
        /// <param name="inline">Force the commands to be inlined rather than sent to a generated file.</param>
        /// <param name="commands"></param>
        public void AddCommandsClean(IEnumerable<string> commands, string friendlyName, bool inline = false)
        {
            string buffer = prependBuffer.ToString();

            if (inline)
            {
                CurrentFile.Add(commands.Select(c => buffer + c));
                return;
            }

            int count = commands.Count();
            if (count < 1)
                return;
            if (count == 1)
            {
                AddCommandClean(commands.First());
                return;
            }

            CommandFile file = Executor.GetNextGeneratedFile(friendlyName);
            file.Add(commands);

            AddExtraFile(file);
            CurrentFile.Add(buffer + Command.Function(file));
        }
        /// <summary>
        /// Add a file on its own to the list.
        /// </summary>
        /// <param name="file"></param>
        public void AddExtraFile(IAddonFile file) =>
            project.AddFile(file);
        /// <summary>
        /// Add a set of files on their own to the list.
        /// </summary>
        /// <param name="file"></param>
        public void AddExtraFiles(IAddonFile[] files) =>
            project.AddFiles(files);
        /// <summary>
        /// Add a command to the top of the 'head' file, being the main project function. Does not affect the prepend buffer.
        /// </summary>
        /// <param name="command"></param>
        public void AddCommandHead(string command) =>
            HeadFile.AddTop(command);
        /// <summary>
        /// Adds a set of commands to the top of the 'head' file, being the main project function. Does not affect the prepend buffer.
        /// </summary>
        /// <param name="commands"></param>
        public void AddCommandsHead(IEnumerable<string> commands)
        {
            if (commands.Count() < 1)
                return;
            HeadFile.AddTop(commands);
        }

        /// <summary>
        /// Set the content that will prepend the next added dirty command.
        /// </summary>
        /// <param name="content"></param>
        public void SetCommandPrepend(string content) =>
            prependBuffer.Clear().Append(content);
        /// <summary>
        /// Append to the content to the prepend buffer.
        /// </summary>
        /// <param name="content"></param>
        public void AppendCommandPrepend(string content) =>
            prependBuffer.Append(content);
        /// <summary>
        /// Prepend content to the prepend buffer.
        /// </summary>
        /// <param name="content"></param>
        public void PrependCommandPrepend(string content) =>
            prependBuffer.Insert(0, content);

        /// <summary>
        /// Try to get a preprocessor variable.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public bool TryGetPPV(string name, out dynamic[] value)
        {
            if (name.StartsWith("$"))
                name = name.Substring(1);
            return ppv.TryGetValue(name, out value);
        }
        /// <summary>
        /// Set or create a preprocessor variable.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="value"></param>
        public void SetPPV(string name, object[] value) =>
            ppv[name] = value;
        /// <summary>
        /// Resolve all preprocessor variables in a string.
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        public string ResolveString(string str)
        {
            foreach (var kv in ppv)
            {
                string name = '$' + kv.Key;
                string value;

                // Only join if necessary.
                if (kv.Value.Length > 1)
                    value = string.Join(" ", kv.Value);
                else
                    value = kv.Value[0].ToString();

                str = str.Replace(name, value);
            }

            return str;
        }
        public TokenLiteral[] ResolvePPV(TokenUnresolvedPPV unresolved)
        {
            int line = unresolved.lineNumber;
            string word = unresolved.word;

            if (TryGetPPV(word, out dynamic[] values))
            {
                TokenLiteral[] literals = new TokenLiteral[values.Length];
                for(int i = 0; i < values.Length; i++)
                {
                    dynamic value = values[i];
                    if (value is int)
                        literals[i] = new TokenIntegerLiteral(value, IntMultiplier.t, line);
                    if (value is float)
                        literals[i] = new TokenDecimalLiteral(value, line);
                    if (value is bool)
                        literals[i] = new TokenBooleanLiteral(value, line);
                    if (value is string)
                        literals[i] = new TokenStringLiteral(value, line);
                    if (value is Coord)
                        literals[i] = new TokenCoordinateLiteral(value, line);
                    if (value is Selector)
                        literals[i] = new TokenSelectorLiteral(value, line);
                }
                return literals;
            }

            return null;
        }

        public void PushFile(CommandFile file) =>
            currentFiles.Push(file);
        public void PopFile()
        {
            unreachableCode = -1;
            project.AddFile(currentFiles.Pop());
        }

        private static Dictionary<int, int> branchFileIndexes = new Dictionary<int, int>();
        /// <summary>
        /// Construct the next available command file with this name, like input0, input1, input2, etc...
        /// </summary>
        /// <param name="friendlyName">A user-friendly name to mark the file by.</param>
        /// <returns></returns>
        public static CommandFile GetNextGeneratedFile(string friendlyName)
        {
            int hash = friendlyName.GetHashCode();
            if (!branchFileIndexes.TryGetValue(hash, out int index))
                index = 0;

            branchFileIndexes[hash] = index + 1;
            return new CommandFile(friendlyName + index, MCC_GENERATED_FOLDER);
        }
        public static void ResetGeneratedFiles()
        {
            branchFileIndexes.Clear();
        }
    }
}