using KTrading.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using KTrading.Data;

namespace KTrading.Pages.Customers
{
    public class EditModel : PageModel
    {
        private readonly ApplicationDbContext _db;

        public EditModel(ApplicationDbContext db)
        {
            _db = db;
        }

        [BindProperty]
        public Customer Customer { get; set; } = new();

        public async Task<IActionResult> OnGetAsync(Guid id)
        {
            var entity = await _db.Customers.FindAsync(id);
            if (entity is null) return NotFound();
            Customer = entity;
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid) return Page();

            _db.Customers.Update(Customer);
            await _db.SaveChangesAsync();

            return RedirectToPage("Index");
        }
    }
}
