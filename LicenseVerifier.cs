using log4net;
using Minitex.Utils;
using Mira.Model.MiraModel.Security;
using Newtonsoft.Json.Linq;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Xml.Linq;
using Minitex.Primo;
using Minitex.Primo.PrimoSearchApi;
using Minitex.Primo.PrimoVE;

namespace Mira.Module.Verification
{
    public class LicenseVerifier
    {
        /// <summary>
        ///     Initializes the logger for this class
        /// </summary>
        private static readonly ILog Log = LogManager
            .GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private static string AlmaApiLink =>
            "https://api-na.hosted.exlibrisgroup.com/almaws/v1/electronic/e-collections/";

        private static string AlmaApiString => $"?apikey={Credentials.AlmaLicenseApiKey}&format=json";

        private static string PrimoApi1 =>
            "https://api-na.hosted.exlibrisgroup.com/primo/v1/search?vid=01UMN_INST:TWINCITIES&scope=TwinCitiesCampus_and_CI&q=any,exact,";

        private static string PrimoApi2 => "&qInclude=facet_rtype,exact,Journals";

        private static string PrimoApiString => $"&apikey={Credentials.PrimoApiKey}&format=json";

        private static string PrimoSearchATitle =>
            "https://api-na.hosted.exlibrisgroup.com/primo/v1/search?vid=01UMN_INST:TWINCITIES&scope=TwinCitiesCampus_and_CI&q=any,contains,";

        public string XmlDebugUrl { get; set; }

        /// <summary>
        ///     Helper function that returns the path of a key-value pair in a JObject
        /// </summary>
        public List<List<object>> FindPath(JObject obj, string key, string value, List<object> path)
        {
            var result = new List<List<object>>();

            if (obj.ContainsKey(key) && obj[key].ToString() == value)
            {
                path.Add(key);
                result.Add(path);
            }

            foreach (var prop in obj.Properties())
            {
                var newKey = prop.Name;
                var newValue = prop.Value;
                var newPath = new List<object>(path) { newKey };
                if (newValue is JObject newObj)
                {
                    foreach (var subPath in FindPath(newObj, key, value, newPath))
                    {
                        result.Add(subPath);
                    }
                }
                else if (newValue is JArray newArray)
                {
                    for (var i = 0; i < newArray.Count; i++)
                    {
                        var subValue = newArray[i];
                        var subPath = new List<object>(newPath) { i };
                        if (subValue is JObject subObj)
                        {
                            foreach (var subSubPath in FindPath(subObj, key, value, subPath))
                            {
                                result.Add(subSubPath);
                            }
                        }
                    }
                }
            }

            return result;
        }

