using KTrading.Data;
using KTrading.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace KTrading.Pages.SalesOfficers
{
    public class CreateModel : PageModel
    {
        private readonly ApplicationDbContext _db;

        public CreateModel(ApplicationDbContext db)
        {
            _db = db;
        }

        [BindProperty]
        public SalesOfficer SalesOfficer { get; set; } = new();

        public void OnGet()
        {
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid) return Page();

            SalesOfficer.Id = Guid.NewGuid();
            SalesOfficer.CreatedAt = DateTimeOffset.UtcNow;
            _db.SalesOfficers.Add(SalesOfficer);
            await _db.SaveChangesAsync();

            return RedirectToPage("Index");
        }
    }
}
