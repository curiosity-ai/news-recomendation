# [Graph-base News Recommendation](https://theolivenbaum.medium.com/building-a-news-recommendation-engine-using-curiosity-24c004d9458b) 🚀

This repository contains the source code presented in the [Building a news recommendation engine](https://theolivenbaum.medium.com/building-a-news-recommendation-engine-using-curiosity-24c004d9458b) article. It uses [Curiosity's](https://curiosity.ai) AI-powered search and it's [data connector](https://www.nuget.org/packages/Curiosity.Library) library to show how one can easily get started to build a few news recomendation algorithms, using graph embeddings and graph queries, to suggest recommended news.

![Graphs](https://raw.githubusercontent.com/curiosity-ai/news-recomendation/main/media/predictions.gif)

The data used for this demo was made available by Microsoft as part of their [MIND project](https://azure.microsoft.com/en-us/services/open-datasets/catalog/microsoft-news-dataset/) - more information about it can also be found on the [related paper](https://msnews.github.io/assets/doc/ACL2020_MIND.pdf). 

Check more details on our accompanying [blog post](https://theolivenbaum.medium.com/building-a-news-recommendation-engine-using-curiosity-24c004d9458b).

## Running Curiosity Locally

[Check our documentation](https://docs.curiosity.ai/en/articles/4449019-installation) to install a free instance of Curiosity on your computer or clould environment of preference.

Once you have your Curiosity instance up and running, check the [initial setup guide](https://docs.curiosity.ai/en/articles/4452603-initial-setup) and then you'll be ready to use the code in this repository.

## Data Ingestion

The code in this repository will automatically download the MIND dataset and ingest all news and interatctions to your Curiosity instance.

You'll need to generate an API token for your system, and pass it to the connector. Check the [documentation on how to create an API token](https://docs.curiosity.ai/en/articles/4453131-external-data-connectors).

```bash
git clone https://github.com/curiosity-ai/news-recomendation
cd news-recomendation
dotnet run small {SERVER_URL} {AUTH_TOKEN}
```

The first argument must be either `small` or `large`, and that defines if you'll download the small (about 30MB compressed) or the large (about 500MB compressed) MIND datasets. You need to replace `{SERVER_URL}` with the address your server is listing to (usually `http://localhost:8080` if you're running it locally), and `{AUTH_TOKEN}` with the API token generated earlier. 
