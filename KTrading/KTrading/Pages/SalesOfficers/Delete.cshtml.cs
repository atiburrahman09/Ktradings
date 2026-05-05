using KTrading.Data;
using KTrading.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace KTrading.Pages.SalesOfficers
{
    public class DeleteModel : PageModel
    {
        private readonly ApplicationDbContext _db;

        public DeleteModel(ApplicationDbContext db)
        {
            _db = db;
        }

        public SalesOfficer? SalesOfficer { get; set; }

        public async Task<IActionResult> OnGetAsync(Guid id)
        {
            SalesOfficer = await _db.SalesOfficers.FindAsync(id);
            if (SalesOfficer is null) return NotFound();

            return Page();
        }

        public async Task<IActionResult> OnPostAsync(Guid id)
        {
            var officer = await _db.SalesOfficers.FindAsync(id);
            if (officer is null) return NotFound();

            var isUsed = await _db.SalesOrders.AnyAsync(o => o.SalesOfficerId == id);
            if (isUsed)
            {
                officer.IsActive = false;
            }
            else
            {
                _db.SalesOfficers.Remove(officer);
            }

            await _db.SaveChangesAsync();
            return RedirectToPage("Index");
        }
    }
}
