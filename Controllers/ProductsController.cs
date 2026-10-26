using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WholesaleRetailStore.Data;
using WholesaleRetailStore.Services;

namespace WholesaleRetailStore.Controllers
{
    /// <summary>
    /// REST API for the product catalogue.
    /// </summary>
    [ApiController]
    public class ProductsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ProductSearchService _search;

        public ProductsController(AppDbContext context, ProductSearchService search)
        {
            _context = context;
            _search = search;
        }

        /// <summary>
        /// GET: api/products
        /// Returns a list of all products
        /// </summary>
        [HttpGet("api/products")]
        public async Task<IActionResult> GetProducts()
        {
            var products = await _context.Products
                .AsNoTracking()
                .OrderBy(p => p.Name)
                .Select(p => new { p.Id, p.Name, p.Description, p.Stock, p.BasePrice })
                .ToListAsync();

            return Ok(products);
        }

        /// <summary>
        /// GET: api/products/search?q=mlk
        /// Typo-tolerant search. If the query had typos the response carries the corrected query
        /// ("did you mean") and the results are for the corrected words. Pass autoCorrect=false
        /// to search for exactly what was typed.
        /// </summary>
        /// <param name="q">What the user typed.</param>
        /// <param name="autoCorrect">Fix typos before searching (default true).</param>
        [HttpGet("api/products/search")]
        public async Task<IActionResult> SearchProducts([FromQuery] string? q, [FromQuery] bool autoCorrect = true)
        {
            // The catalogue is small, so it is searched in memory. For a large catalogue this
            // would become a SQL pre-filter plus ranking.
            var products = await _context.Products.AsNoTracking().OrderBy(p => p.Name).ToListAsync();
            var result = _search.Search(products, q, autoCorrect);

            return Ok(new
            {
                result.Query,
                result.CorrectedQuery,
                Products = result.Products.Select(p => new { p.Id, p.Name, p.Description, p.Stock, p.BasePrice })
            });
        }

        /// <summary>
        /// GET: api/products/{id}
        /// Returns a specific product by ID
        /// </summary>
        [HttpGet("api/products/{id:int}")]
        public async Task<IActionResult> GetProduct(int id)
        {
            var product = await _context.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);
            if (product == null)
            {
                return NotFound(new { message = "Product not found" });
            }

            return Ok(new { product.Id, product.Name, product.Description, product.Stock, product.BasePrice });
        }

        /// <summary>
        /// GET: api/products/stock/{id}
        /// Returns stock information for a specific product
        /// </summary>
        [HttpGet("api/products/stock/{id:int}")]
        public async Task<IActionResult> GetProductStock(int id)
        {
            var product = await _context.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);
            if (product == null)
            {
                return NotFound(new { message = "Product not found" });
            }

            return Ok(new
            {
                ProductId = product.Id,
                ProductName = product.Name,
                product.Stock,
                IsAvailable = product.Stock > 0,
                StockStatus = product.Stock > 10 ? "In Stock" :
                              product.Stock > 0 ? "Low Stock" : "Out of Stock"
            });
        }
    }
}
