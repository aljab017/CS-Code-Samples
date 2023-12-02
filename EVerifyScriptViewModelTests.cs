using Mira.Client.ViewModels;
using Mira.Model.MiraModel;
using Mira.Module.Verification.EVerifyScript;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Mira.Module.Verification.Tests.EVerifyScript
{
    public class EVerifyScriptViewModelTests
    {
        [Fact]
        public void ViewModelPopulatesAllLocationConfigs()
        {
            // Arrange
            var viewModel = EVerifyScriptViewModel.Create();

            // Act
            List<LocationEVerifyScriptConfig> locationEVerifyScriptConfig = viewModel.LocationEVerifyScriptConfigs;

            // Assert
            Assert.NotNull(locationEVerifyScriptConfig);
            Assert.True(locationEVerifyScriptConfig.Count > 0);
        }
        
        [Fact]
        public void LocationConfigsGetShuffled()
        {
            // Arrange
            var viewModel = EVerifyScriptViewModel.Create();

            // Act
            viewModel.WeightShuffleLocations();
            List<LocationEVerifyScriptConfig> locationEVerifyScriptConfig = viewModel.LocationEVerifyScriptConfigs;
            List<LocationEVerifyScriptConfig> weightShuffledLocationConfigs = viewModel.WeightShuffledLocationConfigs;

            // Assert
            Assert.NotEqual(locationEVerifyScriptConfig, weightShuffledLocationConfigs);
        }
        
        [Fact]
        public void PrioritizedLocationConfigsIsPopulatedCorrectly()
        {
            // Arrange
            var viewModel = EVerifyScriptViewModel.Create();

            // Act
            viewModel.WeightShuffleLocations();
            List<LocationEVerifyScriptConfig> weightShuffledLocationConfigs = viewModel.WeightShuffledLocationConfigs;
            List<PrioritizedLocationEVerifyScriptConfig> prioritizedLocationEVerifyScriptConfigs = viewModel.PrioritizedLocationEVerifyScriptConfigs.ToList();

            int LocationsCount = weightShuffledLocationConfigs.Count;
            LocationEVerifyScriptConfig location3 = weightShuffledLocationConfigs[2];
            LocationEVerifyScriptConfig lastLocation = weightShuffledLocationConfigs[LocationsCount - 1];

            // Assert
            Assert.Equal(location3, prioritizedLocationEVerifyScriptConfigs[2].Configuration);
            Assert.Equal(2, prioritizedLocationEVerifyScriptConfigs[2].Priority);

            Assert.Equal(lastLocation, prioritizedLocationEVerifyScriptConfigs[LocationsCount - 1].Configuration);
            Assert.Equal(LocationsCount - 1, prioritizedLocationEVerifyScriptConfigs[LocationsCount - 1].Priority);
        }
       
        [Fact]
        public void AddingMAC4ReportCodePutsItInList()
        {
            // Arrange
            var viewModel = EVerifyScriptViewModel.Create();

            // Act
            viewModel.WeightShuffleLocations();
            viewModel.AddToSource("MAC", 4);
            var addedLocations = viewModel.AddedLocations;
            var addedLocation = addedLocations[0];

            // Assert
            Assert.NotNull(addedLocations);
            Assert.NotNull(addedLocation);
            Assert.Equal("MAC", addedLocation.LocationCode);
            Assert.Equal(4, addedLocation.ReportCode);
        }

    }
}