        /// <summary>
        ///     Function that takes an article title string and returns corresponding license data
        /// </summary>
        public ObservableCollection<LicenseVerifierResult> GetLicenseByArticleTitle(string articleTitle)
        {
            // Store license verifier results 
            var licenseVerifierResults =
                new ObservableCollection<LicenseVerifierResult>();
            var primoFullTextResponse = new PrimoFullTextResponse();

            try
            {
                // Get Query result from Primo
                var primoSearch = new PrimoVeClient(Credentials.PrimoApiKey);
                primoFullTextResponse = primoSearch.PrimoFullTextSearch(articleTitle);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "License Verification Error", MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return licenseVerifierResults;
            }

            string articleTitleFromPrimo;

            try
            {
                articleTitleFromPrimo = primoFullTextResponse.Title;
            }
            catch (Exception ex)
            {
                MessageBox.Show("No suitable match found.", "License Verification Error", MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return licenseVerifierResults;
            }

            var stringUtilities = new StringUtilities();
            var similarityRatio =
                stringUtilities.CalculateSimilarity(articleTitle.ToLower(), articleTitleFromPrimo.ToLower());

            // Check the similarity ratio between articleTitle and articleTitleFromPrimo
            if (similarityRatio >= 0.9)
            {
                // Traverse the JSON to find GetIt link. Use the same paths for isLinktoOnline
                var linktoGetIt = primoFullTextResponse.FullTextUrl;

                // Transform the GetIt link to a debug link
                var debuggedLinktoGetIt =
                    linktoGetIt.Replace("svc_dat=viewit", "svc_dat=CTO") + "&debug=true";

                XmlDebugUrl = debuggedLinktoGetIt;

                // Get the debug link XML
                var client = new RestClient(debuggedLinktoGetIt);
                var request = new RestRequest(debuggedLinktoGetIt);
                var response = client.Execute(request);

                // Parse the XML content
                var xml_string = response.Content;
                var xml_root = XElement.Parse(xml_string);

                // Define namespace
                XNamespace ns = "http://com/exlibris/urm/uresolver/xmlbeans/u";

                var results = new List<Dictionary<string, string>>();

                // Traverse the Primo XML and find all the pieces we need
                foreach (var context_service in xml_root.Descendants(ns + "context_service"))
                {
                    // Any filtered result is tossed. Filtered results are usually for other campuses
                    var filtered = context_service.Descendants(ns + "key")
                        .FirstOrDefault(x => (string)x.Attribute("id") == "Filtered");
                    if (filtered == null)
                    {
                        // If this element is set to 1, it is a free resource
                        var isFree = context_service.Descendants(ns + "key")
                            .FirstOrDefault(x => (string)x.Attribute("id") == "Is_free");
                        // context_service_id is service_id
                        var service_id = context_service.Attribute("context_service_id").Value;
                        // package_pid is collection_id
                        var collection_id = context_service.Descendants(ns + "key")
                            .FirstOrDefault(x => (string)x.Attribute("id") == "package_pid")?.Value;
                        // portfolio_PID is portfolio_pid
                        var portfolio_pid = context_service.Descendants(ns + "key")
                            .FirstOrDefault(x => (string)x.Attribute("id") == "portfolio_PID")?.Value;
                        // Direct link to the article
                        var target_url = context_service.Descendants(ns + "target_url").FirstOrDefault()?.Value;

                        if (portfolio_pid != null && target_url != null)
                        {
                            var result = new Dictionary<string, string>();
                            result["isFree"] = isFree?.Value;
                            result["service_id"] = service_id;
                            result["collection_id"] = collection_id;
                            result["portfolio_pid"] = portfolio_pid;
                            result["target_url"] = target_url;
                            results.Add(result);
                        }
                    }
                }

                string almaJsonDataString = null;
                JObject almaJsonDataJObject = null;
                foreach (var result in results)
                {
                    try
                    {
                        // Use the "Electronic Resource" "Retrieve Portfolio" API
                        client = new RestClient(
                            $"{AlmaApiLink}{result["collection_id"]}/e-services/{result["service_id"]}/portfolios/{result["portfolio_pid"]}{AlmaApiString}");
                        request = new RestRequest(
                            $"{AlmaApiLink}{result["collection_id"]}/e-services/{result["service_id"]}/portfolios/{result["portfolio_pid"]}{AlmaApiString}");
                        var almaResponse = client.Execute(request);
                        almaJsonDataString = almaResponse.Content;
                        almaJsonDataJObject = JObject.Parse(almaJsonDataString);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.Message, "License Verification Error", MessageBoxButton.OK,
                            MessageBoxImage.Error);
                        return licenseVerifierResults;
                    }

                    // Start populating licenseVerifierResults
                    var licenseVerifierResult = new LicenseVerifierResult();
                    licenseVerifierResult.ArticleTitle = articleTitleFromPrimo;
                    licenseVerifierResult.FullTextLink = result["target_url"];
                    licenseVerifierResult.PortfolioId = result["portfolio_pid"];
                    licenseVerifierResult.ElectronicResourcePortfolio = $"{AlmaApiLink}{result["collection_id"]}/e-services/{result["service_id"]}/portfolios/{result["portfolio_pid"]}";
                    // Check if this is a free resource. Free resources have a portfolio but don't have a license
                    if (result["isFree"] == "1")
                    {
                        licenseVerifierResult.Resource = (string)almaJsonDataJObject["resource_metadata"]["title"];
                        licenseVerifierResult.ILLElectronic = "FREE RESOURCE";
                        licenseVerifierResult.ILLSecureElectronic = "FREE RESOURCE";
                        licenseVerifierResult.ILLPrintOrFax = "FREE RESOURCE";
                        licenseVerifierResult.InternationalILLAllowed = "FREE RESOURCE";
                        licenseVerifierResult.IsSuccess = true;
                    }
                    else
                    {
                        // This is checking if the resource has a license. For some reason, some none-free resources don't have a license
                        if (almaJsonDataJObject["license"] != null && almaJsonDataJObject["license"]["value"] != null &&
                            almaJsonDataJObject["license"]["value"].ToString() != "")
                        {
                            // Grab the license from the response from the Retrieve Portfolio
                            var licenseLink = almaJsonDataJObject["license"]["link"].ToString();
                            licenseVerifierResult.LicenseAPI = licenseLink + AlmaApiString;
                            try
                            {
                                // Use License API
                                var AlmaResponse =
                                    client.Execute(new RestRequest(licenseLink + AlmaApiString));
                                almaJsonDataJObject = JObject.Parse(AlmaResponse.Content);
                            }
                            catch (Exception ex)
                            {
                                MessageBox.Show(ex.Message, "License Verification Error", MessageBoxButton.OK,
                                    MessageBoxImage.Error);
                                return licenseVerifierResults;
                            }

                            try
                            {
                                licenseVerifierResult.Resource = (string)almaJsonDataJObject["name"];

                                // Find the path for Interlibrary Loan Electronic
                                var pathtoILLELEC = FindPath(almaJsonDataJObject,
                                    "value", "ILLELEC", new List<object>());
                                var codeforILLELEC = (int)pathtoILLELEC[0][1];

                                // Grab the value for the code for Interlibrary Loan Electronic
                                licenseVerifierResult.ILLElectronic =
                                    (string)almaJsonDataJObject["term"][codeforILLELEC]["value"]["value"];

                                // Find the path for Electrnic Transmission
                                var pathtoILLSET = FindPath(almaJsonDataJObject,
                                    "value",
                                    "ILLSET", new List<object>());
                                var codeforILLSET = (int)pathtoILLSET[0][1];

                                // Grab the value for the code for Secure Electrnic Transmission
                                licenseVerifierResult.ILLSecureElectronic =
                                    (string)almaJsonDataJObject["term"][codeforILLSET]["value"]["value"];

                                // Find the path for Print or Fax
                                var pathtoILLPRINTFAX = FindPath(almaJsonDataJObject,
                                    "value", "ILLPRINTFAX", new List<object>());
                                var codeforILLPRINTFAX = (int)pathtoILLPRINTFAX[0][1];

                                // Grab the value for the code for Print or Fax
                                licenseVerifierResult.ILLPrintOrFax =
                                    (string)almaJsonDataJObject["term"][codeforILLPRINTFAX]["value"]["value"];

                                // Find the path for International ILL
                                var pathtoINTLILL = FindPath(almaJsonDataJObject,
                                    "value", "INTLILL", new List<object>());
                                var codeforINTLILL = (int)pathtoINTLILL[0][1];

                                // Grab the value for the code for International ILL
                                licenseVerifierResult.InternationalILLAllowed =
                                    (string)almaJsonDataJObject["term"][codeforINTLILL]["value"]["value"];
                                licenseVerifierResult.IsSuccess = true;
                            }
                            // This catches invalid ID issues and sets IsSuccess to false
                            catch (Exception ex)
                            {
                                if (string.IsNullOrWhiteSpace(licenseVerifierResult.Resource))
                                {
                                    // Get resource name from resource_metadata
                                    licenseVerifierResult.Resource =
                                        (string)almaJsonDataJObject["resource_metadata"]["title"];
                                }

                                licenseVerifierResult.ILLElectronic = "INVALID LICENSE";
                                licenseVerifierResult.ILLSecureElectronic = "INVALID LICENSE";
                                licenseVerifierResult.ILLPrintOrFax = "INVALID LICENSE";
                                licenseVerifierResult.InternationalILLAllowed = "INVALID LICENSE";
                                licenseVerifierResult.IsSuccess = false;
                            }
                        }
                        else
                        {
                            try
                            {
                                licenseVerifierResult.Resource =
                                    (string)almaJsonDataJObject["resource_metadata"]["title"];
                            }
                            catch (Exception ex)
                            {
                                licenseVerifierResult.Resource = "INVALID RESOURCE";
                            }

                            licenseVerifierResult.ILLElectronic = "INVALID LICENSE";
                            licenseVerifierResult.ILLSecureElectronic = "INVALID LICENSE";
                            licenseVerifierResult.ILLPrintOrFax = "INVALID LICENSE";
                            licenseVerifierResult.InternationalILLAllowed = "INVALID LICENSE";
                            licenseVerifierResult.IsSuccess = false;
                        }
                    }

                    licenseVerifierResults.Add(licenseVerifierResult);
                }

                return licenseVerifierResults;
            }

            MessageBox.Show("No suitable match found.", "License Verification Error", MessageBoxButton.OK,
                MessageBoxImage.Error);
            return licenseVerifierResults;
        }

