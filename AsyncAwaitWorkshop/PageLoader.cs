namespace AsyncAwaitWorkshop
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.IO;
    using System.Net;
    using System.Net.Http;
    using System.Threading.Tasks;

    using CsQuery;

    using MoreLinq;

    public class PageLoader
    {
        private readonly Uri baseUrl;

        private readonly DirectoryInfo directory;

        private readonly PageLoaderSettings settings;

        private readonly List<string> visited;

        public PageLoader(string startFrom, string saveTo)
        {
            this.baseUrl = new Uri(startFrom);
            this.directory = new DirectoryInfo(saveTo);
            if (!this.directory.Exists)
            {
                this.directory.Create();
            }

            this.visited = new List<string>();
        }

        public PageLoader(string startFrom, string saveTo, PageLoaderSettings settings)
            : this(startFrom, saveTo)
        {
            this.settings = settings;
        }

        public async Task GetAsync()
        {
            await this.GetAsync(this.baseUrl);
        }

        private async Task GetAsync(Uri uri)
        {
            if (uri == null)
            {
                return;
            }

            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            
            using (HttpClient client = new HttpClient())
            using (HttpResponseMessage response = await client.GetAsync(uri))
            using (HttpContent content = response.Content)
            {
                string result = await content.ReadAsStringAsync();
                if (result != null)
                {
                    using (var streamWriter = File.CreateText($"{this.directory.FullName}\\" +
                                                              DateTime.Now.ToString("yyyyMMddHHmmss") +
                                                              $"{DateTime.Now.Millisecond}.html"))
                    {
                        await streamWriter.WriteAsync(result);
                    }

                    var cq = CQ.CreateDocument(result);
                    foreach (var c in cq.Find("a").Each(c =>
                        {
                            if (c.GetAttribute("href") == null)
                            {
                                return;
                            }

                            var index = c.GetAttribute("href").IndexOf('#');
                            if (index != -1)
                            {
                                c.SetAttribute("href", c.GetAttribute("href").Substring(0, index));
                            }
                        }).DistinctBy(c => c.GetAttribute("href")))
                    {
                        var newUri = this.UriFromString(uri, c.GetAttribute("href"));
                        if (newUri == null || this.visited.Contains(newUri.AbsolutePath))
                        {
                            continue;
                        }

                        this.visited.Add(newUri.AbsolutePath);
                        await this.GetAsync(newUri);
                    }
                }
            }
        }

        private Uri UriFromString(Uri uriFrom, string link)
        {
            if (link == null)
            {
                return null;
            }

            if (Uri.IsWellFormedUriString(link, UriKind.Absolute))
            {
                return new Uri(link);
            }

            if (link.StartsWith("//"))
            {
                return new Uri($"{uriFrom.Scheme}:{link}");
            }

            if (link.StartsWith("/"))
            {
                return new Uri(uriFrom, link.Remove(0, 1));
            }

            return null;
        }
    }
}
