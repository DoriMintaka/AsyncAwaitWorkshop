using System.Threading.Tasks;

namespace Application
{
    using AsyncAwaitWorkshop;

    class Program
    {
        static void Main(string[] args)
        {
            PageLoader loader = new PageLoader("https://vk.com/", "C:\\Users\\space\\Desktop\\disa");
            Task.WaitAll(loader.GetAsync());
        }
    }
}
