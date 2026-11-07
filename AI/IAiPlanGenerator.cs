using System.Threading.Tasks;
using Task2_AiTestRunner.Models;

namespace Task2_AiTestRunner.AI
{
    public interface IAiPlanGenerator
    {
        Task<string> GenerateJsonPlanAsync(string testCase, string targetUrl);
    }
}

