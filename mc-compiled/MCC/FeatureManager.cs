﻿using mc_compiled.Commands;
using mc_compiled.MCC.Compiler;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace mc_compiled.MCC
{
    internal static class FeatureManager
    {
        /// <summary>
        /// All features available for enabling.
        /// </summary>
        internal static Feature[] FEATURE_LIST = (Feature[])Enum.GetValues(typeof(Feature));

        /// <summary>
        /// Dictionary of features and their actions to perform on an executor when enabled.
        /// </summary>
        internal static Dictionary<Feature, Action<Executor>> ENABLE_ACTIONS = new Dictionary<Feature, Action<Executor>>()
        {
            {
                Feature.NULLS, (executor) =>
                {
                    executor.entities.nulls.AddEntityToProject();
                    executor.SetPPV("null", new object[] { executor.entities.nulls.nullType });
                }
            },
            {
                Feature.GAMETEST, (executor) =>
                {
                    Executor.Warn("gametest integration doesn't currently do anything.");
                }
            },
            {
                Feature.EXPLODERS, (executor) =>
                {
                    executor.entities.exploders.AddEntityToProject();
                }
            },
            {
                Feature.UNINSTALL, (executor) => { }
            }
        };

        /// <summary>
        /// Called when a feature is enabled so that its enable action can run.
        /// </summary>
        /// <param name="caller"></param>
        /// <param name="feature"></param>
        internal static void OnFeatureEnabled(Executor caller, Feature feature)
        {
            if (caller.linting)
                return; // dont bother creating any extraneous files

            if (ENABLE_ACTIONS.TryGetValue(feature, out Action<Executor> run))
                run(caller);
        }
    }
}
