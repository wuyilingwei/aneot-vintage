using System.ServiceModel.Syndication;
using System.Xml;
using System.Text;
using AnEoT.Vintage.Helpers.Custom;

namespace AnEoT.Vintage.Helpers;

/// <summary>
/// 协助生成源的类
/// </summary>
public static class FeedGenerationHelper
{
    private const string LxgwFontUri = "https://unpkg.com/lxgw-wenkai-screen-webfont@1.6.0/style.css";

    /// <summary>
    /// 生成 RSS 与 Atom 源
    /// </summary>
    /// <param name="rssBaseUri">RSS 源的基 Uri</param>
    /// <param name="webRootPath">“wwwroot”文件夹所在路径</param>
    /// <param name="includeAllArticles">确定 RSS 源是否包含全部文章信息的值</param>
    /// <param name="generateDigest">指示生成模式是否为生成摘要的值</param>
    /// <param name="rss20FileName">RSS 2.0 源的文件名</param>
    /// <param name="atomFileName">ATOM 源的文件名</param>
    /// <param name="addCssStyle">确定 RSS 源是否包含 CSS 样式的值</param>
    public static void GenerateFeed(string rssBaseUri, string webRootPath, bool includeAllArticles = false, bool generateDigest = false, bool addCssStyle = false, string rss20FileName = "rss.xml", string atomFileName = "atom.xml")
    {
        if (string.IsNullOrWhiteSpace(rssBaseUri))
        {
            throw new ArgumentException($"“{nameof(rssBaseUri)}”不能为 null 或空白。", nameof(rssBaseUri));
        }

        if (string.IsNullOrWhiteSpace(webRootPath))
        {
            throw new ArgumentException($"“{nameof(webRootPath)}”不能为 null 或空白。", nameof(webRootPath));
        }

        Console.WriteLine("正在生成 RSS 源...");

        if (rssBaseUri.EndsWith('/'))
        {
            rssBaseUri = rssBaseUri[..^1];
        }

        Uri baseUri = new(rssBaseUri);
        #region 第一步：生成RSS源信息
        string title = includeAllArticles switch
        {
            true when generateDigest => "回归线简易版 - 完整源摘要",
            true => "回归线简易版 - 完整源",
            false when generateDigest => "回归线简易版 - 摘要",
            false => "回归线简易版"
        };

        SyndicationFeed feed = new(
           title,
           "Another End of Terra",
           baseUri,
           "AnEoT-Vintage",
           DateTimeOffset.Now)
        {
            Copyright = new TextSyndicationContent("泰拉创作者联合会保留所有权利 | Copyright © 2022-2025 TCA. All rights reserved."),
            Language = "zh-CN",
            Generator = "System.ServiceModel.Syndication.SyndicationFeed, used in AnEoT.Vintage",
            ImageUrl = new Uri(baseUri, "favicon.ico"),
        };

        IEnumerable<string> categories = CategoryAndTagHelper.GetAllCategories(webRootPath);
        foreach (string item in categories)
        {
            feed.Categories.Add(new SyndicationCategory(item));
        }

        //获取 posts 文件夹的信息
        DirectoryInfo postsDirectoryInfo = new(Path.Combine(webRootPath, "posts"));

        List<SyndicationItem> items = [];

        //反向读取文件夹，以获取到最新的期刊
        List<DirectoryInfo> volDirInfos = [.. postsDirectoryInfo.EnumerateDirectories()];
        volDirInfos.Sort(new VolumeOrderComparer());
        volDirInfos.Reverse();

        IEnumerable<DirectoryInfo> targetDirectories;
        if (includeAllArticles)
        {
            targetDirectories = volDirInfos;
        }
        else
        {
            //我们只获取前两个文件夹的信息，避免RSS源过长
            targetDirectories = volDirInfos.Take(2);
        }

        foreach (DirectoryInfo volDirInfo in targetDirectories)
        {
            IOrderedEnumerable<FileInfo> articles = volDirInfo
                                .EnumerateFiles("*.md")
                                .Where(file => !file.Name.Contains("README.md"))
                                .Order(new ArticleOrderComparer());
            foreach (FileInfo article in articles)
            {
                string markdown = File.ReadAllText(article.FullName);
                ArticleInfo articleInfo = MarkdownHelper.GetFromFrontMatter<ArticleInfo>(markdown);
                string articleLink = $"{rssBaseUri}/posts/{volDirInfo.Name}/{article.Name.Replace(".md", ".html")}";

                TextSyndicationContent content;
                CustomMarkdownParser parser = new(false, false, true, $"{rssBaseUri}/posts/{volDirInfo.Name}", true, true);

                if (generateDigest)
                {
                    string quote = MarkdownHelper.GetArticleQuote(markdown);
                    string bodyContent = parser.Parse(quote);

                    string htmlContentString;
                    if (string.IsNullOrWhiteSpace(bodyContent))
                    {
                        htmlContentString = $"""<a href="{articleLink}" target="_blank">请单击这里阅读全文......</a>""";
                    }
                    else
                    {
                        bodyContent = $"""
                            {bodyContent}
                            <br />
                            <a href="{articleLink}" target="_blank">请单击这里阅读全文......</a>
                            """;

                        if (addCssStyle)
                        {
                            htmlContentString = $"""
                        <head>
                            <link href="{rssBaseUri}/css/site.css" rel="stylesheet" type="text/css" />
                            <link href="{rssBaseUri}/css/index.css" rel="stylesheet" type="text/css" />
                            <link href="{rssBaseUri}/css/palette.css" rel="stylesheet" type="text/css" />
                            <link href="{rssBaseUri}/css/rss-style.css" rel="stylesheet" type="text/css" />
                            <link href="{LxgwFontUri}" rel="stylesheet" type="text/css" />
                        </head>
                        <body>
                            {bodyContent}
                        </body>
                        """;
                        }
                        else
                        {
                            htmlContentString = bodyContent;
                        }
                    }

                    content = SyndicationContent.CreateHtmlContent(htmlContentString);
                }
                else
                {
                    string html = parser.Parse(markdown);

                    if (addCssStyle)
                    {
                        html = $"""
                      <head>
                          <link href="{rssBaseUri}/css/site.css" rel="stylesheet" type="text/css" />
                          <link href="{rssBaseUri}/css/index.css" rel="stylesheet" type="text/css" />
                          <link href="{rssBaseUri}/css/palette.css" rel="stylesheet" type="text/css" />
                          <link href="{rssBaseUri}/css/rss-style.css" rel="stylesheet" type="text/css" />
                          <link href="{LxgwFontUri}" rel="stylesheet" type="text/css" />
                      </head>
                      <body>
                          {html}
                      </body>
                      """;
                    }

                    content = SyndicationContent.CreateHtmlContent(html);
                }

                bool hasDate = DateTimeOffset.TryParse(articleInfo.Date, out DateTimeOffset publishDate);
                DateTimeOffset timeNow = DateTimeOffset.Now;

                SyndicationItem item = new(
                    articleInfo.Title,
                    content,
                    new Uri(articleLink, UriKind.Absolute),
                    articleLink,
                    hasDate ? publishDate : timeNow);

                item.Authors.Add(new SyndicationPerson() { Name = articleInfo.Author });

                foreach (string category in articleInfo.Category ?? [])
                {
                    item.Categories.Add(new SyndicationCategory(category));
                }

                if (hasDate)
                {
                    item.PublishDate = publishDate;
                }
                else
                {
                    item.PublishDate = timeNow;
                }

                items.Add(item);
            }
        }

        feed.Items = items;
        #endregion

        #region 第二步：将RSS源信息序列化为XML文件
        XmlWriterSettings settings = new()
        {
            Encoding = Encoding.UTF8,
            NewLineHandling = NewLineHandling.Entitize,
            NewLineOnAttributes = true,
            Indent = true
        };


        string atomFilePath = Path.Combine(webRootPath, atomFileName);
        if (Path.Exists(atomFilePath))
        {
            File.Delete(atomFilePath);
        }

        XmlWriter atomWriter = XmlWriter.Create(atomFilePath, settings);
        Atom10FeedFormatter atomFormatter = new(feed);
        atomFormatter.WriteTo(atomWriter);
        atomWriter.Close();

        string rssFilePath = Path.Combine(webRootPath, rss20FileName);
        if (Path.Exists(rssFilePath))
        {
            File.Delete(rssFilePath);
        }

        XmlWriter rssWriter = XmlWriter.Create(rssFilePath, settings);
        Rss20FeedFormatter rssFormatter = new(feed);
        rssFormatter.WriteTo(rssWriter);
        rssWriter.Close();
        #endregion

        Console.WriteLine("RSS 源生成完成！");
        Console.WriteLine($"已在以下路径生成\nRSS：{rssFilePath}\nAtom：{atomFilePath}");
    }
}

