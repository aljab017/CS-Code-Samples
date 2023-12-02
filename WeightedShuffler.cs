using System;
using System.Collections.Generic;
using System.Linq;
using Mira.Model.MiraModel;

namespace Mira.Module.Verification.EVerifyScript
{
    public class WeightedShuffler
    {
        public int NumberOfGroup1Locations { get; set; }
        public List<LocationEVerifyScriptConfig> UnshuffledLocationList { get; set; }
        public List<LocationEVerifyScriptConfig> WeightShuffledLocations { get; set; }
        public List<PrioritizedLocationEVerifyScriptConfig> Group1Locations { get; set; }
        public List<PrioritizedLocationEVerifyScriptConfig> Group2Locations { get; set; }
        public List<PrioritizedLocationEVerifyScriptConfig> LastResortLocations { get; set; }
        public List<PrioritizedLocationEVerifyScriptConfig> PrioritizedLocationEVerifyScriptConfigs { get; set; }

        public WeightedShuffler(int numberOfGroup1Locations, List<LocationEVerifyScriptConfig> locations)
        {
            if (numberOfGroup1Locations <= 0)
            {
                throw new ArgumentException("The number of Group1 locations must be greater than zero.");
            }

            if (locations == null || locations.Count == 0)
            {
                throw new ArgumentException("The list of locations is empty or null.");
            }

            NumberOfGroup1Locations = numberOfGroup1Locations;
            UnshuffledLocationList = locations;
            WeightShuffledLocations = new List<LocationEVerifyScriptConfig>();
            LastResortLocations = new List<PrioritizedLocationEVerifyScriptConfig>();
            PrioritizedLocationEVerifyScriptConfigs = new List<PrioritizedLocationEVerifyScriptConfig>();

            // Separate the last resort locations from the main list and add them to LastResortLocations
            var lastResortLocations = UnshuffledLocationList.Where(location => location.IsLastResort).ToList();
            foreach (var lastResortLocation in lastResortLocations)
            {
                UnshuffledLocationList.Remove(lastResortLocation);
                var prioritizedLastResortLocation = new PrioritizedLocationEVerifyScriptConfig(lastResortLocation)
                {
                    Priority = 999
                };
                LastResortLocations.Add(prioritizedLastResortLocation);
            }

            ShuffleGroups();
        }

        /// <summary>
        /// Performs a weighted shuffle for a list of LocationEVerifyScriptConfig objects. 
        /// </summary>
        /// <remarks>Adapted from https://softwareengineering.stackexchange.com/questions/233541/how-to-implement-a-weighted-shuffle </remarks>
        public void ShuffleGroups()
        {
            Random random = new Random();

            // Calculate the sum of weights
            decimal totalWeight = 0;
            decimal tolerance = 0.0001M;
            foreach (var location in UnshuffledLocationList)
            {
                totalWeight += location.Weight;
            }
                
            while (UnshuffledLocationList.Count > 0)
            {
                // Generate a random number from 0 to the sum of weights
                decimal randomNumber = (decimal)random.NextDouble() * totalWeight;
                decimal currentSum = 0;
                LocationEVerifyScriptConfig selectedLocation = null;

                foreach (var location in UnshuffledLocationList)
                {
                    currentSum += location.Weight;
                    if (randomNumber < currentSum + tolerance)
                    {
                        selectedLocation = location;
                        break;
                    }
                }

                if (selectedLocation != null)
                {
                    totalWeight -= selectedLocation.Weight;
                    WeightShuffledLocations.Add(selectedLocation);
                    UnshuffledLocationList.Remove(selectedLocation);
                }
            }

            // Convert LocationEVerifyScriptConfigs to PrioritizedLocationEVerifyScriptConfigs 
            int priority = 0;
            foreach (var location in WeightShuffledLocations)
            {
                var prioritizedLocationEVerifyScriptConfig = new PrioritizedLocationEVerifyScriptConfig(location)
                {
                    Priority = priority
                };
                priority++;
                PrioritizedLocationEVerifyScriptConfigs.Add(prioritizedLocationEVerifyScriptConfig);
            }

            // Separate the shuffled locations into different groups
            Group1Locations = new List<PrioritizedLocationEVerifyScriptConfig>(PrioritizedLocationEVerifyScriptConfigs.Take(NumberOfGroup1Locations));
            Group2Locations = new List<PrioritizedLocationEVerifyScriptConfig>(PrioritizedLocationEVerifyScriptConfigs.Skip(NumberOfGroup1Locations));

            // Add last Resort Locations to PrioritizedLocationEVerifyScriptConfigs
            PrioritizedLocationEVerifyScriptConfigs.AddRange(LastResortLocations);
        }

    }

}