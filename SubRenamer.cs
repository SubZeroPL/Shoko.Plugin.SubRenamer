using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Shoko.Plugin.Abstractions;
using NLog;
using Shoko.Plugin.Abstractions.DataModels;

namespace Shoko.Plugins.SubRenamer
{
    public class SubRenamer : IRenamer
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        public void Load()
        {
            Logger.Info("Plugin loaded");
        }

        public void OnSettingsLoaded(IPluginSettings settings)
        {
            Logger.Info("Settings loaded");
        }

        public string Name => "SubRenamer";

        public void GetFilename(RenameEventArgs args)
        {
            Logger.Info("GetFilename");

            var finalName = "";

            var extension = Path.GetExtension(args.FileInfo.Filename);
            var anime = args.AnimeInfo.First();
            if (anime == null)
                return;

            var type = anime.Type;
            var episode = args.EpisodeInfo.First();
            if (episode == null)
                return;

            var isSpecial = episode.Type != EpisodeType.Episode;

            if (anime.EpisodeCounts.Episodes == 1 && !isSpecial)
            {
                var title = episode.Titles.First(t => t.Language == TitleLanguage.English).Title ??
                            episode.Titles.First(t => t.Language == TitleLanguage.Romaji).Title ?? "";
                if (Regex.IsMatch(title, "Episode \\d+"))
                {
                    title = type == AnimeType.Movie ? "Complete Movie" : anime.PreferredTitle;
                }

                finalName += title;
            }
            else
            {
                var prefix = "";
                if (isSpecial)
                {
                    prefix = episode.Type.ToString().Substring(0, 1);
                }

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

            args.Result = finalName;
        }

        public void GetDestination(MoveEventArgs args)
        {
            Logger.Info("GetDestination");

            /*
             * dropFolder\type\romajiName [year]
             */

            // there is no configuration so for now we use first drop folder as destination
            IImportFolder dropFolder = args.AvailableFolders.First();
            args.DestinationImportFolder = dropFolder;

            // first determine anime type
            IAnime anime = args.AnimeInfo.First();
            // we use type as string
            var type = anime.Type.ToString();

            // now we determine anime name to use
            var romajiName = anime.PreferredTitle.RemoveInvalidPathCharacters();

            // and finally year
            String year = anime.AirDate.HasValue ? anime.AirDate.Value.Year.ToString() : 2020.ToString();

            String finalDest = Path.Combine(type, $"{romajiName} [{year}]");
            Logger.Info($"FinalDest = {finalDest}");

            args.DestinationPath = finalDest;
        }
    }
}