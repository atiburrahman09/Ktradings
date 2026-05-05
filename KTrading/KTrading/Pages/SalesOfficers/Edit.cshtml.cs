using KTrading.Data;
using KTrading.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace KTrading.Pages.SalesOfficers
{
    public class EditModel : PageModel
    {
        private readonly ApplicationDbContext _db;

        public EditModel(ApplicationDbContext db)
        {
            _db = db;
        }

        [BindProperty]
        public SalesOfficer SalesOfficer { get; set; } = new();

        public async Task<IActionResult> OnGetAsync(Guid id)
        {
            var officer = await _db.SalesOfficers.FindAsync(id);
            if (officer is null) return NotFound();

            SalesOfficer = officer;
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid) return Page();

            var officer = await _db.SalesOfficers.FindAsync(SalesOfficer.Id);
            if (officer is null) return NotFound();

            officer.Code = SalesOfficer.Code;
            officer.Name = SalesOfficer.Name;
            officer.Phone = SalesOfficer.Phone;
            officer.IsActive = SalesOfficer.IsActive;
            await _db.SaveChangesAsync();

            return RedirectToPage("Index");
        }
    }
}
