﻿using mc_compiled.Commands;
using mc_compiled.Commands.Selectors;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace mc_compiled.MCC.Compiler
{
    /// <summary>
    /// A set of comparisons.
    /// </summary>
    public class ComparisonSet : List<Comparison>
    {
        /// <summary>
        /// Instantiates an empty ComparisonSet.
        /// </summary>
        public ComparisonSet() : base() { }

        /// <summary>
        /// Instantiates a ComparisonSet with an array of items in it to start.
        /// </summary>
        /// <param name="comparisons"></param>
        public ComparisonSet(params Comparison[] comparisons) : base(comparisons) { }
        /// <summary>
        /// Instantiates a ComparisonSet with a set of items in it to start.
        /// </summary>
        /// <param name="comparisons"></param>
        public ComparisonSet(IEnumerable<Comparison> comparisons) : base(comparisons) { }

        /// <summary>
        /// Pulls tokens identifying with comparisons from a statement and return a new ComparisonSet holding them.
        /// </summary>
        /// <returns></returns>
        public static ComparisonSet GetComparisons(Executor executor, Statement tokens)
        {
            if (!tokens.HasNext)
                return new ComparisonSet();

            ComparisonSet set = new ComparisonSet();
            bool invertNext = false;
            Token currentToken;

            // ! read at your own risk !

            do
            {
                currentToken = tokens.Next();

                if (currentToken is TokenNot)
                {
                    invertNext = !invertNext;
                    continue;
                }

                if (currentToken is TokenIdentifierValue identifierValue)
                {
                    ScoreboardValue value = identifierValue.value;
                    if (value is ScoreboardValueBoolean booleanValue)
                    {
                        // ComparisonBoolean
                        // if <boolean>
                        ComparisonBoolean boolean = new ComparisonBoolean(booleanValue, invertNext);
                        invertNext = false;
                        set.Add(boolean);
                    }
                    else
                    {
                        // ComparisonValue
                        // if <score> <operator> <value>
                        TokenCompare comparison = tokens.Next<TokenCompare>();
                        Token b = tokens.Next();

                        ComparisonValue field = new ComparisonValue(identifierValue,
                            comparison.GetCompareType(), b, invertNext);

                        invertNext = false;
                        set.Add(field);
                    }
                }
                else if (currentToken is TokenSelectorLiteral selectorLiteral)
                {
                    // ComparisonSelector
                    // if <@selector>
                    ComparisonSelector comparison = new ComparisonSelector(selectorLiteral.selector, invertNext);
                    invertNext = false;
                    set.Add(comparison);
                }
                else if (currentToken is TokenIdentifier identifier)
                {
                    string word = identifier.word.ToUpper();

                    if (word.Equals("COUNT"))
                    {
                        // ComparisonCount
                        // if count <@selector> <operator> <value>
                        TokenSelectorLiteral selector = tokens.Next<TokenSelectorLiteral>();
                        TokenCompare comparison = tokens.Next<TokenCompare>();
                        Token b = tokens.Next();

                        ComparisonCount count = new ComparisonCount(selector,
                            comparison.GetCompareType(), b, invertNext);

                        invertNext = false;
                        set.Add(count);
                    }
                    else if (word.Equals("ANY"))
                    {
                        // ComparisonAny
                        // if any <@selector>
                        TokenSelectorLiteral selector = tokens.Next<TokenSelectorLiteral>();
                        ComparisonAny any = new ComparisonAny(selector, invertNext);

                        invertNext = false;
                        set.Add(any);
                    }
                    else if (word.Equals("BLOCK"))
                    {
                        // ComparisonBlock
                        // if block <x> <y> <z> <block> [data]
                        Coord x = tokens.Next<TokenCoordinateLiteral>();
                        Coord y = tokens.Next<TokenCoordinateLiteral>();
                        Coord z = tokens.Next<TokenCoordinateLiteral>();
                        string block = tokens.Next<TokenStringLiteral>();

                        int? data = null;
                        if (tokens.NextIs<TokenIntegerLiteral>())
                            data = tokens.Next<TokenIntegerLiteral>();

                        ComparisonBlock blockCheck = new ComparisonBlock(x, y, z, block, data, invertNext);

                        invertNext = false;
                        set.Add(blockCheck);
                    }
                    else if (word.Equals("POSITIONED"))
                    {
                        // ComparisonPositioned
                        // if positioned <x> <y> <z>
                        Coord x = tokens.Next<TokenCoordinateLiteral>();
                        Coord y = tokens.Next<TokenCoordinateLiteral>();
                        Coord z = tokens.Next<TokenCoordinateLiteral>();

                        ComparisonPositioned positioned = new ComparisonPositioned(x, y, z, invertNext);

                        invertNext = false;
                        set.Add(positioned);
                    }
                }

                if (!tokens.HasNext)
                    break;

                currentToken = tokens.Next();

                // loop again if an AND operator is present
                if (currentToken is TokenAnd)
                    continue;
                else
                    break;

            } while (tokens.HasNext);

            return set;
        }


        /// <summary>
        /// Run this ComparisonSet and apply it to an executor.
        /// </summary>
        /// <param name="executor"></param>
        /// <param name="callingStatement"></param>
        public void Run(Executor executor, Statement callingStatement)
        {
            if (this.IsEmpty)
                throw new StatementException(callingStatement, "No valid conditions specified.");

            List<string> commands = new List<string>();
            StringBuilder setupBuffer = new StringBuilder(); // used with positioned argument
            StringBuilder primaryBuffer = new StringBuilder();


            // add a size-1 buffer, popped after the statements inside are run
            executor.PushSelector(false);
            Selector activeSelector = executor.ActiveSelector;
            
            // align before doing anything else
            if(activeSelector.NeedsAlign)
                primaryBuffer.Append(activeSelector.GetAsPrefix());

            foreach (Comparison comparison in this)
            {
                var partCommands = comparison.GetCommands(executor);
                var partSelector = comparison.GetSelector(executor, callingStatement);

                if (partCommands != null)
                    commands.AddRange(partCommands);

                string part = partSelector.GetAsPrefix();
                
                // append to setup buffer so it also applies to the setup commands (if needed).
                if (comparison.ModifiesSetupCommands)
                    setupBuffer.Append(part);

                primaryBuffer.Append(part);

                // aligned to @s after this
                executor.PopSelector();
                executor.PushSelector(true);
            }

            if (!executor.HasNext)
                throw new StatementException(callingStatement, "Unexpected end of file when running comparison.");

            // get the next statement to determine how to run this comparison
            Statement next = executor.Peek();

            // add the commands given from all the comparisons
            string setup = setupBuffer.ToString();
            if (string.IsNullOrEmpty(setup))
            {
                // no setup prepend needed.
                executor.AddCommandsClean(commands, "compareSetup");
            } else
            {
                // need to set the setup prepend.
                string old = executor.SetCommandPrepend(setup);
                executor.AddCommands(commands, "compareSetup");
                executor.SetCommandPrepend(old);
            }

            int popCount = Count;

            if (next is StatementOpenBlock openBlock)
            {
                // only do the block stuff if necessary.
                if (openBlock.statementsInside == 0)
                    return; // do nothing
                else if (openBlock.statementsInside == 1)
                {
                    // modify prepend buffer as if 1 statement was there
                    executor.AppendCommandPrepend(primaryBuffer.ToString());
                    executor.PopSelectorAfterNext();
                    openBlock.openAction = null;
                    openBlock.CloseAction = null;
                }
                else
                {
                    CommandFile blockFile = Executor.GetNextGeneratedFile("branch");
                    string command = primaryBuffer.ToString() + Command.Function(blockFile);
                    executor.AddCommand(command);

                    openBlock.openAction = (e) =>
                    {
                        e.PushFile(blockFile);
                    };
                    openBlock.CloseAction = (e) =>
                    {
                        e.PopFile();
                        e.PopSelector();
                    };
                }
            }
            else
            {
                executor.AppendCommandPrepend(primaryBuffer.ToString());
                executor.PopSelectorAfterNext();
            }
        }
        /// <summary>
        /// Set the inversion on all elements.
        /// </summary>
        /// <param name="invert"></param>
        public void InvertAll(bool invert)
        {
            foreach (Comparison item in this)
                item.SetInversion(invert);
        }

        public bool IsEmpty
        {
            get => Count == 0;
        }
    }

    /// <summary>
    /// Represents a generic comparison in an if-statement.
    /// </summary>
    public abstract class Comparison
    {
        readonly bool originallyInverted;

        /// <summary>
        /// Encodes a selector's hash and depth together with a prefix. (not actually 'encoding')
        /// </summary>
        /// <param name="prefix"></param>
        /// <param name="depth"></param>
        /// <param name="selector"></param>
        /// <returns></returns>
        public static string DepthEncode(string prefix, int depth, Selector selector)
        {
            return prefix + depth + '_' + selector.GetHashCode().ToString();
        }
        public Comparison(bool invert)
        {
            originallyInverted = invert;
            inverted = invert;
        }
        /// <summary>
        /// Returns if this comparison modifies the setup command prepend as well as the primary one.
        /// </summary>
        public virtual bool ModifiesSetupCommands
        {
            get => false;
        }

        /// <summary>
        /// If this comparison is inverted.
        /// </summary>
        public bool inverted;
        /// <summary>
        /// Toggles the inversion of this comparison.
        /// </summary>
        public void SetInversion(bool invert) => inverted = invert ? !originallyInverted : originallyInverted;

        /// <summary>
        /// Get the commands needed, if any, to set up this comparison. May return null if no commands are needed.
        /// </summary>
        /// <returns></returns>
        public abstract IEnumerable<string> GetCommands(Executor executor);
        /// <summary>
        /// Gets the selector for this stage of the if-statement.
        /// </summary>
        /// <param name="executor"></param>
        /// <param name="callingStatement"></param>
        public abstract Selector GetSelector(Executor executor, Statement callingStatement);
    }
}
