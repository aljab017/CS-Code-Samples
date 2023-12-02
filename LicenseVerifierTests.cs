using Xunit;

namespace Mira.Module.Verification.Tests
{
    public class LicenseVerifierTests
    {
        [Fact]
        public void OneResultLicenseTest()
        {
            var licenseVerifier = new LicenseVerifier();
            var results = licenseVerifier.GetLicenseByArticleTitle(
                "Anesthetic Properties of Xenon in Animals and Human Beings, with Additional Observations on Krypton");

            string expectedFullTextLink =
                "https://login.ezproxy.lib.umn.edu/login?url=https://www.jstor.org/stable/1679348?sid=primo";
            string resultFullTextLink = results[0].FullTextLink;
            string expectedResource = "JSTOR";
            string resultResource = results[0].Resource;
            string expectedILLElectronic = "PROHIBITED";
            string resultILLElectronic = results[0].ILLElectronic;

            Assert.Equal(expectedFullTextLink, resultFullTextLink);
            Assert.Equal(expectedResource, resultResource);
            Assert.Equal(expectedILLElectronic, resultILLElectronic);
        }

        [Fact]
        public void FreeResourceLicenseTest()
        {
            var licenseVerifier = new LicenseVerifier();
            var results = licenseVerifier.GetLicenseByArticleTitle(
                "Racial/Ethnic Differences in COVID-19 Vaccine Hesitancy Among Health Care Workers in 2 Large Academic Hospitals");

            string expectedILLElectronic = "FREE RESOURCE";
            string resultILLElectronic = results[0].ILLElectronic;
            string expectedInternationalILL = "FREE RESOURCE";
            string resultInternationalILL = results[0].InternationalILLAllowed;

            Assert.Equal(expectedInternationalILL, resultInternationalILL);
            Assert.Equal(expectedILLElectronic, resultILLElectronic);
        }

        [Fact]
        public void GetLicenseByISSNTest()
        {
            var licenseVerifier = new LicenseVerifier();
            var results = licenseVerifier.GetLicenseByISSN("00368075");

            string expectedILLElectronic = "PERMITTED";
            string resultILLElectronic = results[0].ILLElectronic;

            Assert.Equal(expectedILLElectronic, resultILLElectronic);
        }
    }
}