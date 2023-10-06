using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NLog;
using Shoko.Plugin.Abstractions;
using Shoko.Plugin.Abstractions.Attributes;
using Shoko.Plugin.Abstractions.DataModels;

namespace Shoko.Plugin.SubRenamer
{
    [Renamer("SubRenamer")]
    public class SubRenamer : IRenamer
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        public string GetFilename(RenameEventArgs args)
        {
            Logger.Info("GetFilename");

            var finalName = "";

            var extension = Path.GetExtension(args.FileInfo.Filename);
            var anime = args.AnimeInfo.First();
            if (anime == null)
                return null;

            var type = anime.Type;
            var episode = args.EpisodeInfo.First();
            if (episode == null)
                return null;

            var isSpecial = episode.Type != EpisodeType.Episode;

            if (anime.EpisodeCounts.Episodes == 1 && !isSpecial)
            {
                var title = episode.Titles.First(t => t.Language == TitleLanguage.English).Title ??
                            episode.Titles.First(t => t.Language == TitleLanguage.Romaji).Title ?? "";
                if (Regex.IsMatch(title, "Episode \\d+"))
                    title = type == AnimeType.Movie ? "Complete Movie" : anime.PreferredTitle;

                finalName += title;
            }
            else
            {
                var prefix = "";
                if (isSpecial) prefix = episode.Type.ToString()[..1];

                finalName += $"{prefix}{episode.Number.PadZeroes(anime.EpisodeCounts.Episodes)} - ";
                var title = episode.Titles.First(t => t.Language == TitleLanguage.English).Title ??
                            episode.Titles.First(t => t.Language == TitleLanguage.Romaji).Title ?? "";

                finalName += title;
            }

            var groupName = args.FileInfo.AniDBFileInfo?.ReleaseGroup?.ShortName;

            groupName = groupName != null ? $" [{groupName}]" : "";

            finalName += groupName;

            finalName = (finalName + extension).RemoveInvalidPathCharacters();
            Logger.Info($"FinalName = {finalName}");

            return finalName;
        }

        public (IImportFolder destination, string subfolder) GetDestination(MoveEventArgs args)
        {
            Logger.Info("GetDestination");

            /*
             * dropFolder\type\romajiName [year]
             */

            var dropFolder = args.AvailableFolders.First();
            try
            {
                dropFolder = args.AvailableFolders.First(f => f.DropFolderType == DropFolderType.Destination);
            }
            catch (InvalidOperationException)
            {
                Logger.Error("No import folders configured as drop source, picking first import folder as destination.");
            }


            // first determine anime type
            var anime = args.AnimeInfo.First();
            // we use type as string
            var type = anime.Type.ToString();

            // now we determine anime name to use
            var romajiName = anime.PreferredTitle.RemoveInvalidPathCharacters();

            // and finally year
            var year = anime.AirDate.HasValue ? anime.AirDate.Value.Year.ToString() : "";

            var finalDest = Path.Combine(type, string.IsNullOrEmpty(year) ? $"{romajiName}" : $"{romajiName} [{year}]");
            Logger.Info($"FinalDest = {finalDest}");

            return (dropFolder, finalDest);
        }
    }
}