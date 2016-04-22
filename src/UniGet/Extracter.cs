﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using ICSharpCode.SharpZipLib.GZip;
using ICSharpCode.SharpZipLib.Tar;

namespace UniGet
{
    internal static class Extracter
    {
        public static void ExtractUnityPackage(string packageFile, string outputDir, Func<string, bool> filter)
        {
            var tempPath = CreateTemporaryDirectory();

            using (var fileStream = new FileStream(packageFile, System.IO.FileMode.Open, FileAccess.Read))
            using (var gzipStream = new GZipInputStream(fileStream))
            {
                var tarArchive = TarArchive.CreateInputTarArchive(gzipStream);
                tarArchive.ExtractContents(tempPath);
                tarArchive.Close();
            }

            var files = new Dictionary<string, string>();
            var folders = new Dictionary<string, string>();

            foreach (var guid in Directory.GetDirectories(tempPath))
            {
                var assetFile = Path.Combine(guid, "asset");
                var assetMetaFile = Path.Combine(guid, "asset.meta");
                var pathNameFile = Path.Combine(guid, "pathname");

                if (File.Exists(pathNameFile) == false)
                    continue;

                var path = File.ReadAllText(pathNameFile).Trim();
                if (File.Exists(assetFile))
                    files.Add(path, assetFile);
                else
                    folders.Add(path, assetFile);
            }

            var folderForAddedFile = new HashSet<string>();
            foreach (var file in files)
            {
                if (file.Key.EndsWith(".unitypackage.json") == false &&
                    filter != null && filter(file.Key) == false)
                {
                    continue;
                }

                // copy file

                var destPath = Path.Combine(outputDir, file.Key);
                var destDirPath = Path.GetDirectoryName(destPath);

                if (Directory.Exists(destDirPath) == false)
                    Directory.CreateDirectory(destDirPath);

                File.Copy(file.Value, destPath, true);
                File.Copy(file.Value + ".meta", destPath + ".meta", true);

                // mark directory for copying directory meta file later

                var dirPath = Path.GetDirectoryName(file.Key).Replace("\\", "/");
                while (string.IsNullOrEmpty(dirPath) == false && folderForAddedFile.Add(dirPath))
                {
                    dirPath = Path.GetDirectoryName(dirPath).Replace("\\", "/");
                }
            }

            foreach (var folder in folderForAddedFile)
            {
                string folderPath;
                if (folders.TryGetValue(folder, out folderPath))
                {
                    var destPath = Path.Combine(outputDir, folder);
                    File.Copy(folderPath + ".meta", destPath + ".meta", true);
                }
            }

            Directory.Delete(tempPath, true);
        }

        public static string CreateTemporaryDirectory()
        {
            var tempDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(tempDirectory);
            return tempDirectory;
        }

        public static Func<string, bool> MakeFilter(IEnumerable<Regex> filters)
        {
            return p => filters.Any(f => f.IsMatch(p));
        }

        public static Func<string, bool> MakeSampleFilter()
        {
            return p => Path.GetDirectoryName(p).ToLower().Contains("sample");
        }

        public static Func<string, bool> MakeFilter(List<string> includes, List<string> excludes)
        {
            var excludeSample = false;
            var idx = excludes.FindIndex(f => f.ToLower() == "$sample$");
            if (idx != -1)
            {
                excludeSample = true;
                excludes = new List<string>(excludes);
                excludes.RemoveAt(idx);
            }

            var excludeSampleFilter = excludeSample ? MakeSampleFilter() : null;
            var excludeFilter = excludes.Any() ? MakeFilter(excludes.Select(f => new Regex(f)).ToList()) : null;
            var includeFilter = includes.Any() ? MakeFilter(includes.Select(f => new Regex(f)).ToList()) : null;

            return s =>
            {
                if (excludeSampleFilter != null && excludeSampleFilter(s))
                    return false;
                if (excludeFilter != null && excludeFilter(s))
                    return false;
                return (includeFilter != null) ? includeFilter(s) : true;
            };
        }
    }
}
