namespace AsyncAwaitWorkshop
{
    using System;
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

        public enum DomainRestriction
        {
            NoRestrictions, SameDomainOnly, SubdomainsOnly
        }

        public PageLoader(string startFrom, string saveTo)
            : this(startFrom, saveTo, null, -1, DomainRestriction.NoRestrictions)
        {
        }

        public PageLoader(string startFrom, string saveTo, string extensions, int levels, DomainRestriction restriction)
        {
            this.baseUrl = new Uri(startFrom);
            this.directory = new DirectoryInfo(saveTo);
            if (!this.directory.Exists)
            {
                this.directory.Create();
            }

            this.visited = new List<string>();

            this.settings = new PageLoaderSettings(this.baseUrl, extensions, levels, restriction);
        }

        public async Task GetAsync()
        {
            await this.GetAsync(this.baseUrl, 0);
        }

        private async Task GetAsync(Uri uri, int currentLevel)
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

                    if (currentLevel == this.settings.Levels)
                    {
                        return;
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
                        if (newUri == null || !this.settings.Validate(newUri) || this.visited.Contains(newUri.AbsoluteUri))
                        {
                            continue;
                        }

                        if (newUri.IsFile)
                        {
                            await this.GetFileAsync(newUri);
                            continue;
                        }

                        this.visited.Add(newUri.AbsoluteUri);
                        Console.WriteLine($"Processing {newUri.AbsoluteUri}");
                        await this.GetAsync(newUri, currentLevel + 1);
                    }
                }
            }
        }

        private async Task GetFileAsync(Uri uri)
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
                var result = await content.ReadAsByteArrayAsync();
                using (var sr = File.Create($"{this.directory.FullName}\\" +
                                            DateTime.Now.ToString("yyyyMMddHHmmss") +
                                            $"{DateTime.Now.Millisecond}." +
                                            uri.AbsoluteUri.Substring(uri.AbsoluteUri.LastIndexOf('.'))))
                {
                    await sr.WriteAsync(result, 0, result.Length);
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

        private class PageLoaderSettings
        {
            private readonly Uri baseUri;

            private Predicate<Uri> extensionValidator;

            private Predicate<Uri> domainValidator;

            public PageLoaderSettings(Uri baseUri, string extensions, int levels, DomainRestriction restrictions)
            {
                this.baseUri = baseUri;
                this.ConfigureExtensionValidator(extensions);
                this.ConfigureDomainValidator(restrictions);
                this.Levels = levels < 0 ? int.MaxValue : levels;
            }

            public int Levels { get; }

            public bool Validate(Uri uri)
            {
                return this.extensionValidator(uri) && this.domainValidator(uri);
            }

            private void ConfigureExtensionValidator(string extensions)
            {
                if (extensions == null)
                {
                    this.extensionValidator += u => true;
                    return;
                }

                var parsed = extensions.Split(',');
                for (int i = 0; i < parsed.Length; i++)
                {
                    parsed[i] = parsed[i].Trim();
                }

                this.extensionValidator += e =>
                    {
                        foreach (string s in parsed)
                        {
                            if (!e.IsFile || e.AbsoluteUri.EndsWith(s))
                            {
                                return true;
                            }
                        }

                        return false;
                    };
            }

            private void ConfigureDomainValidator(DomainRestriction restrictions)
            {
                if (restrictions == DomainRestriction.SameDomainOnly)
                {
                    this.domainValidator += u => this.baseUri.Host == u.Host;
                    return;
                }

                if (restrictions == DomainRestriction.SubdomainsOnly)
                {
                    this.domainValidator += u => this.baseUri.IsBaseOf(u);
                    return;
                }

                this.domainValidator += u => true;
            }
        }
    }
}
