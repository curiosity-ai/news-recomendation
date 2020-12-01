using Curiosity.Library;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace news_recomendation
{
    class Program
    {
        static async Task Main(string[] args)
        {
            ForceInvariantCultureAndUTF8Output();

            var (type, server, token) = (args[0], args[1], args[2]);

            MIND.IsLarge = type switch
            {
                "small" => false,
                "large" => true,
                _ => throw new NotSupportedException($"Invalid type: {type}, supported values are 'small' and 'large'")
            };

            await MIND.DownloadAsync();

            using (var graph = Graph.Connect(server, token, "MIND"))
            {
                await graph.CreateNodeSchemaAsync<Article>();
                await graph.CreateNodeSchemaAsync<User>();
                await graph.CreateNodeSchemaAsync<Entity>();
                await graph.CreateNodeSchemaAsync<Category>();
                await graph.CreateNodeSchemaAsync<Subcategory>();

                await graph.CreateEdgeSchemaAsync(Edges.Viewed,         Edges.ViewedBy,
                                                  Edges.CategoryOf,     Edges.HasCategory,
                                                  Edges.SubcategoryOf,  Edges.HasSubcategory,
                                                  Edges.AppearsIn,      Edges.Mentions,
                                                  Edges.Ignored,        Edges.IgnoredBy);

                await IngestMIND(graph);

                await graph.CommitPendingAsync();
            }

            Console.WriteLine("Done !");
        }

        static async Task IngestMIND(Graph graph)
        {
            Console.WriteLine("Reading MIND...");

            var tasks = new List<Task>();

            await foreach (var article in MIND.ReadNewsAsync())
            {
                Console.WriteLine($"Ingesting article {article.Title}\n\t{article.URL}");

                tasks.Add(Task.Run(async () =>
                {
                    var (html, fullText, date) = await MIND.ReadHtmlAsync(article);

                    var articleNode = graph.AddOrUpdate(new Article()
                    {
                        ID = article.ID,
                        Abstract = article.Abstract,
                        Title = article.Title,
                        Url = article.URL,
                        Html = html,
                        FullText = fullText,
                        Timestamp = date ?? DateTimeOffset.UnixEpoch,
                    });

                    var categoryNode = graph.AddOrUpdate(new Category() { Name = article.Category });
                    var subcategoryNode = graph.AddOrUpdate(new Subcategory() { Name = article.SubCategory });

                    graph.Link(articleNode, categoryNode, Edges.HasCategory, Edges.CategoryOf);
                    graph.Link(articleNode, subcategoryNode, Edges.HasSubcategory, Edges.SubcategoryOf);
                    graph.Link(categoryNode, subcategoryNode, Edges.HasSubcategory, Edges.SubcategoryOf);

                    foreach (var entity in article.TitleEntities.Concat(article.AbstractEntities))
                    {
                        var entityNode = graph.AddOrUpdate(new Entity()
                        {
                            WikidataId = entity.WikidataId,
                            WikidataType = entity.Type,
                            Label = entity.Label
                        });

                        graph.Link(articleNode, entityNode, Edges.Mentions, Edges.AppearsIn);

                        foreach (var surfaceForm in entity.SurfaceForms)
                        {
                            graph.AddAlias(entityNode, Mosaik.Core.Language.English, surfaceForm, false);
                        }
                    }
                }));

                if (tasks.Count > 20)
                {
                    await Task.WhenAny(tasks);
                    tasks.RemoveAll(t => t.IsCanceled);
                }
            }

            await Task.WhenAll(tasks);


            int count = 0;

            await foreach (var impression in MIND.ReadImpressionsAsync())
            {
                var userNode = graph.AddOrUpdate(new User() { ID = impression.UserID });

                foreach (var fromHistory in impression.History)
                {
                    var articleNode = Node.Key(nameof(Article), fromHistory);
                    graph.Link(userNode, articleNode, Edges.Viewed, Edges.ViewedBy);
                }

                foreach (var fromImpression in impression.Impressions)
                {
                    var articleNode = Node.Key(nameof(Article), fromImpression.Substring(0, fromImpression.IndexOf('-') - 1));
                    if (fromImpression.EndsWith("-1"))
                    {
                        graph.Link(userNode, articleNode, Edges.Viewed, Edges.ViewedBy);
                    }
                    else
                    {
                        graph.Link(userNode, articleNode, Edges.Ignored, Edges.IgnoredBy);
                    }
                }

                if(++count % 10_000 == 0)
                {
                    Console.WriteLine($"Reading impressions, at {count:n0}");
                    await graph.CommitPendingAsync(); //Avoid operations accumulating up in memory on the connector side
                }
            }

            Console.WriteLine("Finished reading MIND...");
        }


        static void ForceInvariantCultureAndUTF8Output()
        {
            if (Environment.UserInteractive)
            {
                try
                {
                    Console.OutputEncoding = Encoding.UTF8;
                    Console.InputEncoding = Encoding.UTF8;
                }
                catch
                {
                    //This might throw if not running on a console, ignore as we don't care in that case
                }
            }
            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
        }
    }
}
