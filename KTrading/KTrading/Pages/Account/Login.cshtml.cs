using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace KTrading.Pages.Account
{
    public class LoginModel : PageModel
    {
        private readonly SignInManager<IdentityUser> _signInManager;

        public LoginModel(SignInManager<IdentityUser> signInManager)
        {
            _signInManager = signInManager;
        }

        [BindProperty]
        public InputModel Input { get; set; } = new InputModel();

        public string? ReturnUrl { get; set; }

        public class InputModel
        {
            public string Email { get; set; } = "";
            public string Password { get; set; } = "";
            public bool RememberMe { get; set; }
        }

        public IActionResult OnGet(string? returnUrl = null)
        {
            if (User?.Identity?.IsAuthenticated ?? false)
            {
                return RedirectToPage("/Index");
            }

            ReturnUrl = returnUrl;
            return Page();
        }

        public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
        {
            if (!ModelState.IsValid) return Page();
            var res = await _signInManager.PasswordSignInAsync(Input.Email, Input.Password, Input.RememberMe, lockoutOnFailure: false);
            if (res.Succeeded)
            {
                if (!string.IsNullOrEmpty(returnUrl)) return LocalRedirect(returnUrl);
                return RedirectToPage("/Index");
            }
            ModelState.AddModelError(string.Empty, "Invalid login attempt.");
            return Page();
        }
    }
}
