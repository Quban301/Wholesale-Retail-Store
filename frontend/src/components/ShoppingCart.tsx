import { useState } from 'react';
import { Link } from 'react-router-dom';
import { CheckCircle2, CreditCard, Minus, Plus, ShoppingBag, Sparkles, Trash2 } from 'lucide-react';
import type { CartState, Customer, Order, Product, Quote } from '../types';
import { RuleScope } from '../types';
import { ApiService, PricingUtils, errorMessage } from '../services/api';

interface ShoppingCartProps {
  cart: CartState;
  customer: Customer | null;
  quote: Quote | null;
  quoteError: string | null;
  quoteLoading: boolean;
  onSetQuantity: (product: Product, quantity: number) => void;
  onClearCart: () => void;
  onOrderPlaced: () => void;
}

const money = PricingUtils.formatCurrency;

const ShoppingCart: React.FC<ShoppingCartProps> = ({
  cart,
  customer,
  quote,
  quoteError,
  quoteLoading,
  onSetQuantity,
  onClearCart,
  onOrderPlaced,
}) => {
  const [placing, setPlacing] = useState(false);
  const [placeError, setPlaceError] = useState<string | null>(null);
  const [placedOrder, setPlacedOrder] = useState<Order | null>(null);

  const totalUnits = cart.items.reduce((sum, i) => sum + i.quantity, 0);

  const placeOrder = async () => {
    if (cart.customerId === null) return;
    setPlacing(true);
    setPlaceError(null);
    try {
      const order = await ApiService.createOrder({
        customerId: cart.customerId,
        items: cart.items.map(i => ({ productId: i.product.id, quantity: i.quantity })),
      });
      setPlacedOrder(order);
      onOrderPlaced();
    } catch (error) {
      setPlaceError(errorMessage(error, 'Could not place the order.'));
    } finally {
      setPlacing(false);
    }
  };

  if (placedOrder) {
    return <OrderConfirmation order={placedOrder} onDismiss={() => setPlacedOrder(null)} />;
  }

  if (cart.items.length === 0) {
    return (
      <div className="py-16 text-center">
        <ShoppingBag className="mx-auto mb-4 h-16 w-16 text-sage" aria-hidden />
        <h2 className="mb-2 text-xl font-medium">Your cart is empty</h2>
        <p className="mb-6 text-moss">Add some products to get started.</p>
        <Link to="/" className="btn btn-primary">Browse products</Link>
      </div>
    );
  }

  const lineFor = (productId: number) => quote?.items.find(i => i.productId === productId);
  const customerLabel = customer ? PricingUtils.getCustomerTypeLabel(customer.customerType) : null;

  return (
    <div className="space-y-6">
      <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <h2 className="text-3xl font-bold">Shopping cart</h2>
          <p className="mt-1 text-moss">
            {totalUnits} item{totalUnits !== 1 ? 's' : ''} in your cart
          </p>
        </div>
        <button type="button" onClick={onClearCart} className="btn btn-secondary">
          <Trash2 className="h-4 w-4" aria-hidden />
          Clear cart
        </button>
      </div>

      {customer ? (
        <div className="card flex items-center gap-3 p-4">
          <span className={`badge ${PricingUtils.getCustomerTypeBadge(customer.customerType)}`}>{customerLabel}</span>
          <p>
            Ordering as <strong>{customer.name}</strong>
            <span className="text-moss">
              {' '}
              &middot; {customer.customerType === 1 ? 'wholesale discounts applied' : 'listed prices apply'}
            </span>
          </p>
        </div>
      ) : (
        <p role="alert" className="rounded-lg bg-wheat px-4 py-3">
          Choose a customer on the <Link to="/" className="font-semibold underline">Products</Link> page to price this cart.
        </p>
      )}

      <div className="grid gap-6 lg:grid-cols-3">
        <ul className="space-y-4 lg:col-span-2">
          {cart.items.map(({ product, quantity }) => {
            const line = lineFor(product.id);
            const unit = line?.discountedPrice ?? product.basePrice;
            const saved = line ? line.basePrice - line.discountedPrice : 0;
            const stock = PricingUtils.getStockStatus(product.stock);

            return (
              <li key={product.id} className="card p-5">
                <div className="flex flex-col gap-4 md:flex-row md:items-center">
                  <div className="flex-1">
                    <h3 className="text-lg font-semibold">{product.name}</h3>
                    <p className="mb-2 text-sm text-moss">{product.description}</p>
                    <span className={`badge ${stock.badge}`}>{stock.label}</span>
                  </div>

                  <div className="flex items-center rounded-lg border border-sage">
                    <button
                      type="button"
                      onClick={() => onSetQuantity(product, quantity - 1)}
                      aria-label={`Decrease quantity of ${product.name}`}
                      className="p-2 hover:bg-mist"
                    >
                      <Minus className="h-4 w-4" aria-hidden />
                    </button>
                    <input
                      type="number"
                      min={1}
                      max={product.stock}
                      value={quantity}
                      onChange={e => onSetQuantity(product, Number(e.target.value))}
                      aria-label={`Quantity of ${product.name}`}
                      className="w-16 bg-transparent px-2 py-2 text-center"
                    />
                    <button
                      type="button"
                      onClick={() => onSetQuantity(product, quantity + 1)}
                      disabled={quantity >= product.stock}
                      aria-label={`Increase quantity of ${product.name}`}
                      className="p-2 hover:bg-mist disabled:opacity-40"
                    >
                      <Plus className="h-4 w-4" aria-hidden />
                    </button>
                  </div>

                  <div className="min-w-40 text-right">
                    <div className="flex items-center justify-end gap-2">
                      <span className="text-lg font-bold">{money(unit)}</span>
                      {saved > 0 && <span className="text-sm text-moss line-through">{money(product.basePrice)}</span>}
                    </div>
                    {saved > 0 && <p className="text-xs font-medium text-moss">You save {money(saved)} each</p>}
                    <p className="text-sm text-moss">Line total: {money(line?.lineTotal ?? unit * quantity)}</p>
                  </div>

                  <button
                    type="button"
                    onClick={() => onSetQuantity(product, 0)}
                    aria-label={`Remove ${product.name}`}
                    className="self-end rounded-lg p-2 text-moss hover:bg-mist hover:text-bark md:self-center"
                  >
                    <Trash2 className="h-4 w-4" aria-hidden />
                  </button>
                </div>
              </li>
            );
          })}
        </ul>

        <aside className="card h-fit space-y-4 p-6 lg:sticky lg:top-6" aria-label="Order summary">
          <h3 className="text-xl font-semibold">Order summary</h3>

          {quoteError && (
            <p role="alert" className="rounded-lg bg-wheat px-3 py-2 text-sm">{quoteError}</p>
          )}

          {quote && !quoteError && <Breakdown quote={quote} dimmed={quoteLoading} />}

          {!quote && !quoteError && customer && (
            <p className="text-sm text-moss" role="status">Pricing your cart...</p>
          )}

          {quote?.nextTier && !quoteError && (
            <div className="flex items-start gap-2 rounded-lg bg-mist/60 p-3 text-sm">
              <Sparkles className="mt-0.5 h-4 w-4 shrink-0 text-moss" aria-hidden />
              <p>
                Add <strong>{money(quote.nextTier.amountToUnlock)}</strong> more to get{' '}
                <strong>{quote.nextTier.discountPercentage}% off</strong> your whole order.
              </p>
            </div>
          )}

          {placeError && (
            <p role="alert" className="rounded-lg bg-wheat px-3 py-2 text-sm">{placeError}</p>
          )}

          <button
            type="button"
            onClick={placeOrder}
            disabled={placing || quoteLoading || !quote || !!quoteError || cart.customerId === null}
            className="btn btn-primary w-full py-3"
          >
            <CreditCard className="h-5 w-5" aria-hidden />
            {placing ? 'Placing order...' : 'Place order'}
          </button>
        </aside>
      </div>
    </div>
  );
};

