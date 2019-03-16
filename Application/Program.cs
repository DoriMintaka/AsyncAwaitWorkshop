using System.Threading.Tasks;

namespace Application
{
    using System;

    using AsyncAwaitWorkshop;

    class Program
    {
        static void Main(string[] args)
        {
            string address = args[0];
            string location = args[1];

            try
            {
                PageLoader loader;
                if (args.Length == 2)
                {
                    loader = new PageLoader(address, location);
                }
                else
                {
                    string extensions = null;
                    int maxLevel = -1;
                    PageLoader.DomainRestriction r = 0;
                    for (int i = 2; i < args.Length; i++)
                    {
                        var s = args[i].Split('=');
                        switch (s[0])
                        {
                            case "extensions":
                                extensions = s[1];
                                break;
                            case "maxlevel":
                                int.TryParse(s[1], out maxLevel);
                                break;
                            case "restrictions":
                                switch (s[1])
                                {
                                    case "norestrictions":
                                        r = PageLoader.DomainRestriction.NoRestrictions;
                                        break;
                                    case "samedomain":
                                        r = PageLoader.DomainRestriction.SameDomainOnly;
                                        break;
                                    case "subdomains":
                                        r = PageLoader.DomainRestriction.SubdomainsOnly;
                                        break;
                                    default:
                                        Console.WriteLine("Restrictions argument cannot be {0}", s[1]);
                                        return;
                                }

                                break;
                            default:
                                Console.WriteLine($"There's no argument with name {s[0]}");
                                return;
                        }
                    }

                    loader = new PageLoader(address, location, extensions, maxLevel, r);
                    Task.WaitAll(loader.GetAsync());
                }
            }
            catch (UnauthorizedAccessException)
            {
                Console.WriteLine("Access denied to the folder specified. Try another location.");
            }
            catch (UriFormatException)
            {
                Console.WriteLine("Something wrong with the url.");
            }
            catch (Exception)
            {
                Console.WriteLine("Something went wrong. Try again.");
            }
        }
    }
}