        // Function that takes an ISSN string and returns its corresponding license data
        public ObservableCollection<LicenseVerifierResult> GetLicenseByISSN(string ISSN)
        {
            // Store license verifier results 
            var licenseVerifierResults =
                new ObservableCollection<LicenseVerifierResult>();

            // Primo Search API using ISSN
            var client = new RestClient(PrimoApi1 + ISSN + PrimoApi2 + PrimoApiString);
            var request =
                new RestRequest(PrimoApi1 + ISSN + PrimoApi2 + PrimoApiString);

            JObject PrimoJSONdata = null;
            RestResponse response = null;
            try
            {
                response = client.Execute(request);
                PrimoJSONdata = JObject.Parse(response.Content);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "License Verification Error", MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return licenseVerifierResults;
            }

            var path = new List<List<object>>(FindPath(PrimoJSONdata, "isLinktoOnline", "True", new List<object>()));
            var docsPath = (int)path[0][1];
            var getItPath = (int)path[0][4];
            var linksPath = (int)path[0][6];

            // Traverse the JSON to find GetIt link. Use the same paths for isLinktoOnline
            var linktoGetIt =
                (string)PrimoJSONdata["docs"][docsPath]["delivery"]["GetIt1"][getItPath]["links"][linksPath]["link"];

            // Transform the GetIt link to a debug link
            var debuggedLinktoGetIt =
                linktoGetIt.Replace("http://1.1.1.1?",
                        "https://na01.alma.exlibrisgroup.com/view/uresolver/01UMN_INST/openurl-Twin%20Cities?")
                    .Replace("svc_dat=viewit", "svc_dat=CTO") + "&debug=true";

            // Get the debug link XML
            client = new RestClient(debuggedLinktoGetIt);
            request = new RestRequest(debuggedLinktoGetIt);
            response = client.Execute(request);

            // Parse the XML content
            var xml_string = response.Content;
            var xml_root = XElement.Parse(xml_string);

            // Define namespace
            XNamespace ns = "http://com/exlibris/urm/uresolver/xmlbeans/u";

            var results = new List<Dictionary<string, string>>();

            // Traverse the Primo XML and find all the pieces we need
            foreach (var context_service in xml_root.Descendants(ns + "context_service"))
            {
                // Any filtered result is tossed. Filtered results are usually for other campuses.
                var filtered = context_service.Descendants(ns + "key")
                    .FirstOrDefault(x => (string)x.Attribute("id") == "Filtered");
                if (filtered == null)
                {
                    // If this element is set to 1, it is a free resource
                    var isFree = context_service.Descendants(ns + "key")
                        .FirstOrDefault(x => (string)x.Attribute("id") == "Is_free");
                    // context_service_id is service_id
                    var service_id = context_service.Attribute("context_service_id").Value;
                    // package_pid is collection_id
                    var collection_id = context_service.Descendants(ns + "key")
                        .FirstOrDefault(x => (string)x.Attribute("id") == "package_pid")?.Value;
                    // portfolio_PID is portfolio_pid
                    var portfolio_pid = context_service.Descendants(ns + "key")
                        .FirstOrDefault(x => (string)x.Attribute("id") == "portfolio_PID")?.Value;
                    // Direct link to the article
                    var target_url = context_service.Descendants(ns + "target_url").FirstOrDefault()?.Value;

                    if (portfolio_pid != null && target_url != null)
                    {
                        var result = new Dictionary<string, string>();
                        result["isFree"] = isFree?.Value;
                        result["service_id"] = service_id;
                        result["collection_id"] = collection_id;
                        result["portfolio_pid"] = portfolio_pid;
                        result["target_url"] = target_url;
                        results.Add(result);
                    }
                }
            }

            string almaJsonDataString = null;
            JObject almaJsonDataJObject = null;
            foreach (var result in results)
            {
                try
                {
                    // Use the "Electronic Resource" "Retrieve Portfolio" API
                    client = new RestClient(
                        $"{AlmaApiLink}{result["collection_id"]}/e-services/{result["service_id"]}/portfolios/{result["portfolio_pid"]}{AlmaApiString}");
                    request = new RestRequest(
                        $"{AlmaApiLink}{result["collection_id"]}/e-services/{result["service_id"]}/portfolios/{result["portfolio_pid"]}{AlmaApiString}");
                    var almaResponse = client.Execute(request);
                    almaJsonDataString = almaResponse.Content;
                    almaJsonDataJObject = JObject.Parse(almaJsonDataString);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "License Verification Error", MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    return licenseVerifierResults;
                }

                // Start populating licenseVerifierResults
                var licenseVerifierResult = new LicenseVerifierResult();
                licenseVerifierResult.ArticleTitle = (string)PrimoJSONdata["docs"][0]["pnx"]["search"]["title"][0];
                licenseVerifierResult.FullTextLink = result["target_url"];
                licenseVerifierResult.ElectronicResourcePortfolio = $"{AlmaApiLink}{result["collection_id"]}/e-services/{result["service_id"]}/portfolios/{result["portfolio_pid"]}";

                // Check if this is a free resource. Free resources have a portfolio but don't have a license.
                if (result["isFree"] == "1")
                {
                    licenseVerifierResult.Resource = (string)almaJsonDataJObject["resource_metadata"]["title"];
                    licenseVerifierResult.ILLElectronic = "FREE RESOURCE";
                    licenseVerifierResult.ILLSecureElectronic = "FREE RESOURCE";
                    licenseVerifierResult.ILLPrintOrFax = "FREE RESOURCE";
                    licenseVerifierResult.InternationalILLAllowed = "FREE RESOURCE";
                }
                else
                {
                    // This is checking if the resource has a license. For some reason, some none-free resources don't have a license. Investigating.
                    if (almaJsonDataJObject["license"] != null && almaJsonDataJObject["license"]["value"] != null &&
                        almaJsonDataJObject["license"]["value"].ToString() != "")
                    {
                        // Grab the license from the response from the Retrieve Portfolio
                        var licenseLink = almaJsonDataJObject["license"]["link"].ToString();
                        licenseVerifierResult.LicenseAPI = licenseLink + AlmaApiString;
                        try
                        {
                            // Use License API
                            var AlmaResponse =
                                client.Execute(new RestRequest(licenseLink + AlmaApiString));
                            almaJsonDataJObject = JObject.Parse(AlmaResponse.Content);
                        }

                        catch (Exception ex)
                        {
                            MessageBox.Show(ex.Message, "License Verification Error", MessageBoxButton.OK,
                                MessageBoxImage.Error);
                            return licenseVerifierResults;
                        }


                        licenseVerifierResult.Resource = (string)almaJsonDataJObject["name"];

                        // Find the path for Interlibrary Loan Electronic
                        var pathtoILLELEC = FindPath(almaJsonDataJObject,
                            "value", "ILLELEC", new List<object>());
                        var codeforILLELEC = (int)pathtoILLELEC[0][1];

                        // Grab the value for the code for Interlibrary Loan Electronic
                        licenseVerifierResult.ILLElectronic =
                            (string)almaJsonDataJObject["term"][codeforILLELEC]["value"]["value"];

                        // Find the path for Electrnic Transmission
                        var pathtoILLSET = FindPath(almaJsonDataJObject, "value",
                            "ILLSET", new List<object>());
                        var codeforILLSET = (int)pathtoILLSET[0][1];

                        // Grab the value for the code for Secure Electrnic Transmission
                        licenseVerifierResult.ILLSecureElectronic =
                            (string)almaJsonDataJObject["term"][codeforILLSET]["value"]["value"];

                        // Find the path for Print or Fax
                        var pathtoILLPRINTFAX = FindPath(almaJsonDataJObject,
                            "value", "ILLPRINTFAX", new List<object>());
                        var codeforILLPRINTFAX = (int)pathtoILLPRINTFAX[0][1];

                        // Grab the value for the code for Print or Fax
                        licenseVerifierResult.ILLPrintOrFax =
                            (string)almaJsonDataJObject["term"][codeforILLPRINTFAX]["value"]["value"];

                        // Find the path for International ILL
                        var pathtoINTLILL = FindPath(almaJsonDataJObject,
                            "value", "INTLILL", new List<object>());
                        var codeforINTLILL = (int)pathtoINTLILL[0][1];

                        // Grab the value for the code for International ILL
                        licenseVerifierResult.InternationalILLAllowed =
                            (string)almaJsonDataJObject["term"][codeforINTLILL]["value"]["value"];
                    }
                    else
                    {
                        try
                        {
                            licenseVerifierResult.Resource = (string)almaJsonDataJObject["resource_metadata"]["title"];
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show(ex.Message, "License Verification Error", MessageBoxButton.OK,
                                MessageBoxImage.Error);
                            return licenseVerifierResults;
                        }

                        licenseVerifierResult.ILLElectronic = "NO LICENSE";
                        licenseVerifierResult.ILLSecureElectronic = "NO LICENSE";
                        licenseVerifierResult.ILLPrintOrFax = "NO LICENSE";
                        licenseVerifierResult.InternationalILLAllowed = "NO LICENSE";
                    }

                    licenseVerifierResults.Add(licenseVerifierResult);
                }
            }

            return licenseVerifierResults;
        }
    }
}