const Breakdown: React.FC<{ quote: Quote; dimmed: boolean }> = ({ quote, dimmed }) => {
  const unitRule = quote.appliedRules.find(r => r.scope === RuleScope.PerUnit);
  const volumeRule = quote.appliedRules.find(r => r.scope === RuleScope.OrderTotal);

  return (
    <dl className={`space-y-2 text-sm transition-opacity ${dimmed ? 'opacity-50' : ''}`}>
      <Row label="List price" value={money(quote.listTotal)} />
      {unitRule && (
        <Row
          label={unitRule.description}
          value={`-${money(quote.unitDiscountTotal)}`}
          highlight
        />
      )}
      <Row label="Subtotal" value={money(quote.subtotal)} />
      {volumeRule && (
        <Row
          label={volumeRule.description}
          value={`-${money(quote.additionalDiscount)}`}
          highlight
        />
      )}
      <hr className="border-mist" />
      <div className="flex justify-between text-lg font-bold">
        <dt>Total</dt>
        <dd>{money(quote.total)}</dd>
      </div>
      {quote.totalSavings > 0 && (
        <p className="rounded-lg bg-ochre px-3 py-2 text-center font-semibold text-bark">
          You save {money(quote.totalSavings)} on this order
        </p>
      )}
    </dl>
  );
};

const Row: React.FC<{ label: string; value: string; highlight?: boolean }> = ({ label, value, highlight }) => (
  <div className={`flex justify-between gap-4 ${highlight ? 'font-medium text-moss' : ''}`}>
    <dt>{label}</dt>
    <dd className="whitespace-nowrap">{value}</dd>
  </div>
);

const OrderConfirmation: React.FC<{ order: Order; onDismiss: () => void }> = ({ order, onDismiss }) => (
  <div className="mx-auto max-w-xl py-8">
    <div className="card p-8 text-center">
      <CheckCircle2 className="mx-auto mb-3 h-14 w-14 text-moss" aria-hidden />
      <h2 className="mb-1 text-2xl font-bold">Order #{order.id} confirmed</h2>
      <p className="mb-6 text-moss">Thanks, {order.customerName}. Your stock has been reserved.</p>

      <ul className="mb-4 space-y-1 text-left text-sm">
        {order.items.map(item => (
          <li key={item.productId} className="flex justify-between gap-4">
            <span>{item.quantity} &times; {item.productName} @ {money(item.unitPrice)}</span>
            <span>{money(item.totalPrice)}</span>
          </li>
        ))}
      </ul>

      <dl className="space-y-1 border-t border-mist pt-3 text-left text-sm">
        <Row label="Subtotal" value={money(order.subtotal)} />
        {order.discountAmount > 0 && (
          <Row label={order.discountDescription ?? 'Order discount'} value={`-${money(order.discountAmount)}`} highlight />
        )}
        <div className="flex justify-between pt-1 text-lg font-bold">
          <dt>Total paid</dt>
          <dd>{money(order.totalAmount)}</dd>
        </div>
      </dl>

      <div className="mt-6 flex flex-col justify-center gap-3 sm:flex-row">
        <Link to="/" onClick={onDismiss} className="btn btn-primary">Continue shopping</Link>
        <Link to="/orders" onClick={onDismiss} className="btn btn-secondary">View orders</Link>
      </div>
    </div>
  </div>
);

export default ShoppingCart;
