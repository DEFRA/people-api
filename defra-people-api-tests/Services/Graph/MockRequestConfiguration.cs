using System.Collections.Generic;
using Microsoft.Graph.Users;
using Microsoft.Kiota.Abstractions;

namespace defra.people_api_tests.Services.Graph;

/// <summary>
/// Mock implementation of RequestConfiguration for testing
/// </summary>
public class MockRequestConfiguration
{
    public RequestHeaders Headers { get; set; } = new RequestHeaders();
    public MockQueryParameters QueryParameters { get; set; } = new MockQueryParameters();
    
    public class MockQueryParameters
    {
        public bool Count { get; set; }
        public string[]? Select { get; set; }
        public string[]? Expand { get; set; }
    }
}
