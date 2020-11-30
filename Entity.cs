using Curiosity.Library;

namespace news_recomendation
{
    [Node]
    public sealed class Entity
    {
        [Key] public string WikidataId { get; set; }
        [Property] public string Label { get; set; }
        [Property] public string WikidataType { get; set; }
    }
}
