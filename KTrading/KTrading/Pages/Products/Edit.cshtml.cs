using KTrading.Models;
using KTrading.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using KTrading.Data;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace KTrading.Pages.Products
{
    public class EditModel : PageModel
    {
        private readonly IProductService _service;
        private readonly ApplicationDbContext _db;

        public EditModel(IProductService service, ApplicationDbContext db)
        {
            _service = service;
            _db = db;
        }

        [BindProperty]
        public Product Product { get; set; } = new();

        public IEnumerable<SelectListItem> CategoryList { get; set; } = Array.Empty<SelectListItem>();

        public async Task<IActionResult> OnGetAsync(Guid id)
        {
            var entity = await _service.GetByIdAsync(id);
            if (entity is null) return NotFound();

            Product = entity;
            CategoryList = await _db.ProductCategories.Select(c => new SelectListItem(c.Name, c.Id.ToString())).ToListAsync();
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                CategoryList = await _db.ProductCategories.Select(c => new SelectListItem(c.Name, c.Id.ToString())).ToListAsync();
                return Page();
            }

            await _service.UpdateAsync(Product);
            return RedirectToPage("Index");
        }
    }
}
