
using Nest;
using System;

public static class ElasticClientProvider
{
    private static ElasticClient _client;

    public static ElasticClient GetClient()
    {
        if (_client != null) return _client;

        var settings = new ConnectionSettings(new Uri("http://localhost:9200")) //default docker port
        .DefaultIndex("logs");  //default index name

        _client = new ElasticClient(settings);
        return _client;
    }
}