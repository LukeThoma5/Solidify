using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Solidify;

var sourcePath = args[0];

var sourceInfo = new FileInfo(sourcePath);

if (!sourceInfo.Exists)
{
    throw new Exception("File does not exist");
}

await using var fileContents = sourceInfo.OpenRead();

var root = new DirectoryInfo(sourceInfo.Directory + "/" +
                             Helpers.CreatePathSafeName(sourceInfo.Name.Split(".")[0]));
Helpers.EnsureCreated(root);

if (sourceInfo.Name.EndsWith(".epub"))
{
    await EpubSaver.SaveEpubAsync(fileContents, root);
}
else if (sourceInfo.Name.EndsWith(".zip"))
{
    using var archive = new ZipArchive(fileContents, ZipArchiveMode.Read);

    foreach (var item in archive.Entries.ToList())
    {
        if (item.FullName.EndsWith(".epub"))
        {
            var weekRootPath = root.FullName + "/" + Helpers.CreatePathSafeName(item.Name.Split(".")[0]);
            var weekRoot = new DirectoryInfo(weekRootPath);
            Helpers.EnsureCreated(weekRoot);
            try
            {
                await EpubSaver.SaveEpubAsync(item.Open(), weekRoot);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Failed to process file " + item.FullName + " " + ex.Message);
            }
        }
    }
}
else
{
    throw new Exception("Invalid file type, expecting .epub or .zip");
}