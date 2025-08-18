
// using Nest;
// using System;

// public static class ElasticClientProvider
// {
//     private static ElasticClient _client;   //DI

//     public static ElasticClient GetClient()
//     {
//         if (_client != null) return _client;

//         var settings = new ConnectionSettings(new Uri("http://localhost:9200")) //default docker port
//         .DefaultIndex("loggingapi-{0:yyyy-MM-dd}");  //default index name

//         _client = new ElasticClient(settings);
//         return _client;
//     }
// }