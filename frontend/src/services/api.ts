import axios from 'axios';
import type {
  Customer,
  Order,
  OrderRequest,
  PricingRule,
  ProductPrice,
  Quote,
  SearchResponse,
} from '../types';
import { CustomerType, OrderStatus } from '../types';

// Relative base URL: the .NET app serves the built SPA, and the Vite dev server proxies /api.
const api = axios.create({
  baseURL: '/api',
  headers: { 'Content-Type': 'application/json' },
});

/** Pulls the server's `message` out of a failed request, falling back to a generic text. */
export function errorMessage(error: unknown, fallback = 'Something went wrong. Please try again.'): string {
  if (axios.isAxiosError(error)) {
    const data = error.response?.data as { message?: string; errors?: Record<string, string[]> } | undefined;
    if (data?.message) return data.message;
    if (data?.errors) return Object.values(data.errors).flat().join(' ');
    if (!error.response) return 'Cannot reach the server.';
  }
  return fallback;
}

export class ApiService {
  static async searchProducts(query: string, autoCorrect = true): Promise<SearchResponse> {
    const response = await api.get<SearchResponse>('/products/search', { params: { q: query, autoCorrect } });
    return response.data;
  }

  static async getCustomers(): Promise<Customer[]> {
    return (await api.get<Customer[]>('/customers')).data;
  }

  static async createCustomer(customer: Omit<Customer, 'id'>): Promise<Customer> {
    return (await api.post<Customer>('/customers', customer)).data;
  }

  static async getProductPrices(customerId: number): Promise<ProductPrice[]> {
    const response = await api.get<{ products: ProductPrice[] }>(`/pricing/products/${customerId}`);
    return response.data.products;
  }

  static async getPricingRules(): Promise<PricingRule[]> {
    return (await api.get<PricingRule[]>('/pricing/rules')).data;
  }

  static async generateQuote(request: OrderRequest): Promise<Quote> {
    return (await api.post<Quote>('/pricing/quote', request)).data;
  }

  static async getOrders(): Promise<Order[]> {
    return (await api.get<Order[]>('/orders')).data;
  }

  static async createOrder(request: OrderRequest): Promise<Order> {
    return (await api.post<Order>('/orders', request)).data;
  }

  static async updateOrderStatus(id: number, status: OrderStatus): Promise<void> {
    await api.put(`/orders/${id}/status`, { status: ORDER_STATUS_LABELS_MAP[status] });
  }
}

const ORDER_STATUS_LABELS_MAP: Record<OrderStatus, string> = {
  [OrderStatus.Pending]: 'Pending',
  [OrderStatus.Confirmed]: 'Confirmed',
  [OrderStatus.Shipped]: 'Shipped',
  [OrderStatus.Cancelled]: 'Cancelled',
};

export const ORDER_STATUS_LABELS = ORDER_STATUS_LABELS_MAP;

export class PricingUtils {
  static formatCurrency(amount: number): string {
    return new Intl.NumberFormat('en-ZA', { style: 'currency', currency: 'ZAR' }).format(amount);
  }

  static getCustomerTypeLabel(type: CustomerType): string {
    return type === CustomerType.Retail ? 'Retail' : 'Wholesale';
  }

  static getCustomerTypeBadge(type: CustomerType): string {
    return type === CustomerType.Retail ? 'bg-mist text-bark' : 'bg-ochre text-bark';
  }

  static getStockStatus(stock: number): { label: string; badge: string } {
    if (stock === 0) return { label: 'Out of stock', badge: 'bg-bark/10 text-bark' };
    if (stock < 10) return { label: `Low stock (${stock})`, badge: 'bg-wheat text-bark' };
    return { label: `In stock (${stock})`, badge: 'bg-mist text-bark' };
  }
}

export default api;
