using Markdig;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.FileProviders;

namespace KTrading.Pages.Help
{
    public class IndexModel : PageModel
    {
        private readonly IWebHostEnvironment _env;
        public string HtmlContent { get; set; } = "";

        public IndexModel(IWebHostEnvironment env)
        {
            _env = env;
        }

        public async Task OnGetAsync()
        {
            var file = Path.Combine(_env.ContentRootPath, "Docs", "USER_GUIDE.md");
            if (System.IO.File.Exists(file))
            {
                var md = await System.IO.File.ReadAllTextAsync(file);
                HtmlContent = Markdown.ToHtml(md);
            }
            else
            {
                HtmlContent = "<p>User guide not found.</p>";
            }
        }
    }
}
