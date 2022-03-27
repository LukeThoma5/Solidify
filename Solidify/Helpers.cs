using System.IO;
using System.Linq;

namespace Solidify
{
    public static class Helpers
    {
        public static string CreatePathSafeName(string s)
        {
            var invalids = Path.GetInvalidFileNameChars();
            return new string(s.Select(x =>
                x switch
                {
                    '_' => ' ',
                    _ when invalids.Contains(x) => '_',
                    _ => x
                }).ToArray());
        }

        public static string GetFileName(string title, string fileName)
        {
            if (fileName.Contains("_answ") || fileName.Contains("_disc") || fileName.Contains("_tran"))
            {
                return fileName.Replace("html", "md");
            }

            return title + ".md";
        }

        public static void EnsureCreated(DirectoryInfo info)
        {
            if (!info.Exists)
            {
                info.Create();
            }
        }
    }
}