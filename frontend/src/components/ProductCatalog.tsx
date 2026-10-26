import { useEffect, useMemo, useState } from 'react';
import { ChevronDown, Minus, Plus, ShoppingCart as CartIcon, Tag } from 'lucide-react';
import type { CartItem, CustomerType, PricingRule, Product, ProductPrice } from '../types';
import { ApiService, PricingUtils } from '../services/api';
import SearchBar from './SearchBar';

interface ProductCatalogProps {
  customerId: number | null;
  customerType: CustomerType | null;
  cartItems: CartItem[];
  onSetQuantity: (product: Product, quantity: number) => void;
}

type SortKey = 'relevance' | 'name' | 'price-low' | 'price-high' | 'stock';

const ProductCatalog: React.FC<ProductCatalogProps> = ({ customerId, customerType, cartItems, onSetQuantity }) => {
  const [products, setProducts] = useState<Product[]>([]);
  const [correctedQuery, setCorrectedQuery] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState(false);

  const [query, setQuery] = useState('');
  const [autoCorrect, setAutoCorrect] = useState(true);
  const [inStockOnly, setInStockOnly] = useState(false);
  const [sortBy, setSortBy] = useState<SortKey>('relevance');

  const [unitPrices, setUnitPrices] = useState<Map<number, number>>(new Map());
  const [pricedFor, setPricedFor] = useState<number | null>(null);
  const [rules, setRules] = useState<PricingRule[]>([]);

  // Search runs on the server so the typo correction lives in one place. Debounced, and a
  // response for an older query is ignored if a newer one is already on its way.
  useEffect(() => {
    let stale = false;
    const timer = setTimeout(async () => {
      try {
        const result = await ApiService.searchProducts(query, autoCorrect);
        if (!stale) {
          setProducts(result.products);
          setCorrectedQuery(result.correctedQuery);
          setLoadError(false);
        }
      } catch (error) {
        console.error('Failed to load products:', error);
        if (!stale) setLoadError(true);
      } finally {
        if (!stale) setLoading(false);
      }
    }, query ? 250 : 0);

    return () => {
      stale = true;
      clearTimeout(timer);
    };
  }, [query, autoCorrect]);

  useEffect(() => {
    ApiService.getPricingRules().then(setRules).catch(error => console.error('Failed to load rules:', error));
  }, []);

  useEffect(() => {
    if (customerId === null) {
      setUnitPrices(new Map());
      setPricedFor(null);
      return;
    }

    let stale = false;
    ApiService.getProductPrices(customerId)
      .then((prices: ProductPrice[]) => {
        if (!stale) {
          setUnitPrices(new Map(prices.map(p => [p.id, p.calculatedPrice])));
          setPricedFor(customerId);
        }
      })
      .catch(error => console.error('Failed to load prices:', error));

    return () => {
      stale = true;
    };
  }, [customerId]);

  const handleQueryChange = (value: string) => {
    setQuery(value);
    setAutoCorrect(true);
  };

  // Until this customer's prices arrive the list price is shown dimmed, so it never looks final.
  const pricesReady = customerId === null || pricedFor === customerId;

  const visible = useMemo(() => {
    const list = inStockOnly ? products.filter(p => p.stock > 0) : [...products];
    const price = (p: Product) => unitPrices.get(p.id) ?? p.basePrice;
    switch (sortBy) {
      case 'name': return list.sort((a, b) => a.name.localeCompare(b.name));
      case 'price-low': return list.sort((a, b) => price(a) - price(b));
      case 'price-high': return list.sort((a, b) => price(b) - price(a));
      case 'stock': return list.sort((a, b) => b.stock - a.stock);
      default: return list; // relevance: the server's order
    }
  }, [products, inStockOnly, sortBy, unitPrices]);

  const quantityInCart = (productId: number) =>
    cartItems.find(i => i.product.id === productId)?.quantity ?? 0;

  // The volume tiers that apply to this customer type, shown so the discount isn't a surprise.
  const volumeRules = rules
    .filter(r => r.customerType === customerType && r.minimumOrderAmount !== null)
    .sort((a, b) => (a.minimumOrderAmount ?? 0) - (b.minimumOrderAmount ?? 0));

  if (loading) {
    return (
      <div className="flex items-center justify-center gap-3 py-12" role="status">
        <div className="h-8 w-8 animate-spin rounded-full border-4 border-mist border-t-moss" />
        <span className="text-moss">Loading products...</span>
      </div>
    );
  }

  return (
    <div className="space-y-6">
      {volumeRules.length > 0 && (
        <div className="flex items-start gap-3 rounded-lg bg-wheat p-4">
          <Tag className="mt-0.5 h-5 w-5 shrink-0" aria-hidden />
          <p className="text-sm">
            <strong>Volume discounts stack on top of your prices:</strong>{' '}
            {volumeRules
              .map(r => `${r.discountPercentage}% off orders from ${PricingUtils.formatCurrency(r.minimumOrderAmount!)}`)
              .join('; ')}
            .
          </p>
        </div>
      )}

      <div className="space-y-3">
        <div className="max-w-2xl">
          <SearchBar value={query} onChange={handleQueryChange} />
        </div>

        <div className="flex flex-wrap items-center justify-between gap-3">
          <div className="text-sm text-moss" aria-live="polite">
            {correctedQuery && autoCorrect ? (
              <>
                Showing results for <strong className="text-bark">{correctedQuery}</strong>.{' '}
                <button type="button" className="underline hover:text-bark" onClick={() => setAutoCorrect(false)}>
                  Search instead for &ldquo;{query.trim()}&rdquo;
                </button>
              </>
            ) : query && !autoCorrect ? (
              <>
                Showing exact results for <strong className="text-bark">{query.trim()}</strong>.{' '}
                <button type="button" className="underline hover:text-bark" onClick={() => setAutoCorrect(true)}>
                  Use suggestions
                </button>
              </>
            ) : query ? (
              `${visible.length} result${visible.length === 1 ? '' : 's'}`
            ) : (
              `${visible.length} products`
            )}
          </div>

          <div className="flex items-center gap-4">
            <div className="relative">
              <label htmlFor="sort" className="sr-only">Sort by</label>
              <select
                id="sort"
                value={sortBy}
                onChange={e => setSortBy(e.target.value as SortKey)}
                className="field appearance-none pr-9"
              >
                <option value="relevance">{query ? 'Best match' : 'Default order'}</option>
                <option value="name">Name</option>
                <option value="price-low">Price: low to high</option>
                <option value="price-high">Price: high to low</option>
                <option value="stock">Stock level</option>
              </select>
              <ChevronDown className="pointer-events-none absolute right-3 top-1/2 h-4 w-4 -translate-y-1/2 text-moss" aria-hidden />
            </div>

            <label className="flex items-center gap-2 whitespace-nowrap text-sm">
              <input
                type="checkbox"
                checked={inStockOnly}
                onChange={e => setInStockOnly(e.target.checked)}
                className="h-4 w-4 accent-moss"
              />
              In stock only
            </label>
          </div>
        </div>
      </div>

      {loadError && (
        <p role="alert" className="rounded-lg bg-wheat px-4 py-3 text-sm">
          Could not load products. Check that the server is running and refresh.
        </p>
      )}

      {visible.length > 0 ? (
        <div className="grid grid-cols-1 gap-6 md:grid-cols-2 lg:grid-cols-3">
          {visible.map(product => {
            const unitPrice = unitPrices.get(product.id) ?? product.basePrice;
            const discounted = unitPrice < product.basePrice;
            const percentOff = Math.round(((product.basePrice - unitPrice) / product.basePrice) * 100);
            const stock = PricingUtils.getStockStatus(product.stock);
            const inCart = quantityInCart(product.id);

            return (
              <article key={product.id} className="card flex flex-col p-6 transition-shadow hover:shadow-md">
                <div className="mb-3 flex items-start justify-between gap-2">
                  <h3 className="text-lg font-semibold">{product.name}</h3>
                  <span className={`badge whitespace-nowrap ${stock.badge}`}>{stock.label}</span>
                </div>

                <p className="line-clamp-2 mb-4 text-sm text-moss">{product.description}</p>

                <div className="mb-4 mt-auto">
                  <div className={`text-2xl font-bold transition-opacity ${pricesReady ? "" : "opacity-40"}`}>
                    {PricingUtils.formatCurrency(unitPrice)}
                  </div>
                  {discounted && (
                    <div className="mt-1 flex items-center gap-2">
                      <span className="text-sm text-moss line-through">{PricingUtils.formatCurrency(product.basePrice)}</span>
                      <span className="badge bg-ochre text-bark">{percentOff}% off</span>
                    </div>
                  )}
                </div>

                {customerId === null ? (
                  <button type="button" disabled className="btn btn-secondary">
                    <CartIcon className="h-4 w-4" aria-hidden />
                    Choose a customer
                  </button>
                ) : inCart === 0 ? (
                  <button
                    type="button"
                    onClick={() => onSetQuantity(product, 1)}
                    disabled={product.stock === 0}
                    className="btn btn-primary"
                  >
                    <CartIcon className="h-4 w-4" aria-hidden />
                    {product.stock === 0 ? 'Out of stock' : 'Add to cart'}
                  </button>
                ) : (
                  <div className="flex items-center justify-between rounded-lg border border-moss bg-mist/40 p-1">
                    <button
                      type="button"
                      onClick={() => onSetQuantity(product, inCart - 1)}
                      aria-label={`Remove one ${product.name}`}
                      className="rounded-md p-2 hover:bg-mist"
                    >
                      <Minus className="h-4 w-4" aria-hidden />
                    </button>
                    <span className="font-semibold" aria-live="polite">{inCart} in cart</span>
                    <button
                      type="button"
                      onClick={() => onSetQuantity(product, inCart + 1)}
                      disabled={inCart >= product.stock}
                      aria-label={`Add one more ${product.name}`}
                      className="rounded-md p-2 hover:bg-mist disabled:opacity-40"
                    >
                      <Plus className="h-4 w-4" aria-hidden />
                    </button>
                  </div>
                )}
              </article>
            );
          })}
        </div>
      ) : (
        !loadError && (
          <div className="py-12 text-center">
            <h3 className="mb-1 text-lg font-medium">No products found</h3>
            <p className="text-moss">
              {query ? `Nothing matches "${query.trim()}". Try a different word.` : 'The catalogue is empty.'}
            </p>
          </div>
        )
      )}
    </div>
  );
};

export default ProductCatalog;
