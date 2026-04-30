using KTrading.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using KTrading.Data;

namespace KTrading.Pages.Customers
{
    public class DeleteModel : PageModel
    {
        private readonly ApplicationDbContext _db;

        public DeleteModel(ApplicationDbContext db)
        {
            _db = db;
        }

        [BindProperty]
        public Customer? Customer { get; set; }

        public async Task<IActionResult> OnGetAsync(Guid id)
        {
            Customer = await _db.Customers.FindAsync(id);
            if (Customer is null) return NotFound();
            return Page();
        }

        public async Task<IActionResult> OnPostAsync(Guid id)
        {
            var entity = await _db.Customers.FindAsync(id);
            if (entity is null) return NotFound();

            _db.Customers.Remove(entity);
            await _db.SaveChangesAsync();

            return RedirectToPage("Index");
        }
    }
}
