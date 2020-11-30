using Curiosity.Library;
using System;

namespace news_recomendation
{
    [Node]
    public sealed class Article
    {
        [Key] public string ID { get; set; }
        [Property] public string Title { get; set; }
        [Property] public string Abstract { get; set; }
        [Property] public string Url { get; set; }
        [Property] public string Html { get; set; }
        [Property] public string FullText { get; set; }
        [Timestamp] public DateTimeOffset Timestamp { get; set; }
    }
}
