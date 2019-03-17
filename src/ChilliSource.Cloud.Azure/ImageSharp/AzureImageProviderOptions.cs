#if !NET_4X
using ChilliSource.Cloud.Azure;
using System;
using System.Collections.Generic;

namespace ChilliSource.Cloud.Azure.ImageSharp
{
    public class AzureImageProviderOptions
    {
        public AzureStorageConfiguration StorageConfiguration { get; set; }
        public string UrlPrefix { get; set; }
    }
}
#endif