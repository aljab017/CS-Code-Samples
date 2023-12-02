using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Mira.Model.MiraModel;
using Mira.Module.Verification.EVerifyScript;
using Xunit;

namespace Mira.Module.Verification.Tests.EVerifyScript
{
    public class WeightedShufflerTests
    {
        public List<LocationEVerifyScriptConfig> LocationsExample1 = new List<LocationEVerifyScriptConfig>
        {
            new LocationEVerifyScriptConfig() { Code = "aaa", Weight = 1 },
            new LocationEVerifyScriptConfig() { Code = "bbb", Weight = 2, IsLastResort = true},
            new LocationEVerifyScriptConfig() { Code = "ccc", Weight = 3 },
            new LocationEVerifyScriptConfig() { Code = "ddd", Weight = 4, IsLastResort = true},
            new LocationEVerifyScriptConfig() { Code = "eee", Weight = 5 },
            new LocationEVerifyScriptConfig() { Code = "fff", Weight = 6 },
            new LocationEVerifyScriptConfig() { Code = "ggg", Weight = 7, IsLastResort = true }
        };

        public List<LocationEVerifyScriptConfig> LocationsExample2 = new List<LocationEVerifyScriptConfig>
        {
            new LocationEVerifyScriptConfig() { Code = "aaa", Weight = 1.2M },
            new LocationEVerifyScriptConfig() { Code = "bbb", Weight = 3.8M },
            new LocationEVerifyScriptConfig() { Code = "ccc", Weight = 2.5M },
            new LocationEVerifyScriptConfig() { Code = "ddd", Weight = 7 },
            new LocationEVerifyScriptConfig() { Code = "eee", Weight = 4.3M },
            new LocationEVerifyScriptConfig() { Code = "fff", Weight = 9.9M },
            new LocationEVerifyScriptConfig() { Code = "ggg", Weight = 6.6M },
            new LocationEVerifyScriptConfig() { Code = "hhh", Weight = 8 },
            new LocationEVerifyScriptConfig() { Code = "iii", Weight = 5.5M },
            new LocationEVerifyScriptConfig() { Code = "jjj", Weight = 10 }
        };

        [Fact]
        public void ConstructorSavesNumberOfGroup1Locations()
        {
            // Arrange
            int expectedGroup1Locations = 6;
            
            // Act
            var shufflerObject = new WeightedShuffler(expectedGroup1Locations, LocationsExample1);
            // assert
            Assert.Equal(expectedGroup1Locations, shufflerObject.NumberOfGroup1Locations);
        }

        [Fact]
        public void LastResortLocationAlwaysLast()
        {
            // arrange
            int numberOfGroup1Locations = 6;
            var expectedLastResortLocations = new List<LocationEVerifyScriptConfig>
            {
               LocationsExample1[1],
               LocationsExample1[3],
               LocationsExample1[6],
            };

            // act
            var shufflerObject = new WeightedShuffler(numberOfGroup1Locations , LocationsExample1);

            // assert
            Assert.Equal(expectedLastResortLocations[0], shufflerObject.LastResortLocations[0].Configuration);
            Assert.Equal(expectedLastResortLocations[1], shufflerObject.LastResortLocations[1].Configuration);
            Assert.Equal(expectedLastResortLocations[2], shufflerObject.LastResortLocations[2].Configuration);
        }

        [Fact]
        public void AllLocationsInGroups()
        {
            // Arrange
            int numberOfGroup1Locations = 3;
            int originalLocationsExample1Count = LocationsExample2.Count;

            // Act
            var shufflerObject = new WeightedShuffler(numberOfGroup1Locations, LocationsExample2);
            
            // Assert
            Assert.Equal(numberOfGroup1Locations, shufflerObject.Group1Locations.Count);
            Assert.Equal(originalLocationsExample1Count - numberOfGroup1Locations, shufflerObject.Group2Locations.Count + shufflerObject.LastResortLocations.Count);
        }

        [Fact]
        public void LastResortLocationsIsNotShuffled()
        {
            // Arrange
            int numberOfGroup1Locations = 6;
            var expectedLastResortLocations = LocationsExample1.Where(location => location.IsLastResort).ToList();

            // Act
            var shufflerObject = new WeightedShuffler(numberOfGroup1Locations, LocationsExample1);
            var lastResortLocations = shufflerObject.LastResortLocations;

            // Assert
            Assert.Equal(expectedLastResortLocations[0], shufflerObject.LastResortLocations[0].Configuration);
            Assert.Equal(expectedLastResortLocations[2], shufflerObject.LastResortLocations[2].Configuration);
        }

        [Fact]
        public void GroupsAreShuffled()
        {
            // Arrange
            int numberOfGroup1Locations = 3;
            var originalGroup1Locations = LocationsExample2.Take(numberOfGroup1Locations);
            var originalGroup2Locations = LocationsExample2.Skip(numberOfGroup1Locations);
           
            // Act
            var shufflerObject = new WeightedShuffler(numberOfGroup1Locations, LocationsExample2);
            var shuffledGroup1Locations = shufflerObject.WeightShuffledLocations.Take(numberOfGroup1Locations);
            var shuffledGroup2Locations = shufflerObject.WeightShuffledLocations.Skip(numberOfGroup1Locations);

            // Assert
            Assert.NotEqual(originalGroup1Locations, shuffledGroup1Locations);
            Assert.NotEqual(originalGroup2Locations, shuffledGroup2Locations);
        }

    }

}


