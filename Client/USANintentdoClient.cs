using Playnite.SDK.Plugins;
using System;
using System.Collections.Generic;
using System.Linq;
using RestSharp;
using Newtonsoft.Json.Linq;
using HtmlAgilityPack;
using Newtonsoft.Json;

namespace NintendoMetadata.Client
{
    public class USANintendoClient : NintendoClient
    {
        private const string UsAlgoliaId = "U3B6GR4UA3";
        private const string UsAlgoliaKey = "a29c6927638bfd8cee23993e51e721c9";

        protected override string BaseUrl => $"https://{UsAlgoliaId}-2.algolia.net/1/indexes/*/queries";

        public USANintendoClient(MetadataRequestOptions options, NintendoMetadataSettings settings) : base(options, settings)
        {
        }

        protected override void InitRestClient()
        {
            base.InitRestClient();
            restClient
                    .AddDefaultHeader("Content-Type", "application/json")
                    .AddDefaultHeader("Accept", "*/*")
                    .AddDefaultHeader("X-Algolia-API-Key", UsAlgoliaKey)
                    .AddDefaultHeader("X-Algolia-Application-Id", UsAlgoliaId);
        }

        public override List<NintendoGame> SearchGames(string normalizedSearchName)
        {
            List<NintendoGame> results = new List<NintendoGame>();

            var request = new RestRequest("/", Method.Post);
            var requestItem = new
            {
                indexName = "store_game_en_us",
                query = normalizedSearchName,
                facetFilters = new[] { "corePlatforms:Nintendo Switch", "hasDlc:false" },
                hitsPerPage = 10,
            };
            var body = $@"{{""requests"": [{JsonConvert.SerializeObject(requestItem)}]}}";
            request.RequestFormat = DataFormat.Json;
            request.AddBody(body);

            try
            {
                JObject response = ExecuteRequest(request);

                if (!response.TryGetValue("results", out JToken resultsData))
                {
                    logger.Error($"Encountered API error: [{response["status"]}] {response["message"]}");
                    return results;
                }

                var result = resultsData[0];
                logger.Debug($"SearchGames {result["nbHits"]} results for {normalizedSearchName}");

                foreach (dynamic game in result["hits"])
                {
                    NintendoGame g = NintendoGame.ParseUsGame(game);
                    results.Add(g);
                }
            }
            catch (Exception e)
            {
                logger.Error(e, "Error performing search");
            }

            return results.OrderByRelevance(normalizedSearchName);
        }

        public override NintendoGame GetGameDetails(NintendoGame game)
        {
            var link = game.Links.FirstOrDefault(l => l.Name == "My Nintendo Store");

            if (link == null)
            {
                return game;
            }

            var web = new HtmlWeb();
            var doc = web.Load(link.Url);
            var dataNode = doc.DocumentNode.SelectSingleNode(@"//script[@id='__NEXT_DATA__']");
            if (dataNode == null)
            {
                return game;
            }

            var dataJson = JObject.Parse(dataNode.InnerText);
            var sku = (string)dataJson.SelectToken($@"props.pageProps.analytics.product.sku");

            if (dataJson.SelectToken("props.pageProps.initialApolloState") is JObject apolloState)
            {
                var productKey = $"Product:{{\"sku\":\"{sku}\"}}";
                if (apolloState[productKey] is JObject product)
                {
                    var fullDescription = (string)product["description({\"html\":true})"];
                    if (!string.IsNullOrEmpty(fullDescription))
                    {
                        logger.Debug(fullDescription);

                        // Local function to remove a single outer <p> wrapper while preserving inner HTML
                        string RemoveOuterP(string html)
                        {
                            if (string.IsNullOrWhiteSpace(html))
                            {
                                return html;
                            }

                            var trimmed = html.Trim();
                            var tmpDoc = new HtmlDocument();
                            tmpDoc.LoadHtml(trimmed);

                            // Consider only non-whitespace child nodes
                            var significantChildren = tmpDoc.DocumentNode.ChildNodes
                                .Where(n => !(n.NodeType == HtmlNodeType.Text && string.IsNullOrWhiteSpace(n.InnerText)))
                                .ToList();

                            if (significantChildren.Count == 1 && 
                                string.Equals(significantChildren[0].Name, "p", StringComparison.OrdinalIgnoreCase))
                            {
                                // Return inner HTML of the single top-level <p> node
                                return significantChildren[0].InnerHtml;
                            }

                            return html;
                        }

                        game.FullDescription = RemoveOuterP(fullDescription);
                    }
                }
            }

            return game;
        }
    }
}
