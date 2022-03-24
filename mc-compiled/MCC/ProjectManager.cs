﻿using mc_compiled.Modding;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace mc_compiled.MCC
{
    /// <summary>
    /// Holds data about the project and formats/writes files.
    /// </summary>
    internal class ProjectManager
    {
        internal readonly string name;
        internal string description = "Change Me!";

        readonly OutputRegistry registry;
        readonly List<IAddonFile> files;
        private uint intents;

        /// <summary>
        /// Create a new ProjectManager with default description.
        /// </summary>
        /// <param name="name">The name of the project.</param>
        /// <param name="bpBase">e.g: development_behavior_packs/project_name/</param>
        /// <param name="rpBase">e.g: development_resource_packs/project_name/</param>
        internal ProjectManager(string name, string bpBase, string rpBase)
        {
            this.name = name;
            registry = new OutputRegistry(bpBase, rpBase);
            files = new List<IAddonFile>();
            intents = 0;
        }
        internal void AddFile(IAddonFile file) =>
            files.Add(file);
        internal void WriteAllFiles()
        {
            // manifests
            bool needsBehaviorManifest = !File.Exists(Path.Combine(registry.bpBase, "manifest.json"));
            bool needsResourceManifest = !File.Exists(Path.Combine(registry.rpBase, "manifest.json"));
            needsBehaviorManifest &= files.Any(file => file.GetOutputLocation().IsBehavior());
            needsResourceManifest &= files.Any(file => !file.GetOutputLocation().IsBehavior());

            if (needsBehaviorManifest)
                AddFile(new Manifest(OutputLocation.b_ROOT, Guid.NewGuid(), name, "Change Me!")
                    .WithModule(Manifest.Module.BehaviorData(name)));
            if (needsResourceManifest)
                AddFile(new Manifest(OutputLocation.r_ROOT, Guid.NewGuid(), name, "Change Me!")
                    .WithModule(Manifest.Module.ResourceData(name)));

            // actual writing
            foreach (IAddonFile file in files)
                WriteSingleFile(file);
            files.Clear();
        }
        internal void WriteSingleFile(IAddonFile file)
        {
            OutputLocation baseLocation = file.GetOutputLocation();
            string folder = registry[baseLocation];
            string extend = file.GetExtendedDirectory();

            if (extend != null)
                folder = Path.Combine(folder, extend);

            // create folder if it doesn't exist
            Directory.CreateDirectory(folder);

            // write it
            string output = Path.Combine(folder, file.GetOutputFile());
            File.WriteAllBytes(output, file.GetOutputData());
        }

        /// <summary>
        /// Allow this project an intent.
        /// </summary>
        /// <param name="intent"></param>
        internal void GiveIntent(Intent intent) =>
            intents |= (uint)intent;
        /// <summary>
        /// Check if this project has an intent.
        /// </summary>
        /// <param name="intent"></param>
        internal bool HasIntent(Intent intent) =>
            (intents &= (uint)intent) != 0;
    }
    /// <summary>
    /// Generates and holds a "registry" for directing file outputs.
    /// </summary>
    internal struct OutputRegistry
    {
        internal readonly string bpBase; // e.g: development_behavior_packs/project_name/
        internal readonly string rpBase; // e.g: development_resource_packs/project_name/
        readonly Dictionary<OutputLocation, string> registry;

        internal OutputRegistry(string bpBase, string rpBase)
        {
            this.bpBase = bpBase;
            this.rpBase = rpBase;
            registry = new Dictionary<OutputLocation, string>();

            // BP Folders
            registry[OutputLocation.b_ROOT] = bpBase;
            registry[OutputLocation.b_ANIMATIONS] = Path.Combine(bpBase, "animations");
            registry[OutputLocation.b_ANIMATION_CONTROLLERS] = Path.Combine(bpBase, "animation_controllers");
            registry[OutputLocation.b_BLOCKS] = Path.Combine(bpBase, "blocks");
            registry[OutputLocation.b_BIOMES] = Path.Combine(bpBase, "biomes");
            registry[OutputLocation.b_ENTITIES] = Path.Combine(bpBase, "entities");
            registry[OutputLocation.b_FEATURES] = Path.Combine(bpBase, "features");
            registry[OutputLocation.b_FEATURE_RULES] = Path.Combine(bpBase, "feature_rules");
            registry[OutputLocation.b_FUNCTIONS] = Path.Combine(bpBase, "functions");
            registry[OutputLocation.b_ITEMS] = Path.Combine(bpBase, "items");
            registry[OutputLocation.b_LOOT_TABLES] = Path.Combine(bpBase, "loot_tables");
            registry[OutputLocation.b_RECIPES] = Path.Combine(bpBase, "recipes");
            registry[OutputLocation.b_SCRIPTS_CLIENT] = Path.Combine(bpBase, "scripts", "client");
            registry[OutputLocation.b_SCRIPTS_SERVER] = Path.Combine(bpBase, "scripts", "server");
            registry[OutputLocation.b_SCRIPTS_GAMETESTS] = Path.Combine(bpBase, "scripts", "gametests");
            registry[OutputLocation.b_SPAWN_RULES] = Path.Combine(bpBase, "spawn_rules");
            registry[OutputLocation.b_TEXTS] = Path.Combine(bpBase, "texts");
            registry[OutputLocation.b_TRADING] = Path.Combine(bpBase, "trading");
            registry[OutputLocation.b_STRUCTURES] = Path.Combine(bpBase, "structures");

            // RP Folders
            registry[OutputLocation.r_ROOT] = rpBase;
            registry[OutputLocation.r_ANIMATION_CONTROLLERS] = Path.Combine(rpBase, "animation_controllers");
            registry[OutputLocation.r_ANIMATIONS] = Path.Combine(rpBase, "animations");
            registry[OutputLocation.r_ATTACHABLES] = Path.Combine(rpBase, "attachables");
            registry[OutputLocation.r_ENTITY] = Path.Combine(rpBase, "entity");
            registry[OutputLocation.r_FOGS] = Path.Combine(rpBase, "fogs");
            registry[OutputLocation.r_MODELS_ENTITY] = Path.Combine(rpBase, "models", "entity");
            registry[OutputLocation.r_MODELS_BLOCKS] = Path.Combine(rpBase, "models", "blocks");
            registry[OutputLocation.r_PARTICLES] = Path.Combine(rpBase, "particles");
            registry[OutputLocation.r_ITEMS] = Path.Combine(rpBase, "items");
            registry[OutputLocation.r_RENDER_CONTROLLERS] = Path.Combine(rpBase, "render_controllers");
            registry[OutputLocation.r_SOUNDS] = Path.Combine(rpBase, "sounds");
            registry[OutputLocation.r_TEXTS] = Path.Combine(rpBase, "texts");
            registry[OutputLocation.r_TEXTURES_ENVIRONMENT] = Path.Combine(rpBase, "textures", "environment");
            registry[OutputLocation.r_TEXTURES_BLOCKS] = Path.Combine(rpBase, "textures", "blocks");
            registry[OutputLocation.r_TEXTURES_ENTITY] = Path.Combine(rpBase, "textures", "entity");
            registry[OutputLocation.r_TEXTURES_ITEMS] = Path.Combine(rpBase, "textures", "items");
            registry[OutputLocation.r_TEXTURES_PARTICLE] = Path.Combine(rpBase, "textures", "particle");
            registry[OutputLocation.r_UI] = Path.Combine(rpBase, "ui");
        }
        internal string this[OutputLocation location] =>
            registry[location];
    }
    internal enum Intent : uint
    {
        NO_INTENTS = 0,     // No intents.

        NULLS = 1 << 0,     // Permission to create nulls.
        WORKROOM = 1 << 1   // Permission to use the 0, 0 chunk and ticking area.
    }
}