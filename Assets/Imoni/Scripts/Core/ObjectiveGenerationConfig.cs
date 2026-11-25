using System;
using Newtonsoft.Json.Linq;

namespace Imoni.Core
{
    public sealed class ObjectiveGenerationConfig
    {
        public int RequiredCount { get; }
        public decimal MinimumFraction { get; }
        public decimal MaximumFraction { get; }

        internal ObjectiveGenerationConfig(JObject data)
        {
            JToken section = data["objectiveGeneration"] ?? throw new ArgumentException("Missing objectiveGeneration config.");
            if (section["requiredCount"]?.Type != JTokenType.Integer ||
                !IsNumber(section["minimumFraction"]) || !IsNumber(section["maximumFraction"]))
                throw new ArgumentException("Objective count must be an integer and fractions must be numeric.");
            RequiredCount = (int?)section["requiredCount"] ?? throw new ArgumentException("Missing requiredCount.");
            MinimumFraction = (decimal?)section["minimumFraction"] ?? throw new ArgumentException("Missing minimumFraction.");
            MaximumFraction = (decimal?)section["maximumFraction"] ?? throw new ArgumentException("Missing maximumFraction.");
            if (RequiredCount < 1 || MinimumFraction <= 0 || MinimumFraction > MaximumFraction || MaximumFraction >= 1)
                throw new ArgumentException("Invalid objective generation count/fractions.");
        }

        private static bool IsNumber(JToken token) => token?.Type == JTokenType.Integer || token?.Type == JTokenType.Float;
    }
}
