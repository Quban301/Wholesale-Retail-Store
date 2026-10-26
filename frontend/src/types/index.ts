// Enums as const objects (erasableSyntaxOnly-friendly). The API sends enums as numbers.
export const CustomerType = {
  Retail: 0,
  Wholesale: 1,
} as const;
export type CustomerType = (typeof CustomerType)[keyof typeof CustomerType];

export const OrderStatus = {
  Pending: 0,
  Confirmed: 1,
  Shipped: 2,
  Cancelled: 3,
} as const;
export type OrderStatus = (typeof OrderStatus)[keyof typeof OrderStatus];

export const RuleScope = {
  PerUnit: 0,
  OrderTotal: 1,
} as const;
export type RuleScope = (typeof RuleScope)[keyof typeof RuleScope];

export interface Product {
  id: number;
  name: string;
  description: string;
  stock: number;
  basePrice: number;
}

export interface Customer {
  id: number;
  name: string;
  email: string;
  customerType: CustomerType;
}

export interface OrderItem {
  productId: number;
  productName: string;
  quantity: number;
  unitPrice: number;
  totalPrice: number;
}

export interface Order {
  id: number;
  customerId: number;
  customerName: string;
  customerType: CustomerType;
  createdAt: string;
  /** Sum of the line totals, before the order-level discount. */
  subtotal: number;
  discountAmount: number;
  discountDescription: string | null;
  totalAmount: number;
  status: OrderStatus;
  items: OrderItem[];
}

export interface OrderRequest {
  customerId: number;
  items: { productId: number; quantity: number }[];
}

export interface QuoteItem {
  productId: number;
  productName: string;
  quantity: number;
  basePrice: number;
  discountedPrice: number;
  discountAmount: number;
  lineTotal: number;
}

export interface AppliedRule {
  ruleId: number;
  description: string;
  discountPercentage: number;
  scope: RuleScope;
  amountSaved: number;
}

export interface NextTier {
  description: string;
  discountPercentage: number;
  minimumOrderAmount: number;
  amountToUnlock: number;
}

export interface Quote {
  customerId: number;
  customerName: string;
  customerType: CustomerType;
  generatedAt: string;
  items: QuoteItem[];
  listTotal: number;
  unitDiscountTotal: number;
  subtotal: number;
  additionalDiscount: number;
  additionalDiscountPercentage: number;
  total: number;
  totalSavings: number;
  appliedRules: AppliedRule[];
  nextTier: NextTier | null;
}

export interface PricingRule {
  id: number;
  customerType: CustomerType;
  discountPercentage: number;
  minimumOrderAmount: number | null;
  description: string;
}

export interface ProductPrice extends Product {
  /** Unit price for the selected customer after the per-unit rule. */
  calculatedPrice: number;
}

export interface SearchResponse {
  query: string;
  /** Set when the query had typos; results are for this corrected text. */
  correctedQuery: string | null;
  products: Product[];
}

export interface CartItem {
  product: Product;
  quantity: number;
}

export interface CartState {
  customerId: number | null;
  customerType: CustomerType | null;
  items: CartItem[];
}
