using Newtonsoft.Json.Linq;
using Playnite.SDK;
using Playnite.SDK.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace NintendoMetadata
{
    public class NintendoGame : GenericItemOption
    {

        public string Title { get; set; }

        public string FullDescription { get; set; }

        public ReleaseDate? ReleaseDate { get; set; }

        public List<MetadataProperty> Developers { get; set; }

        public List<MetadataProperty> Publishers { get; set; }

        public List<MetadataProperty> Genres { get; set; }

        public List<MetadataProperty> Series { get; set; }

        public List<MetadataProperty> AgeRatings { get; set; }

        public List<Link> Links { get; set; }

        public MetadataFile Image { get; set; }

        public MetadataFile WideCoverImage { get; set; }

        public MetadataFile LandscapeImage { get; set; }

        public string NSUID { get; set; }

        public Game LibraryGame;

        public NintendoGame()
        {
            this.Developers = new List<MetadataProperty>();
            this.Publishers = new List<MetadataProperty>();
            this.Genres = new List<MetadataProperty>();
            this.Series = new List<MetadataProperty>();
            this.AgeRatings = new List<MetadataProperty>();
            this.Links = new List<Link>();
        }

        public static NintendoGame ParseUsGame(JObject data)
        {
            var result = new NintendoGame
            {
                Title = ((string)data["title"]).Replace("™", ""),
                FullDescription = (string)data["description"] ?? "",
                ReleaseDate = new ReleaseDate((DateTime)data["releaseDate"]),
                NSUID = (string)data["nsuid"],
            };

            var developer = (string)data["softwareDeveloper"];

            if (!string.IsNullOrEmpty(developer?.Trim()))
            {
                if (developer.EndsWith(", LTD.") || developer.EndsWith(", Inc."))
                {
                    result.Developers.Add(new MetadataNameProperty(developer));
                }
                else
                {
                    var developers = new Regex(@", ").Split(developer);
                    foreach (var d in developers)
                    {
                        result.Developers.Add(new MetadataNameProperty(d));
                    }
                }
            }

            var publisher = (string)data["softwarePublisher"];
            if (!string.IsNullOrEmpty(publisher?.Trim()))
            {
                result.Publishers.Add(new MetadataNameProperty(publisher));
            }

            IList<string> genreList = data["genres"]?.Select(v => (string)v)?.ToList() ?? new List<string>();
            foreach (string genre in genreList)
            {
                result.Genres.Add(new MetadataNameProperty(genre));
            }

            foreach (dynamic franchise in data["franchises"])
            {
                result.Series.Add(new MetadataNameProperty((string)franchise));
            }

            var esrbRating = $"ESRB {(string)data["contentRatingCode"]}";
            result.AgeRatings.Add(new MetadataNameProperty(esrbRating));

            result.Links.Add(new Link("My Nintendo Store", $"https://www.nintendo.com{(string)data["url"]}"));

            var squareImageUrl = ((string)data["productImageSquare"])?.Replace("_1024", "_512");
            result.Image = new MetadataFile(squareImageUrl);

            var wideCoverImageUrl = $"https://assets.nintendo.com/image/upload/ar_16:9,b_auto:border,c_lpad/b_white/f_auto/q_auto/dpr_1/c_scale,w_640/{(string)data["productImage"]}";
            result.WideCoverImage = new MetadataFile(wideCoverImageUrl);

            var landscapeImageUrl = $"https://assets.nintendo.com/image/upload/ar_16:9,b_auto:border,c_lpad/b_white/f_auto/q_auto/dpr_1/c_scale,w_1920/{(string)data["productImage"]}";
            result.LandscapeImage = new MetadataFile(landscapeImageUrl);

            result.Name = result.Title;
            result.Description = $"{result.ReleaseDate?.Year}-{result.ReleaseDate?.Month}-{result.ReleaseDate?.Day} | {publisher ?? ""}";

            return result;
        }

        public static NintendoGame ParseEuropeGame(JObject data)
        {
            var result = new NintendoGame
            {
                Title = (string)data["title"],
                FullDescription = (string)data["product_catalog_description_s"],
                ReleaseDate = new ReleaseDate((DateTime)data["dates_released_dts"][0]),
                NSUID = (string)data["nsuid_txt"]?[0],
            };
            
            var developer = (string)data["softwareDeveloper"];
            if (!string.IsNullOrEmpty(developer))
            {
                result.Developers.Add(new MetadataNameProperty(developer));
            }
            
            result.Publishers.Add(new MetadataNameProperty((string)data["publisher"]));

            IList<string> genreList = data["pretty_game_categories_txt"]?.Select(v => (string)v)?.ToList() ?? new List<string>();
            foreach (string genre in genreList)
            {
                result.Genres.Add(new MetadataNameProperty(genre));
            }

            result.AgeRatings.Add(new MetadataNameProperty((string)data["pretty_agerating_s"]));

            result.Links.Add(new Link("My Nintendo Store", $"https://www.nintendo.com{(string)data["url"]}"));

            var imageUrl = (string)data["image_url_sq_s"] ?? ((string)data["image_url_tm_s"])?.Replace("300w", "500w") ?? (string)data["image_url"];
            result.Image = new MetadataFile(imageUrl);

            var wideCoverImageUrl = ((string)data["image_url_h2x1_s"]) ?? (string)data["image_url"];
            result.WideCoverImage = new MetadataFile(wideCoverImageUrl);

            result.LandscapeImage = new MetadataFile(((string)data["image_url_h2x1_s"])?.Replace("500w", "1600w") ?? (string)data["image_url"]);

            result.Name = result.Title;
            result.Description = $"{result.ReleaseDate?.Year}-{result.ReleaseDate?.Month}-{result.ReleaseDate?.Day} | {result.Publishers.First()}";
            
            return result;
        }

        public static NintendoGame ParseJapanGame(JObject data)
        {
            var result = new NintendoGame
            {
                Title = (string)data["title"],
                FullDescription = (string)data["text"],
                ReleaseDate = new ReleaseDate((DateTime)data["dsdate"]),
                NSUID = (string)data["nsuid"],
            };

            result.Publishers.Add(new MetadataNameProperty((string)data["maker"]));

            IList<string> genreList = data["genre"]?.Select(v => (string)v)?.ToList() ?? new List<string>();
            foreach (string genre in genreList)
            {
                JapanGenreMap.TryGetValue(genre, out string g);
                result.Genres.Add(new MetadataNameProperty(g ?? genre));
            }

            if ((string)data["url"] != null)
            {
                result.Links.Add(new Link("Nintendo Landing Page", (string)data["url"]));
            }

            var storeUrl = $"https://store-jp.nintendo.com/list/software/{(string)data["nsuid"]}.html";
            result.Links.Add(new Link("My Nintendo Store", storeUrl));

            // 1920x1080 $"https://img-eshop.cdn.nintendo.net/i/{data["iurl"]}.jpg"
            // sw $"https://store-jp.nintendo.com/dw/image/v2/BFGJ_PRD/on/demandware.static/-/Sites-all-master-catalog/ja_JP/dwe8af036b/products/D{(string)data["nsuid"]}/heroBanner/{(string)data["iurl"]}.jpg?sw=1024&strip=false"
            var imageUrl = $"https://store-jp.nintendo.com/dw/image/v2/BFGJ_PRD/on/demandware.static/-/Sites-all-master-catalog/ja_JP/dwe8af036b/products/D{(string)data["nsuid"]}/squareHeroBanner/{(string)data["siurl"]}.jpg?sw=512&strip=false";
            result.Image = new MetadataFile(imageUrl);

            var wideCoverImageUrl = $"https://store-jp.nintendo.com/dw/image/v2/BFGJ_PRD/on/demandware.static/-/Sites-all-master-catalog/ja_JP/dwe8af036b/products/D{(string)data["nsuid"]}/heroBanner/{(string)data["iurl"]}.jpg?sw=640&strip=false";
            result.WideCoverImage = new MetadataFile(wideCoverImageUrl);

            var landscapeImageUrl = $"https://img-eshop.cdn.nintendo.net/i/{data["iurl"]}.jpg";
            result.LandscapeImage = new MetadataFile(landscapeImageUrl);

            result.Name = result.Title;
            result.Description = $"{result.ReleaseDate?.Year}-{result.ReleaseDate?.Month}-{result.ReleaseDate?.Day} | {result.Publishers.First()}";
            
            return result;
        }

        public static NintendoGame ParseAsiaGame(JObject data)
        {
            var result = new NintendoGame()
            {
                Title = (string)data["common"]["title"],
                ReleaseDate = new ReleaseDate((DateTime)data["releaseDate"]),
                NSUID = (string)data["common"]["nsuid"],
            };

            string developer = (string)data.SelectToken("common.developerName");
            if (!string.IsNullOrEmpty(developer))
            {
                result.Developers.Add(new MetadataNameProperty(developer));
            }

            result.Publishers.Add(new MetadataNameProperty((string)data["common"]["publisherName"]));

            result.AgeRatings.Add(new MetadataNameProperty($"ESRB {(string)data["common"]["esrb"]}"));

            if ((string)data["softPageUrl"] != null)
            {
                var storeUrl = $"https://www.nintendo.com/sg{(string)data["softPageUrl"]}";
                result.Links.Add(new Link("My Nintendo Store", storeUrl));
            }
            else
            {
                var storeUrl = $"https://www.nintendo.com/sg/games/switch/detail/{(string)data["common"]["nsuid"]}";
                result.Links.Add(new Link("My Nintendo Store", storeUrl));
            }

            var imageUrl = (string)data.SelectToken("common.heroImageSquare.url");
            if (imageUrl != null)
            {
                result.Image = new MetadataFile(imageUrl);
            }

            var landscapeImageUrl = (string)data.SelectToken("common.heroImage169.url");
            if (landscapeImageUrl != null)
            {
                result.WideCoverImage = new MetadataFile(landscapeImageUrl + "?w=640");
                result.LandscapeImage = new MetadataFile(landscapeImageUrl);
            }

            if (result.Image == null)
            {
                result.Image = result.WideCoverImage ?? result.LandscapeImage;
            }

            result.Name = result.Title;
            result.Description = $"{result.ReleaseDate?.Year}-{result.ReleaseDate?.Month}-{result.ReleaseDate?.Day} | {result.Publishers.First()}";

            return result;
        }

        public static Dictionary<string, string> JapanGenreMap = new Dictionary<string, string>()
        {
            { "アクション", "Action" },
            { "アドベンチャー", "Adventure" },
            { "アーケード", "Arcade" },
            { "コミュニケーション", "Communication" },
            { "格闘", "Fighting" },
            { "音楽", "Music" },
            { "パーティー", "Party" },
            { "パズル", "Puzzle" },
            { "レース", "Race" },
            { "ロールプレイング", "Role-playing (RPG)"},
            { "シューティング", "Shooting" },
            { "シミュレーション", "Simulation" },
            { "スポーツ", "Sports" },
            { "ストラテジー", "Strategy" },
            { "学習", "Study" },
            { "テーブル", "Table" },
            { "トレーニング", "Training" },
            { "実用", "Utility" },
            { "その他", "Other" },
        };
    }
}
