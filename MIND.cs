using CsvHelper;
using CsvHelper.Configuration;
using HtmlAgilityPack;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using UID;

namespace news_recomendation
{
    public static class MIND
    {
        public record Impression(string ID, string UserID, DateTime Time, string[] History, string[] Impressions);
        public record NewsArticle(string ID, string Category, string SubCategory, string Title, string Abstract, string URL, Entity[] TitleEntities, Entity[] AbstractEntities);
        public record Entity(string Label, string Type, string WikidataId, float Confidence, int[] OccurrenceOffsets, string[] SurfaceForms);

        const string BaseUrl = "https://mind201910small.blob.core.windows.net/release/";
        const string SmallTraining   = "MINDsmall_train.zip";
        const string SmallValidation = "MINDsmall_dev.zip";
        const string LargeTraining   = "MINDlarge_train.zip";
        const string LargeValidation = "MINDlarge_dev.zip";
        const string DocTypeJson     = "https://raw.githubusercontent.com/msnews/MIND/master/crawler/doc_type.json";
        const string DataFolder = ".data";
        const string CacheFolder = ".cache";

        static HttpClientHandler _handler = new HttpClientHandler();

        public static bool IsLarge { get; set; }  = false;
        private static Dictionary<string, string> DocTypes;

        public static async Task DownloadAsync()
        {
            await (IsLarge ? DownloadLargeAsync() : DownloadSmallAsync());
        }

        static async Task DownloadLargeAsync()
        {
            await Task.WhenAll(DownloadFileAsync(BaseUrl + LargeTraining, DataFolder),
                               DownloadFileAsync(BaseUrl + LargeValidation, DataFolder),
                               DownloadDocTypes());
        }

        static async Task DownloadSmallAsync()
        {
            await Task.WhenAll(DownloadFileAsync(BaseUrl + SmallTraining,   DataFolder),
                               DownloadFileAsync(BaseUrl + SmallValidation, DataFolder),
                               DownloadDocTypes());
        }

        static async Task DownloadDocTypes()
        {
            await DownloadFileAsync(DocTypeJson, DataFolder);
            DocTypes = JsonConvert.DeserializeObject<Dictionary<string, string>>(await File.ReadAllTextAsync(Path.Combine(DataFolder, "doc_type.json")));
        }

