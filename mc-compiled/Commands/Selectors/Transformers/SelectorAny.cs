﻿using mc_compiled.MCC;
using mc_compiled.MCC.Compiler;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace mc_compiled.Commands.Selectors.Transformers
{
    internal sealed class SelectorAny : MutationProvider
    {
        public string GetKeyword() => "ANY";
        public bool CanBeInverted() => true;

        public Mutation.SelectorMutation[] GetMutations(bool inverted, Executor executor, Statement tokens)
        {
            /*Selector testFor = tokens.Next<TokenSelectorLiteral>();
            testFor.count = new Count(1);

            const string counter = "_mcc_counter";
            string activeSelector = executor.ActiveSelectorStr;

            ScoreboardValue temp = executor.scoreboard.RequestTemp();

            commands.Add(Command.ScoreboardSet(activeSelector, temp, 0));
            commands.Add(Command.Tag(activeSelector, counter));
            commands.Add(Command.Execute(testFor.ToString(), Coord.here, Coord.here, Coord.here,
                Command.ScoreboardSet($"@e[tag={counter}]", temp, 1)));
            commands.Add(Command.TagRemove(activeSelector, counter));

            rootSelector.scores.checks.Add(new ScoresEntry(temp, new Range(inverted ? 0 : 1, false)));*/

            return null;
        }
    }
}
