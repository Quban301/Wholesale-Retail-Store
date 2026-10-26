import { useCallback, useEffect, useState } from 'react';
import { BrowserRouter as Router, NavLink, Route, Routes } from 'react-router-dom';
import { FileText, Package, ShoppingCart as CartIcon, Sprout } from 'lucide-react';
import HomePage from './components/HomePage';
import ShoppingCart from './components/ShoppingCart';
import OrderHistory from './components/OrderHistory';
import type { CartState, Customer, Product, Quote } from './types';
import { ApiService, errorMessage } from './services/api';

const EMPTY_CART: CartState = { customerId: null, customerType: null, items: [] };

const navClass = ({ isActive }: { isActive: boolean }) =>
  `flex items-center gap-2 rounded-lg px-3 py-2 transition-colors ${
    isActive ? 'bg-cream text-bark' : 'text-cream hover:bg-moss'
  }`;

function App() {
  const [cart, setCart] = useState<CartState>(EMPTY_CART);
  const [customers, setCustomers] = useState<Customer[]>([]);
  const [loading, setLoading] = useState(true);
  const [orderCount, setOrderCount] = useState(0);

  // The server is the single source of truth for prices: whenever the customer or the basket
  // changes we ask for a fresh quote instead of adding up prices in the browser.
  const [quote, setQuote] = useState<Quote | null>(null);
  const [quoteError, setQuoteError] = useState<string | null>(null);
  const [quoteLoading, setQuoteLoading] = useState(false);

  const loadCustomers = useCallback(async () => {
    try {
      setCustomers(await ApiService.getCustomers());
    } catch (error) {
      console.error('Failed to load customers:', error);
    } finally {
      setLoading(false);
    }
  }, []);

  const loadOrderCount = useCallback(async () => {
    try {
      setOrderCount((await ApiService.getOrders()).length);
    } catch (error) {
      console.error('Failed to load order count:', error);
    }
  }, []);

  useEffect(() => {
    loadCustomers();
    loadOrderCount();
  }, [loadCustomers, loadOrderCount]);

  useEffect(() => {
    if (cart.customerId === null || cart.items.length === 0) {
      setQuote(null);
      setQuoteError(null);
      setQuoteLoading(false);
      return;
    }

    let stale = false;
    setQuoteLoading(true);
    const timer = setTimeout(async () => {
      try {
        const result = await ApiService.generateQuote({
          customerId: cart.customerId!,
          items: cart.items.map(i => ({ productId: i.product.id, quantity: i.quantity })),
        });
        if (!stale) {
          setQuote(result);
          setQuoteError(null);
        }
      } catch (error) {
        if (!stale) {
          setQuote(null);
          setQuoteError(errorMessage(error, 'Could not price your cart.'));
        }
      } finally {
        if (!stale) setQuoteLoading(false);
      }
    }, 150);

    return () => {
      stale = true;
      clearTimeout(timer);
    };
  }, [cart.customerId, cart.items]);

  const selectCustomer = (customer: Customer) => {
    // The basket stays; the effect above re-prices it for the new customer type.
    setCart(prev => ({ ...prev, customerId: customer.id, customerType: customer.customerType }));
  };

  const setQuantity = (product: Product, quantity: number) => {
    const clamped = Math.min(Math.max(0, Math.floor(quantity) || 0), product.stock);
    setCart(prev => {
      const others = prev.items.filter(i => i.product.id !== product.id);
      return { ...prev, items: clamped === 0 ? others : [...others, { product, quantity: clamped }] };
    });
  };

  const clearCart = () => setCart(prev => ({ ...prev, items: [] }));

  const handleOrderPlaced = () => {
    clearCart();
    loadOrderCount();
  };

  if (loading) {
    return (
      <div className="flex min-h-screen items-center justify-center bg-cream" role="status" aria-label="Loading">
        <div className="h-12 w-12 animate-spin rounded-full border-4 border-mist border-t-moss" />
      </div>
    );
  }

  const cartLines = cart.items.length;

  return (
    <Router>
      <div className="min-h-screen bg-cream">
        <header className="bg-bark text-cream shadow">
          <div className="mx-auto flex max-w-7xl flex-wrap items-center justify-between gap-3 px-4 py-3">
            <div className="flex items-center gap-3">
              <div className="rounded-lg bg-ochre p-2 text-bark">
                <Sprout className="h-6 w-6" aria-hidden />
              </div>
              <div>
                <h1 className="text-xl font-bold leading-tight">Wholesale Retail Store</h1>
                <p className="text-sm text-sage">Fair prices for every kind of customer</p>
              </div>
            </div>

            <nav className="flex items-center gap-1" aria-label="Main">
              <NavLink to="/" end className={navClass}>
                <Package className="h-4 w-4" aria-hidden />
                <span>Products</span>
              </NavLink>
              <NavLink to="/cart" className={navClass}>
                <CartIcon className="h-4 w-4" aria-hidden />
                <span>Cart</span>
                {cartLines > 0 && (
                  <span className="min-w-5 rounded-full bg-ochre px-1.5 text-center text-xs font-semibold text-bark">
                    {cartLines}
                  </span>
                )}
              </NavLink>
              <NavLink to="/orders" className={navClass}>
                <FileText className="h-4 w-4" aria-hidden />
                <span>Orders</span>
                {orderCount > 0 && (
                  <span className="min-w-5 rounded-full bg-sage px-1.5 text-center text-xs font-semibold text-bark">
                    {orderCount}
                  </span>
                )}
              </NavLink>
            </nav>
          </div>
        </header>

        <main className="mx-auto max-w-7xl px-4 py-6">
          <Routes>
            <Route
              path="/"
              element={
                <HomePage
                  customers={customers}
                  customerId={cart.customerId}
                  customerType={cart.customerType}
                  cartItems={cart.items}
                  onCustomerSelect={selectCustomer}
                  onCustomerCreated={loadCustomers}
                  onSetQuantity={setQuantity}
                />
              }
            />
            <Route
              path="/cart"
              element={
                <ShoppingCart
                  cart={cart}
                  customer={customers.find(c => c.id === cart.customerId) ?? null}
                  quote={quote}
                  quoteError={quoteError}
                  quoteLoading={quoteLoading}
                  onSetQuantity={setQuantity}
                  onClearCart={clearCart}
                  onOrderPlaced={handleOrderPlaced}
                />
              }
            />
            <Route path="/orders" element={<OrderHistory onOrdersLoaded={setOrderCount} />} />
          </Routes>
        </main>
      </div>
    </Router>
  );
}

export default App;
