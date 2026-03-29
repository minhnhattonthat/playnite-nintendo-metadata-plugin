using NintendoMetadata.Client;
using Playnite.SDK;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime;
using System.Text.RegularExpressions;

namespace NintendoMetadata
{
    public class NintendoMetadataProvider : OnDemandMetadataProvider
    {
        private readonly MetadataRequestOptions options;
        private readonly NintendoMetadata plugin;
        private readonly IPlayniteAPI playniteApi;

        private INintendoClient client;
        private NintendoGame game;
        private static readonly ILogger logger = LogManager.GetLogger();
        private List<MetadataField> availableFields;
        public override List<MetadataField> AvailableFields
        {
            get
            {
                if (availableFields == null)
                {
                    availableFields = GetAvailableFields();
                }

                return availableFields;
            }
        }

        public NintendoMetadataProvider(MetadataRequestOptions options, NintendoMetadata plugin)
        {
            this.options = options;
            this.plugin = plugin;
            this.playniteApi = plugin.PlayniteApi;
            var pluginSettings = ((NintendoMetadataSettingsViewModel)plugin.GetSettings(false)).Settings;

            var storeRegion = pluginSettings.StoreRegion;

            var isPlayniteGameRegionPreferred = pluginSettings.IsPlayniteGameRegionPreferred;
            if (isPlayniteGameRegionPreferred)
            {
                var regionName = options.GameData.Regions?[0]?.Name;
                if (regionName != null)
                {
                    var success = Enum.TryParse<StoreRegion>(regionName, out var parsedRegion);
                    if (success)
                    {
                        storeRegion = parsedRegion;
                    }
                }
            }

            switch (storeRegion)
            {
                case StoreRegion.USA:
                    this.client = new USANintendoClient(options, pluginSettings);
                    break;
                case StoreRegion.Europe:
                    this.client = new EuropeNintendoClient(options, pluginSettings);
                    break;
                case StoreRegion.Japan:
                    this.client = new JapanNintendoClient(options, pluginSettings);
                    break;
                case StoreRegion.Asia:
                    this.client = new AsiaNintendoClient(options, pluginSettings);
                    break;
                default:
                    this.client = new USANintendoClient(options, pluginSettings);
                    break;
            }
        }

        private List<MetadataField> GetAvailableFields()
        {
            if (this.game == null)
            {
                GetNintendoGameMetadata();
            }

            var fields = new List<MetadataField> { MetadataField.Links };

            fields.Add(MetadataField.Name);

            if (!string.IsNullOrEmpty(game.FullDescription))
            {
                fields.Add(MetadataField.Description);
            }

            if (game.ReleaseDate != null && game.ReleaseDate?.Year != null)
            {
                fields.Add(MetadataField.ReleaseDate);
            }

            if (game.Developers.Count > 0)
            {
                fields.Add(MetadataField.Developers);
            }

            if (game.Publishers.Count > 0)
            {
                fields.Add(MetadataField.Publishers);
            }

            if (game.Genres.Count > 0)
            {
                fields.Add(MetadataField.Genres);
            }

            if (game.Series.Count > 0)
            {
                fields.Add(MetadataField.Series);
            }

            if (game.Links.Count > 0)
            {
                fields.Add(MetadataField.Links);
            }

            if (game.Image?.HasImageData == true)
            {
                fields.Add(MetadataField.CoverImage);
            }

            if (game.LandscapeImage?.HasImageData == true)
            {
                fields.Add(MetadataField.BackgroundImage);
            }

            var ageRatingOrgPriority = playniteApi.ApplicationSettings.AgeRatingOrgPriority;
            var storeRegion = ((NintendoMetadataSettingsViewModel)plugin.GetSettings(false)).Settings.StoreRegion;
            if (game.AgeRatings.Count > 0 &&
                (ageRatingOrgPriority == AgeRatingOrg.ESRB && storeRegion == StoreRegion.USA)
                || (ageRatingOrgPriority == AgeRatingOrg.PEGI && storeRegion == StoreRegion.Europe))
            {
                fields.Add(MetadataField.AgeRating);
            }

            return fields;
        }