        public static async IAsyncEnumerable<Impression> ReadImpressionsAsync()
        {
            using var file   = File.OpenRead(Path.Combine(DataFolder, IsLarge ? LargeTraining : SmallTraining));
            using var zip    = new ZipArchive(file, ZipArchiveMode.Read);
            using var stream = zip.Entries.First(e => e.Name == "behaviors.tsv").Open();
            var csv          = new CsvReader(new StreamReader(stream), new CsvConfiguration(CultureInfo.InvariantCulture) { Delimiter = "\t", IgnoreQuotes = true });

            while(await csv.ReadAsync())
            {
                yield return new Impression(ID:          csv.GetField(0), 
                                            UserID:      csv.GetField(1),
                                            Time:        csv.GetField<DateTime>(2),
                                            History:     csv.GetField(3)?.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries) ?? Array.Empty<string>(), 
                                            Impressions: csv.GetField(4)?.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries) ?? Array.Empty<string>());
            }
        }

        public static async IAsyncEnumerable<NewsArticle> ReadNewsAsync()
        {
            using var file   = File.OpenRead(Path.Combine(DataFolder, IsLarge ? LargeTraining : SmallTraining));
            using var zip    = new ZipArchive(file, ZipArchiveMode.Read);
            using var stream = zip.Entries.First(e => e.Name == "news.tsv").Open();
            var csv          = new CsvReader(new StreamReader(stream), new CsvConfiguration(CultureInfo.InvariantCulture) { Delimiter = "\t", IgnoreQuotes = true });

            while(await csv.ReadAsync())
            {
                yield return new NewsArticle(ID:          csv.GetField(0),
                                             Category:    csv.GetField(1),
                                             SubCategory: csv.GetField(2),
                                             Title:       csv.GetField(3),
                                             Abstract:    csv.GetField(4),
                                             URL:         csv.GetField(5),
                                             TitleEntities:    JsonConvert.DeserializeObject<Entity[]>(csv.GetField(6)),
                                             AbstractEntities: JsonConvert.DeserializeObject<Entity[]>(csv.GetField(7)));
            }
        }

        static async Task DownloadFileAsync(string url, string folder)
        {
            if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

            var filename = Path.GetFileName(url);
            var downloadTo = Path.Combine(folder, filename);

            if (File.Exists(downloadTo))
            {
                return;
            }
            else
            {
                using (var client = new HttpClient(_handler, disposeHandler: false))
                {
                    client.Timeout = TimeSpan.FromMinutes(10);
                    using var fileStream = File.OpenWrite(downloadTo);
                    using var stream = await client.GetStreamAsync(url);
                    await CopyWithProgressAsync(stream, fileStream, filename);
                }
            }
        }

        private static async Task CopyWithProgressAsync(Stream from, Stream to, string name)
        {
            var totalRead = 0L;
            var totalReads = 0L;
            var buffer = new byte[65536];
            var isMoreToRead = true;

            do
            {
                var read = await from.ReadAsync(buffer, 0, buffer.Length);
                if (read == 0)
                {
                    isMoreToRead = false;
                }
                else
                {
                    await to.WriteAsync(buffer, 0, read);

                    totalRead += read;
                    totalReads += 1;

                    if (totalReads % 64 == 0)
                    {
                        Console.WriteLine($"[{name}] At {totalRead/1024/1024:n1} MB");
                    }
                }
            } while (isMoreToRead);
        }

        public static async Task<(string html, string fullText, DateTimeOffset? date)> ReadHtmlAsync(NewsArticle article)
        {
            var nid = Path.GetFileNameWithoutExtension(article.URL);
            var nidType = DocTypes[nid];

            try
            {
                var page = await GetPage(article.URL, nid);
                var html = page.DocumentNode.InnerHtml;
                

                HtmlNodeCollection nodes = null;

                
                switch (nidType)
                {
                    case "ar": nodes = page.DocumentNode.SelectNodes("//p//text()"); break;
                    case "ss": nodes = page.DocumentNode.SelectNodes("//div[@class='gallery-caption-text']//text()"); break;
                    case "vi": nodes = page.DocumentNode.SelectNodes("//div[@class='video-description']//text()"); break;
                }
                
                var fullText = nodes is not null ? string.Join(" ", nodes.Select(p => p.InnerText)) : "";
                fullText = WebUtility.HtmlDecode(fullText.Trim(new char[] { ' ', '\r', '\n' }).Replace("  ", " "));

                var date = DateTimeOffset.ParseExact(page.DocumentNode.SelectSingleNode("//span[@class='date']").InnerText.Trim(new char[] {' ', '\r', '\n' }), "M/d/yyyy", CultureInfo.GetCultureInfo("en-us"));

                return (html, fullText, date);
            }
            catch(Exception E)
            {
                return (null, null, null);
            }
        }

        static async Task<HtmlDocument> GetPage(string url, string nid)
        {
            var doc = new HtmlDocument();

            if (!Directory.Exists(CacheFolder)) Directory.CreateDirectory(CacheFolder);


            var cached = Path.Combine(CacheFolder, nid + ".html");

            if (File.Exists(cached))
            {
                doc.LoadHtml(await File.ReadAllTextAsync(cached));
            }
            else
            {
                using (var client = new HttpClient(_handler, disposeHandler: false))
                {
                    client.Timeout = TimeSpan.FromMinutes(10);
                    var html = await client.GetStringAsync(url);
                    await File.WriteAllTextAsync(cached, html);
                    doc.LoadHtml(html);
                }
            }
            return doc;
        }
    }
}
