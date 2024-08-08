using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Shoko.Plugin.Abstractions;
using Shoko.Plugin.Abstractions.Attributes;
using Shoko.Plugin.Abstractions.DataModels;

namespace SubRenamer
{
    [Renamer(nameof(SubRenamer))]
    public class SubRenamer : IRenamer
    {
        private readonly ILogger<SubRenamer> _logger;

        public SubRenamer(ILogger<SubRenamer> logger)
        {
            _logger = logger;
        }

        public string GetFilename(MoveEventArgs args)
        {
            _logger.LogInformation("GetFilename");

            var finalName = "";

            try
            {
                var extension = Path.GetExtension(args.File.FileName);
                if (args.Series.Count == 0) throw new Exception("Anime not identified");
                var anime = args.Series[0];

                var type = anime.Type;
                if (args.Episodes.Count == 0) throw new Exception("Episode not identified");
                var episode = args.Episodes[0];
                var episodeCount = episode.Series?.EpisodeCounts.Episodes ?? 0; //anime.EpisodeCounts.Episodes;

                var isSpecial = episode.Type != EpisodeType.Episode;
                
                if (episodeCount == 1 && !isSpecial)
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

                    finalName += $"{prefix}{episode.EpisodeNumber.PadZeroes(episodeCount)} - ";
                    var title = episode.Titles.FirstOrDefault(t => t.Language == TitleLanguage.English)?.Title ??
                                episode.Titles.FirstOrDefault(t => t.Language == TitleLanguage.Romaji)?.Title ?? "";

                    finalName += title;
                }

                var groupName = "";
                
                try
                {
                    groupName = args.Video.AniDB?.ReleaseGroup.ShortName;
                    groupName = groupName != null ? $" [{groupName}]" : "";
                }
                catch
                {
                    _logger.LogInformation("Release group unknown");
                }

                finalName += groupName;
                finalName = (finalName + extension).RemoveInvalidPathCharacters();
                _logger.LogInformation("FinalName = {finalName}", finalName);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "GetFilename error");
                throw;
            }

            return finalName;
        }

        public (IImportFolder destination, string subfolder) GetDestination(MoveEventArgs args)
        {
            _logger.LogInformation("GetDestination");

            /*
             * dropFolder\type\romajiName [year]
             */

            IImportFolder dropFolder = null;
            var finalDest = "";
            try
            {
                dropFolder = args.AvailableFolders.First();
                try
                {
                    dropFolder = args.AvailableFolders.First(f => f.DropFolderType == DropFolderType.Destination);
                }
                catch (InvalidOperationException)
                {
                    _logger.LogError(
                        "No import folders configured as drop source, picking first import folder as destination.");
                }

                if (args.Series.Count == 0) throw new Exception("Anime not identified");
                // first determine anime type
                var anime = args.Series[0];
                // we use type as string
                var type = anime.Type.ToString();

                // now we determine anime name to use
                var romajiName = anime.PreferredTitle.RemoveInvalidPathCharacters();

                // and finally year
                var year = anime.AirDate.HasValue ? anime.AirDate.Value.Year.ToString() : "";

                finalDest = Path.Combine(type,
                    string.IsNullOrEmpty(year) ? $"{romajiName}" : $"{romajiName} [{year}]");
                _logger.LogInformation("FinalDest = {finalDest}", finalDest);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "GetDestination error");
                throw;
            }

            return (dropFolder, finalDest);
        }
    }
}