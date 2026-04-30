using KTrading.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using KTrading.Data;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace KTrading.Pages.ProductReturns
{
    public class CreateModel : PageModel
    {
        private readonly ApplicationDbContext _db;

        public CreateModel(ApplicationDbContext db)
        {
            _db = db;
        }

        [BindProperty]
        public ProductReturn Return { get; set; } = new();

        [BindProperty]
        public List<ProductReturnItem> Items { get; set; } = new();

        public IEnumerable<SelectListItem> Customers { get; set; } = Array.Empty<SelectListItem>();
        public List<KTrading.Models.Product> ProductsFull { get; set; } = new();

        public async Task OnGetAsync()
        {
            Customers = await _db.Customers.Select(c => new SelectListItem(c.Name, c.Id.ToString())).ToListAsync();
            ProductsFull = await _db.Products.ToListAsync();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid) return Page();

            if (Return.Id == Guid.Empty) Return.Id = Guid.NewGuid();
            Return.CreatedAt = DateTimeOffset.UtcNow;

            _db.ProductReturns.Add(Return);
            if(Items.Any()){
                foreach(var it in Items){
                    it.Id = Guid.NewGuid();
                    it.ProductReturnId = Return.Id;
                }
                _db.ProductReturnItems.AddRange(Items);
            }
            await _db.SaveChangesAsync();

            return RedirectToPage("Index");
        }
    }
}
