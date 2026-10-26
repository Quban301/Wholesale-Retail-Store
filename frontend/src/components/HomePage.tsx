import { Package, Users } from 'lucide-react';
import CustomerSelector from './CustomerSelector';
import ProductCatalog from './ProductCatalog';
import type { CartItem, Customer, CustomerType, Product } from '../types';

interface HomePageProps {
  customers: Customer[];
  customerId: number | null;
  customerType: CustomerType | null;
  cartItems: CartItem[];
  onCustomerSelect: (customer: Customer) => void;
  onCustomerCreated: () => void;
  onSetQuantity: (product: Product, quantity: number) => void;
}

const HomePage: React.FC<HomePageProps> = ({
  customers,
  customerId,
  customerType,
  cartItems,
  onCustomerSelect,
  onCustomerCreated,
  onSetQuantity,
}) => (
  <div className="space-y-6">
    <section className="card p-6" aria-labelledby="customer-heading">
      <div className="mb-4 flex items-center gap-2">
        <Users className="h-5 w-5 text-moss" aria-hidden />
        <h2 id="customer-heading" className="text-lg font-semibold">Customer</h2>
      </div>
      <CustomerSelector
        customers={customers}
        selectedCustomerId={customerId}
        onCustomerSelect={onCustomerSelect}
        onCustomerCreated={onCustomerCreated}
      />
    </section>

    <section className="card" aria-labelledby="catalog-heading">
      <div className="flex flex-wrap items-center justify-between gap-2 border-b border-mist p-6">
        <div className="flex items-center gap-2">
          <Package className="h-5 w-5 text-moss" aria-hidden />
          <h2 id="catalog-heading" className="text-lg font-semibold">Product catalogue</h2>
        </div>
        {customerId === null && (
          <p className="rounded-lg bg-wheat px-3 py-1 text-sm">
            Choose a customer to see your prices and add items to the cart
          </p>
        )}
      </div>
      <div className="p-6">
        <ProductCatalog
          customerId={customerId}
          customerType={customerType}
          cartItems={cartItems}
          onSetQuantity={onSetQuantity}
        />
      </div>
    </section>
  </div>
);

export default HomePage;
