import { useState } from 'react';
import { ChevronDown, Plus } from 'lucide-react';
import type { Customer } from '../types';
import { CustomerType } from '../types';
import { ApiService, PricingUtils, errorMessage } from '../services/api';

interface CustomerSelectorProps {
  customers: Customer[];
  selectedCustomerId: number | null;
  onCustomerSelect: (customer: Customer) => void;
  onCustomerCreated: () => void;
}

const BLANK = { name: '', email: '', customerType: CustomerType.Retail as CustomerType };

const CustomerSelector: React.FC<CustomerSelectorProps> = ({
  customers,
  selectedCustomerId,
  onCustomerSelect,
  onCustomerCreated,
}) => {
  const [showForm, setShowForm] = useState(false);
  const [form, setForm] = useState(BLANK);
  const [creating, setCreating] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const selected = customers.find(c => c.id === selectedCustomerId);

  const handleCreate = async (e: React.FormEvent) => {
    e.preventDefault();
    setCreating(true);
    setError(null);
    try {
      const created = await ApiService.createCustomer(form);
      onCustomerCreated();
      onCustomerSelect(created);
      setForm(BLANK);
      setShowForm(false);
    } catch (err) {
      setError(errorMessage(err, 'Could not create the customer.'));
    } finally {
      setCreating(false);
    }
  };

  return (
    <div className="space-y-4">
      <div>
        <label htmlFor="customer" className="mb-1 block text-sm font-medium">
          Shopping as
        </label>
        <div className="flex flex-col gap-3 sm:flex-row">
          <div className="relative flex-1">
            <select
              id="customer"
              value={selectedCustomerId ?? ''}
              onChange={e => {
                const customer = customers.find(c => c.id === Number(e.target.value));
                if (customer) onCustomerSelect(customer);
              }}
              className="field appearance-none pr-10"
            >
              <option value="">Choose a customer...</option>
              {customers.map(c => (
                <option key={c.id} value={c.id}>
                  {c.name} ({PricingUtils.getCustomerTypeLabel(c.customerType)})
                </option>
              ))}
            </select>
            <ChevronDown className="pointer-events-none absolute right-3 top-1/2 h-5 w-5 -translate-y-1/2 text-moss" aria-hidden />
          </div>

          <button type="button" onClick={() => setShowForm(v => !v)} className="btn btn-primary whitespace-nowrap">
            <Plus className="h-4 w-4" aria-hidden />
            New customer
          </button>
        </div>

        {selected && (
          <p className="mt-2 text-sm text-moss">
            <span className={`badge mr-2 ${PricingUtils.getCustomerTypeBadge(selected.customerType)}`}>
              {PricingUtils.getCustomerTypeLabel(selected.customerType)}
            </span>
            {selected.customerType === CustomerType.Wholesale
              ? 'Wholesale discounts are applied automatically.'
              : 'Retail customers pay the listed price.'}
          </p>
        )}
      </div>

      {showForm && (
        <form onSubmit={handleCreate} className="space-y-4 rounded-lg border border-mist bg-mist/30 p-4">
          <div className="grid gap-4 md:grid-cols-2">
            <div>
              <label htmlFor="new-name" className="mb-1 block text-sm font-medium">Name</label>
              <input
                id="new-name"
                className="field"
                value={form.name}
                maxLength={100}
                onChange={e => setForm(f => ({ ...f, name: e.target.value }))}
                placeholder="Customer name"
                required
              />
            </div>
            <div>
              <label htmlFor="new-email" className="mb-1 block text-sm font-medium">Email</label>
              <input
                id="new-email"
                type="email"
                className="field"
                value={form.email}
                maxLength={255}
                onChange={e => setForm(f => ({ ...f, email: e.target.value }))}
                placeholder="customer@example.com"
                required
              />
            </div>
          </div>

          <div>
            <label htmlFor="new-type" className="mb-1 block text-sm font-medium">Customer type</label>
            <select
              id="new-type"
              className="field"
              value={form.customerType}
              onChange={e => setForm(f => ({ ...f, customerType: Number(e.target.value) as CustomerType }))}
            >
              <option value={CustomerType.Retail}>Retail</option>
              <option value={CustomerType.Wholesale}>Wholesale</option>
            </select>
          </div>

          {error && (
            <p role="alert" className="rounded-lg bg-wheat px-3 py-2 text-sm">
              {error}
            </p>
          )}

          <div className="flex gap-2">
            <button type="submit" disabled={creating} className="btn btn-primary flex-1">
              {creating ? 'Creating...' : 'Create customer'}
            </button>
            <button type="button" onClick={() => setShowForm(false)} className="btn btn-secondary">
              Cancel
            </button>
          </div>
        </form>
      )}
    </div>
  );
};

export default CustomerSelector;
