import { useCallback, useEffect, useMemo, useState } from 'react';
import { Calendar, ChevronDown, Package, User } from 'lucide-react';
import { Link } from 'react-router-dom';
import type { Order } from '../types';
import { OrderStatus } from '../types';
import { ApiService, ORDER_STATUS_LABELS, PricingUtils, errorMessage } from '../services/api';

interface OrderHistoryProps {
  onOrdersLoaded: (count: number) => void;
}

const money = PricingUtils.formatCurrency;

const STATUS_BADGE: Record<OrderStatus, string> = {
  [OrderStatus.Pending]: 'bg-wheat text-bark',
  [OrderStatus.Confirmed]: 'bg-mist text-bark',
  [OrderStatus.Shipped]: 'bg-moss text-cream',
  [OrderStatus.Cancelled]: 'bg-bark/10 text-bark',
};

const OrderHistory: React.FC<OrderHistoryProps> = ({ onOrdersLoaded }) => {
  const [orders, setOrders] = useState<Order[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [selectedId, setSelectedId] = useState<number | null>(null);
  const [customerFilter, setCustomerFilter] = useState<number | 'all'>('all');
  const [statusError, setStatusError] = useState<string | null>(null);

  const loadOrders = useCallback(async () => {
    try {
      const data = await ApiService.getOrders();
      setOrders(data);
      setError(null);
      onOrdersLoaded(data.length);
    } catch (err) {
      setError(errorMessage(err, 'Could not load orders.'));
    } finally {
      setLoading(false);
    }
  }, [onOrdersLoaded]);

  useEffect(() => {
    loadOrders();
  }, [loadOrders]);

  const customers = useMemo(() => {
    const seen = new Map<number, string>();
    orders.forEach(o => seen.set(o.customerId, o.customerName));
    return [...seen.entries()].sort((a, b) => a[1].localeCompare(b[1]));
  }, [orders]);

  const shown = orders.filter(o => customerFilter === 'all' || o.customerId === customerFilter);
  const selected = orders.find(o => o.id === selectedId) ?? null;

  const changeStatus = async (order: Order, status: OrderStatus) => {
    setStatusError(null);
    try {
      await ApiService.updateOrderStatus(order.id, status);
      setOrders(prev => prev.map(o => (o.id === order.id ? { ...o, status } : o)));
    } catch (err) {
      setStatusError(errorMessage(err, 'Could not update the status.'));
    }
  };

  if (loading) {
    return (
      <div className="flex items-center justify-center gap-3 py-12" role="status">
        <div className="h-8 w-8 animate-spin rounded-full border-4 border-mist border-t-moss" />
        <span className="text-moss">Loading orders...</span>
      </div>
    );
  }

  return (
    <div className="space-y-6">
      <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <h2 className="text-3xl font-bold">Order history</h2>
          <p className="mt-1 text-moss">
            {shown.length} order{shown.length !== 1 ? 's' : ''}
          </p>
        </div>

        {customers.length > 1 && (
          <div className="relative">
            <label htmlFor="order-customer" className="sr-only">Filter by customer</label>
            <select
              id="order-customer"
              value={customerFilter}
              onChange={e => setCustomerFilter(e.target.value === 'all' ? 'all' : Number(e.target.value))}
              className="field appearance-none pr-9"
            >
              <option value="all">All customers</option>
              {customers.map(([id, name]) => (
                <option key={id} value={id}>{name}</option>
              ))}
            </select>
            <ChevronDown className="pointer-events-none absolute right-3 top-1/2 h-4 w-4 -translate-y-1/2 text-moss" aria-hidden />
          </div>
        )}
      </div>

      {error && <p role="alert" className="rounded-lg bg-wheat px-4 py-3 text-sm">{error}</p>}

      {shown.length === 0 ? (
        <div className="py-12 text-center">
          <Package className="mx-auto mb-4 h-16 w-16 text-sage" aria-hidden />
          <h3 className="mb-2 text-xl font-medium">No orders yet</h3>
          <p className="mb-6 text-moss">Orders you place will appear here.</p>
          <Link to="/" className="btn btn-primary">Browse products</Link>
        </div>
      ) : (
        <div className="grid gap-6 lg:grid-cols-2">
          <ul className="space-y-3">
            {shown.map(order => (
              <li key={order.id}>
                <button
                  type="button"
                  onClick={() => setSelectedId(order.id)}
                  aria-pressed={selected?.id === order.id}
                  className={`card w-full p-4 text-left transition-colors ${
                    selected?.id === order.id ? 'border-moss bg-mist/50' : 'hover:border-sage'
                  }`}
                >
                  <div className="mb-3 flex items-start justify-between">
                    <div>
                      <h3 className="font-semibold">Order #{order.id}</h3>
                      <div className="mt-1 flex items-center gap-4 text-sm text-moss">
                        <span className="flex items-center gap-1"><User className="h-4 w-4" aria-hidden />{order.customerName}</span>
                        <span className="flex items-center gap-1"><Calendar className="h-4 w-4" aria-hidden />{new Date(order.createdAt).toLocaleDateString()}</span>
                      </div>
                    </div>
                    <span className={`badge ${STATUS_BADGE[order.status]}`}>{ORDER_STATUS_LABELS[order.status]}</span>
                  </div>
                  <div className="flex items-center justify-between">
                    <span className="text-sm text-moss">
                      {order.items.length} item{order.items.length !== 1 ? 's' : ''}
                    </span>
                    <span className="font-semibold">{money(order.totalAmount)}</span>
                  </div>
                </button>
              </li>
            ))}
          </ul>

          <div>
            {selected ? (
              <section className="card p-6" aria-label={`Order ${selected.id} details`}>
                <h3 className="mb-4 text-xl font-semibold">Order #{selected.id}</h3>

                <div className="mb-6 grid grid-cols-2 gap-4 text-sm">
                  <div>
                    <h4 className="mb-1 font-medium">Customer</h4>
                    <p>{selected.customerName}</p>
                    <span className={`badge mt-1 ${PricingUtils.getCustomerTypeBadge(selected.customerType)}`}>
                      {PricingUtils.getCustomerTypeLabel(selected.customerType)}
                    </span>
                  </div>
                  <div>
                    <h4 className="mb-1 font-medium">Placed</h4>
                    <p>{new Date(selected.createdAt).toLocaleString()}</p>
                    <div className="mt-1">
                      <label htmlFor="status" className="sr-only">Order status</label>
                      <select
                        id="status"
                        value={selected.status}
                        onChange={e => changeStatus(selected, Number(e.target.value) as OrderStatus)}
                        className="field py-1 text-sm"
                      >
                        {Object.values(OrderStatus).map(s => (
                          <option key={s} value={s}>{ORDER_STATUS_LABELS[s]}</option>
                        ))}
                      </select>
                    </div>
                  </div>
                </div>

                {statusError && <p role="alert" className="mb-4 rounded-lg bg-wheat px-3 py-2 text-sm">{statusError}</p>}

                <h4 className="mb-2 font-medium">Items</h4>
                <ul className="divide-y divide-mist">
                  {selected.items.map(item => (
                    <li key={item.productId} className="flex justify-between py-2">
                      <div>
                        <p className="font-medium">{item.productName}</p>
                        <p className="text-sm text-moss">{item.quantity} &times; {money(item.unitPrice)}</p>
                      </div>
                      <p className="font-semibold">{money(item.totalPrice)}</p>
                    </li>
                  ))}
                </ul>

                <dl className="mt-4 space-y-1 border-t border-mist pt-4 text-sm">
                  <div className="flex justify-between">
                    <dt>Subtotal</dt>
                    <dd>{money(selected.subtotal)}</dd>
                  </div>
                  {selected.discountAmount > 0 && (
                    <div className="flex justify-between font-medium text-moss">
                      <dt>{selected.discountDescription ?? 'Order discount'}</dt>
                      <dd>-{money(selected.discountAmount)}</dd>
                    </div>
                  )}
                  <div className="flex justify-between pt-1 text-lg font-bold">
                    <dt>Total</dt>
                    <dd>{money(selected.totalAmount)}</dd>
                  </div>
                </dl>
              </section>
            ) : (
              <div className="rounded-xl border-2 border-dashed border-sage p-8 text-center">
                <Package className="mx-auto mb-3 h-12 w-12 text-sage" aria-hidden />
                <h3 className="mb-1 text-lg font-medium">Select an order</h3>
                <p className="text-moss">Pick an order from the list to see its breakdown.</p>
              </div>
            )}
          </div>
        </div>
      )}
    </div>
  );
};

export default OrderHistory;
