public class ElasticHealthResponse
        {
            public string Status { get; set; } // Green, Yellow, Red
            public string StatusDescription { get; set; }
            public int NodeCount { get; set; }
            public bool IsHealthy { get; set; }
            public DateTime Timestamp { get; set; }
        }


