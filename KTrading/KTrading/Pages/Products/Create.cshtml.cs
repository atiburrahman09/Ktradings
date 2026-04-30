using KTrading.Models;
using KTrading.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Authorization;
using KTrading.Data;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace KTrading.Pages.Products
{
    [Authorize(Policy = "RequireAdminOrSales")]
    public class CreateModel : PageModel
    {
        private readonly IProductService _service;
        private readonly ApplicationDbContext _db;

        public CreateModel(IProductService service, ApplicationDbContext db)
        {
            _service = service;
            _db = db;
        }

        [BindProperty]
        public Product Product { get; set; } = new();

        public IEnumerable<SelectListItem> CategoryList { get; set; } = Array.Empty<SelectListItem>();

        public async Task OnGetAsync()
        {
            CategoryList = await _db.ProductCategories.Select(c => new SelectListItem(c.Name, c.Id.ToString())).ToListAsync();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                CategoryList = await _db.ProductCategories.Select(c => new SelectListItem(c.Name, c.Id.ToString())).ToListAsync();
                return Page();
            }

            await _service.AddAsync(Product);
            return RedirectToPage("Index");
        }
    }
}
