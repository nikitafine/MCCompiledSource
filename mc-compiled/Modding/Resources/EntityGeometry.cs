﻿using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace mc_compiled.Modding.Resources
{
    public class EntityGeometry : IAddonFile
    {
        public string name;
        public string identifier;
        public int textureWidth;
        public int textureHeight;

        public EntityGeometry(string name, string identifier)
        {
            this.name = name;
            this.identifier = identifier;
            textureWidth = 16;
            textureHeight = 16;
        }
        public JObject ToJSON()
        {
            return new JObject()
            {
                ["format_version"] = FormatVersion.r_MODEL.ToString(),
                ["minecraft:geometry"] = new JArray(new[] {
                    new JObject()
                    {
                        ["description"] = new JObject()
                        {
                            ["identifier"] = identifier,
                            ["texture_width"] = textureWidth,
                            ["texture_height"] = textureHeight
                        }
                    }
                })
            };
        }

        public string GetExtendedDirectory() =>
            null;
        public byte[] GetOutputData()
        {
            JObject full = ToJSON();
            string str = full.ToString();
            return Encoding.UTF8.GetBytes(str);
        }
        public string GetOutputFile() =>
            name + ".json";
        public OutputLocation GetOutputLocation() =>
            OutputLocation.r_MODELS_ENTITY;
    }
}