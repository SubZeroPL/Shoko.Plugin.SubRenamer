using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Shoko.Plugin.Abstractions;
using Shoko.Plugin.Abstractions.Attributes;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.Abstractions.Events;

namespace SubRenamer
{
    [RenamerID("SubRenamer")]
    // ReSharper disable once UnusedType.Global
    public class SubRenamer : IRenamer
    {
        private readonly ILogger<SubRenamer> _logger;

        public string Name => "SubRenamer";
        public string Description => "SubRenamer";
        public bool SupportsMoving => true;
        public bool SupportsRenaming => true;

        public SubRenamer(ILogger<SubRenamer> logger)
        {
            _logger = logger;
        }

        private (RelocationError, string) GetFilename(RelocationEventArgs args)
        {
            _logger.LogInformation("GetFilename");

            var finalName = "";

            var extension = Path.GetExtension(args.File.FileName);
            if (args.Series.Count == 0)
            {
                args.Cancel = true;
                return (new RelocationError("Anime not identified"), null);
            }

            var anime = args.Series[0];

            var type = anime.Type;
            if (args.Episodes.Count == 0)
            {
                args.Cancel = true;
                return (new RelocationError("Episode not identified"), null);
            }

            var episode = args.Episodes[0];

            var isSpecial = episode.Type != EpisodeType.Episode;

            if (anime.EpisodeCounts.Episodes == 1 && !isSpecial)
            {
                var title = episode.Titles.FirstOrDefault(t => t.Language == TitleLanguage.English)?.Title ??
                            episode.Titles.FirstOrDefault(t => t.Language == TitleLanguage.Romaji)?.Title ?? "";
                if (Regex.IsMatch(title, "Episode \\d+"))
                    title = type == AnimeType.Movie ? "Complete Movie" : anime.PreferredTitle;

                finalName += title;
            }
            else
            {
                var prefix = "";
                if (isSpecial) prefix = episode.Type.ToString()[..1];

                finalName += $"{prefix}{episode.EpisodeNumber.PadZeroes(anime.EpisodeCounts.Episodes)} - ";
                var title = episode.Titles.FirstOrDefault(t => t.Language == TitleLanguage.English)?.Title ??
                            episode.Titles.FirstOrDefault(t => t.Language == TitleLanguage.Romaji)?.Title ?? "";

                finalName += title;
            }

            var groupName = "";

            try
            {
                groupName = args.Episodes[0].AnidbEpisode.VideoList[0].AniDB?.ReleaseGroup.ShortName;
                groupName = groupName != null ? $" [{groupName}]" : "";
            }
            catch
            {
                _logger.LogInformation("Release group unknown");
            }

            finalName += groupName;

            finalName = (finalName + extension).RemoveInvalidPathCharacters();
            _logger.LogInformation("FinalName = {finalName}", finalName);

            return (null, finalName);
        }

        private (RelocationError, string) GetDestination(RelocationEventArgs args)
        {
            _logger.LogInformation("GetDestination");

            /*
             * dropFolder\type\romajiName [year]
             */

            if (args.Series.Count == 0)
            {
                args.Cancel = true;
                return (new RelocationError("Anime not identified"), null);
            }

            // first determine anime type
            var anime = args.Series[0];
            // we use type as string
            var type = anime.Type.ToString();

            // now we determine anime name to use
            var romajiName = anime.PreferredTitle.RemoveInvalidPathCharacters();

            // and finally year
            var year = anime.AirDate.HasValue ? anime.AirDate.Value.Year.ToString() : "";

            var finalDest = Path.Combine(type,
                string.IsNullOrEmpty(year) ? $"{romajiName}" : $"{romajiName} [{year}]");
            _logger.LogInformation("FinalDest = {finalDest}", finalDest);

            return (null, finalDest);
        }

        private IImportFolder GetImportFolder(RelocationEventArgs args)
        {
            var dropFolder =
                args.AvailableFolders.FirstOrDefault(a => a.DropFolderType.HasFlag(DropFolderType.Destination));
            if (dropFolder != null) return dropFolder;

            _logger.LogError(
                "No import folders configured as drop source, picking first import folder as destination.");
            dropFolder = args.AvailableFolders[0];

            return dropFolder;
        }

        public RelocationResult GetNewPath(RelocationEventArgs args)
        {
            try
            {
                var result = new RelocationResult
                {
                    DestinationImportFolder = GetImportFolder(args)
                };
                (result.Error, result.FileName) = GetFilename(args);
                if (result.Error != null)
                    return result;
                (result.Error, result.Path) = GetDestination(args);
                return result;
            }
            catch (Exception e)
            {
                return new RelocationResult { Error = new RelocationError(e.Message, e) };
            }
        }
    }
}