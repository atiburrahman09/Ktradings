using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace KTrading.Pages.Diagnostics
{
    public class AdminCheckModel : PageModel
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly IWebHostEnvironment _env;

        public AdminCheckModel(UserManager<IdentityUser> userManager, IWebHostEnvironment env)
        {
            _userManager = userManager;
            _env = env;
        }

        public string AdminEmail => "admin@ktrading.local";
        public bool IsDevelopment => _env.IsDevelopment();
        public bool UserExists { get; set; }
        public string? UserName { get; set; }
        public bool EmailConfirmed { get; set; }
        public bool HasPassword { get; set; }

        public async Task OnGetAsync()
        {
            if (!IsDevelopment) return;

            var user = await _userManager.FindByEmailAsync(AdminEmail);
            UserExists = user != null;
            if (user != null)
            {
                UserName = user.UserName;
                EmailConfirmed = user.EmailConfirmed;
                HasPassword = await _userManager.HasPasswordAsync(user);
            }
        }
    }
}