/// <summary>
/// 为表示文章的<see cref="FileInfo"/>提供比较方法，以用于排序
/// </summary>
file sealed class ArticleOrderComparer : Comparer<FileInfo>
{
    /// <inheritdoc/>
    public override int Compare(FileInfo? x, FileInfo? y)
    {
        if (object.ReferenceEquals(x, y))
        {
            return 0;
        }

        if (x is null)
        {
            return -1;
        }
        else if (y is null)
        {
            return 1;
        }

        string markdownX = File.ReadAllText(x.FullName);
        string markdownY = File.ReadAllText(y.FullName);

        ArticleInfo articleInfoX = MarkdownHelper.GetFromFrontMatter<ArticleInfo>(markdownX);
        ArticleInfo articleInfoY = MarkdownHelper.GetFromFrontMatter<ArticleInfo>(markdownY);

        if (articleInfoX.Order > 0 && articleInfoY.Order > 0)
        {
            return Comparer<int>.Default.Compare(articleInfoX.Order, articleInfoY.Order);
        }
        else if (articleInfoX.Order > 0 && articleInfoY.Order < 0)
        {
            return -1;
        }
        else if (articleInfoX.Order < 0 && articleInfoY.Order > 0)
        {
            return 1;
        }
        else
        {
            return Comparer<int>.Default.Compare(-articleInfoX.Order, -articleInfoY.Order);
        }
    }
}

internal sealed class VolumeOrderComparer : IComparer<DirectoryInfo>
{
    public int Compare(DirectoryInfo? x, DirectoryInfo? y)
    {
        if (object.ReferenceEquals(x, y))
        {
            return 0;
        }

        if (x is null)
        {
            return -1;
        }
        else if (y is null)
        {
            return 1;
        }

        return string.CompareOrdinal(x.Name, y.Name);
    }
}
