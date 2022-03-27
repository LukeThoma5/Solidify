using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using HtmlAgilityPack;

namespace Solidify
{
    public static class EpubSaver
    {
        public static async Task SaveEpubAsync(Stream stream, DirectoryInfo saveRoot)
        {
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read);

            var regex = new Regex(@"u\d\d\d");

            foreach (var item in archive.Entries.ToList())
            {
                if (item.FullName.EndsWith(".html") && !item.FullName.Contains("_desc") && regex.IsMatch(item.Name))
                {
                    await SaveEntryAsMarkdownAsync(item, saveRoot);
                }

                if (item.FullName.Contains("OPS/assets") && !string.IsNullOrWhiteSpace(item.Name) &&
                    !item.Name.EndsWith(".css", StringComparison.InvariantCultureIgnoreCase))
                {
                    var dir = saveRoot.FullName + "/assets/";
                    Helpers.EnsureCreated(new DirectoryInfo(dir));
                    await using var fs = new FileStream(dir + item.Name, FileMode.OpenOrCreate);
                    await item.Open().CopyToAsync(fs);
                    await fs.FlushAsync();
                }
            }
        }

        private static string ConvertHtmlToMarkdown(HtmlDocument document)
        {
            var builder = new StringBuilder();

            void VisitElement(HtmlNode node)
            {
                if (node.NodeType == HtmlNodeType.Text && !string.IsNullOrWhiteSpace(node.InnerText))
                {
                    var text = node.InnerText.Trim().Replace("\n", " ");
                    while (text.Contains("  "))
                    {
                        text = text.Replace("  ", " ");
                    }

                    builder.Append(text);
                }

                if (node.NodeType != HtmlNodeType.Element)
                {
                    return;
                }

                var tag = node.Name.ToLower();
                switch (tag)
                {
                    case "h1":
                        builder.AppendLine("# " + node.InnerText);
                        break;

                    case "h2":
                        builder.AppendLine("## " + node.InnerText);
                        break;

                    case "h3":
                        builder.AppendLine("### " + node.InnerText);
                        break;

                    case "h4":
                        builder.AppendLine("#### " + node.InnerText);
                        break;

                    case "h5":
                        builder.AppendLine("##### " + node.InnerText);
                        break;

                    case "h6":
                        builder.AppendLine("###### " + node.InnerText);
                        break;

                    case "p":
                        builder.Append('\n');
                        VisitContainer(node);
                        builder.Append('\n');
                        break;

                    case "div":
                        VisitContainer(node);
                        break;

                    case "sup":
                        builder.Append(" <sup>");
                        VisitContainer(node);
                        builder.Append(" </sup>");
                        break;

                    case "img":
                        builder.Append($"![{node.GetAttributeValue("alt", "")}]({node.GetAttributeValue("src", "")})");
                        break;

                    case "a":
                        builder.Append(" [");
                        VisitContainer(node);
                        builder.Append($"]({node.GetAttributeValue("href", "")})");
                        break;

                    case "span":
                    case "li":
                        VisitContainer(node);
                        break;

                    case "dfn":
                        builder.Append(" [[");
                        VisitContainer(node);
                        builder.Append("]] ");
                        break;

                    case "b":
                    case "strong":
                        builder.Append("**");
                        VisitContainer(node);
                        builder.Append("** ");
                        break;

                    case "i":
                    case "em":
                        builder.Append('*');
                        VisitContainer(node);
                        builder.Append("* ");
                        break;

                    case "ol":
                    case "ul":
                        var count = 1;
                        builder.Append("\n");
                        foreach (var childNode in node.ChildNodes)
                        {
                            if (!childNode.Name.Equals("li", StringComparison.InvariantCultureIgnoreCase))
                            {
                                continue;
                            }

                            builder.Append(node.Name.Equals("ol", StringComparison.InvariantCultureIgnoreCase)
                                ? $"{count}. "
                                : "- ");

                            VisitElement(childNode);
                            builder.Append('\n');
                        }

                        builder.Append("\n");
                        break;

                    default:
                        throw new Exception($"Unknown tag type {tag}");
                }
            }

            void VisitContainer(HtmlNode container)
            {
                foreach (var child in container.ChildNodes)
                {
                    VisitElement(child);
                }
            }

            var body = document.DocumentNode.SelectSingleNode("//body")
                       ?? throw new Exception("Body not found");
            VisitContainer(body);

            return builder.ToString();
        }

        private static async Task SaveEntryAsMarkdownAsync(ZipArchiveEntry page, DirectoryInfo saveRoot)
        {
            var content = await new StreamReader(page.Open()).ReadToEndAsync();
            var document = new HtmlDocument();
            document.LoadHtml(content);

            var title = document.DocumentNode.SelectSingleNode("//title");
            var markdown = ConvertHtmlToMarkdown(document);

            await using var fs = new FileStream(
                saveRoot.FullName + $"/{Helpers.CreatePathSafeName(title.InnerText)}.md",
                FileMode.OpenOrCreate);
            await fs.WriteAsync(Encoding.UTF8.GetBytes(markdown));
            await fs.FlushAsync();
        }
    }
}