        private void GetNintendoGameMetadata()
        {
            if (this.game != null)
            {
                return;
            }

            if (!options.IsBackgroundDownload)
            {
                logger.Debug("not background");
                var item = plugin.PlayniteApi.Dialogs.ChooseItemWithSearch(null, (a) =>
                {
                    if (string.IsNullOrWhiteSpace(a))
                    {
                        return new List<GenericItemOption>();
                    }
                    
                    // TODO: search by ID

                    return client.SearchGames(a.NormalizeGameName()).Cast<GenericItemOption>().ToList();
                }, options.GameData.Name);

                if (item != null)
                {
                    var searchItem = item as NintendoGame;
                    this.game = client.GetGameDetails(searchItem);
                }
                else
                {
                    this.game = new NintendoGame();
                    logger.Warn($"Cancelled search");
                }
            }
            else
            {
                NintendoGame gameResult = new NintendoGame();
                try
                {
                    var normalizeSearchString = options.GameData.Name.NormalizeGameName();
                    List<GenericItemOption> results = client.SearchGames(normalizeSearchString).Cast<GenericItemOption>().ToList();
                    if (results.Count == 0)
                    {
                        gameResult = new NintendoGame();
                    }
                    else
                    {
                        gameResult = (NintendoGame)results.First();
                    }
                }
                catch (Exception e)
                {
                    logger.Error(e, "Failed to get Nintendo game metadata.");
                    gameResult = new NintendoGame();
                }
                this.game = client.GetGameDetails(gameResult);
            }
        }

        public override string GetName(GetMetadataFieldArgs args)
        {
            if (AvailableFields.Contains(MetadataField.Name))
            {
                return this.game.Title;
            }

            return base.GetName(args);
        }

        public override string GetDescription(GetMetadataFieldArgs args)
        {
            if (AvailableFields.Contains(MetadataField.Description))
            {
                return this.game.FullDescription;
            }

            return base.GetDescription(args);
        }

        public override ReleaseDate? GetReleaseDate(GetMetadataFieldArgs args)
        {
            if (AvailableFields.Contains(MetadataField.ReleaseDate))
            {
                return this.game.ReleaseDate;
            }

            return base.GetReleaseDate(args);
        }

        public override IEnumerable<MetadataProperty> GetPublishers(GetMetadataFieldArgs args)
        {
            if (AvailableFields.Contains(MetadataField.Publishers))
            {
                return this.game.Publishers;
            }

            return base.GetPublishers(args);
        }

        public override IEnumerable<MetadataProperty> GetDevelopers(GetMetadataFieldArgs args)
        {
            if (AvailableFields.Contains(MetadataField.Developers))
            {
                return this.game.Developers;
            }

            return base.GetDevelopers(args);
        }

        public override IEnumerable<MetadataProperty> GetGenres(GetMetadataFieldArgs args)
        {
            if (AvailableFields.Contains(MetadataField.Genres))
            {
                return this.game.Genres;
            }

            return base.GetGenres(args);
        }

        public override IEnumerable<MetadataProperty> GetSeries(GetMetadataFieldArgs args)
        {
            if (AvailableFields.Contains(MetadataField.Series))
            {
                return this.game.Series;
            }

            return base.GetSeries(args);
        }

        public override IEnumerable<MetadataProperty> GetAgeRatings(GetMetadataFieldArgs args)
        {
            if (AvailableFields.Contains(MetadataField.AgeRating))
            {
                return this.game.AgeRatings;
            }
            return base.GetAgeRatings(args);
        }

        public override IEnumerable<Link> GetLinks(GetMetadataFieldArgs args)
        {
            if (AvailableFields.Contains(MetadataField.Links))
            {
                return this.game.Links;
            }

            return base.GetLinks(args);
        }

        public override MetadataFile GetCoverImage(GetMetadataFieldArgs args)
        {
            if (AvailableFields.Contains(MetadataField.CoverImage))
            {
                var coverStyle = ((NintendoMetadataSettingsViewModel)plugin.GetSettings(false)).Settings.CoverStyle;
                if (coverStyle == CoverStyle.Wide && this.game.WideCoverImage?.HasImageData == true)
                {
                    return this.game.WideCoverImage;
                }
                return this.game.Image;
            }
            return base.GetCoverImage(args);
        }

        public override MetadataFile GetBackgroundImage(GetMetadataFieldArgs args)
        {
            if (AvailableFields.Contains(MetadataField.BackgroundImage))
            {
                return this.game.LandscapeImage;
            }
            return base.GetCoverImage(args);
        }
    }
}