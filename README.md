# Wholesale Retail Store

An ASP.NET Core 8 API with a React (TypeScript, Tailwind) storefront. Prices change automatically with the customer type: retail customers pay the listed price, wholesale customers get a per-item discount and a volume discount that stack. Search forgives typos ("mlk" finds "milk").

## Quick start

You need the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0), [Node.js 20+](https://nodejs.org/), and SQL Server LocalDB or Express (LocalDB ships with Visual Studio). Nothing paid or cloud-based is involved.

```bash
# 1. Build the React app (the .NET app serves it from frontend/build)
cd frontend
npm install
npm run build
cd ..

# 2. Run the app (http://localhost:5076)
dotnet run --launch-profile http
```

In Development the app creates the `WholesaleRetailStoreDb` database on LocalDB, applies the migrations and loads the seed data at startup. Swagger is at <http://localhost:5076/swagger>.

To use SQL Server Express instead, change `ConnectionStrings:DefaultConnection` in `appsettings.json`. To apply migrations by hand: `dotnet tool restore` then `dotnet ef database update`.

**Frontend development with hot reload:** keep `dotnet run` going and, in `frontend/`, run `npm run dev` (Vite on <http://localhost:5173>, proxying `/api` to port 5076).

**Tests:** `dotnet test` (pricing rules, order placement against an in-memory SQLite database, and search).

## Pricing rules

Rules live in the `PricingRules` table and are matched on customer type. There are two kinds, and they **stack**:

| Kind | Rule has... | Effect |
|---|---|---|
| Per-unit discount | no minimum order amount | Reduces every unit price. The highest active one wins. |
| Volume discount | a minimum order amount | Once the subtotal (after per-unit discounts) reaches the minimum, the highest qualifying one is taken off the subtotal. |

Seeded rules (wholesale only): 10% off every unit, plus a further 15% off orders from R500.

Example, a wholesale customer buying 6 Smart Watches at R199.99:

| | |
|---|---|
| List price (6 x 199.99) | R1 199.94 |
| 10% per-unit discount (unit price 179.99) | -R120.00 |
| Subtotal | R1 079.94 |
| 15% volume discount | -R161.99 |
| **Total** | **R917.95** |

Unit prices are rounded to cents, so quantity x unit price always equals the line total. Orders store the volume discount in `Orders.DiscountAmount`, so `sum(items) - DiscountAmount = TotalAmount` always holds. The quote and the cart show which rules were applied, and how much more to spend to reach the next tier.

## Search

`GET /api/products/search?q=mlk` compares each word you type with the words used in the catalogue. A word that already matches is left alone. Otherwise it is replaced by the closest catalogue word (by edit distance, allowing swapped neighbours, or by being the same word with letters missing). The response carries `correctedQuery` ("did you mean") and the results for it. The UI shows "Showing results for **milk**. Search instead for "mlk"", like a search engine; `autoCorrect=false` searches exactly what was typed. There is no external or AI service; the logic is in `Services/ProductSearchService.cs`.

## Database

Five normalised tables, defined in `Data/AppDbContext.cs` and the migrations:

- **Products**: Name, Description, Stock, BasePrice
- **Customers**: Name, Email (unique), CustomerType (Retail or Wholesale)
- **PricingRules**: CustomerType, DiscountPercentage, MinimumOrderAmount (optional), IsActive, Description
- **Orders**: CustomerId, CreatedAt, TotalAmount, DiscountAmount, DiscountDescription, Status
- **OrderItems**: OrderId, ProductId, Quantity, UnitPrice (the price paid at the time)

Seed data: 6 products, 2 retail and 2 wholesale customers, and the 2 active pricing rules above.

## Orders and stock

`POST /api/orders` prices the basket, takes the stock and saves the order in one transaction. Stock is taken with a guarded `UPDATE ... WHERE Stock >= qty`, so two simultaneous orders cannot both take the last unit, and a failure leaves no order and no stock change. Repeated products are merged, and quantities must be positive. Cancelling an order returns its stock; reopening it takes the stock again, or fails if it has been sold in the meantime. Problems come back as 400 with a `message`.

## API

| Method | Route | Purpose |
|---|---|---|
| GET | `/api/products` | List products |
| GET | `/api/products/search?q=&autoCorrect=` | Typo-tolerant search |
| GET | `/api/products/{id}`, `/api/products/stock/{id}` | One product, stock status |
| GET, POST | `/api/customers` | List, create (409 if the email exists) |
| GET, PUT, DELETE | `/api/customers/{id}` | Read, update, delete (only without orders) |
| POST | `/api/pricing/quote` | Quote with every applied rule |
| GET | `/api/pricing/quote/{customerId}?productId=&quantity=` | Same, via query string |
| GET | `/api/pricing/rules` | Active rules |
| GET | `/api/pricing/products/{customerId}` | Unit prices for one customer |
| POST | `/api/pricing/compare` | Retail vs wholesale unit prices |
| GET, POST | `/api/orders` | List, place an order |
| GET | `/api/orders/{id}` | One order |
| PUT | `/api/orders/{id}/status` | Set status by name (`Shipped`, ...) |

```bash
curl -X POST http://localhost:5076/api/pricing/quote \
  -H "Content-Type: application/json" \
  -d '{"customerId":3,"items":[{"productId":2,"quantity":6}]}'
```

## The UI

A single-page React app: pick or create a customer, browse and search products with prices for that customer, add to the cart, and check out. The cart asks the server for a quote whenever it changes, so switching customer re-prices it and the summary shows the discounts applied. The Orders page lists orders with their breakdown and lets you change their status. The palette is defined once in `frontend/src/index.css`.

## Project layout

```
Controllers/   API controllers
Services/      PricingService, OrderService, ProductSearchService
Models/        Entities, quote types, DTOs
Data/          AppDbContext and seed data
Migrations/    EF Core migrations
frontend/      React app (Vite); builds into frontend/build
WholesaleRetailStore.Tests/   xUnit tests
```

## Known limitations

- There is no authentication; anyone can read or change any order or customer.
- Orders are listed without paging, and search loads the whole catalogue (fine for a small catalogue).
