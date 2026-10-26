import { Search, X } from 'lucide-react';

interface SearchBarProps {
  value: string;
  onChange: (value: string) => void;
}

const SearchBar: React.FC<SearchBarProps> = ({ value, onChange }) => (
  <div className="relative">
    <Search className="pointer-events-none absolute left-3 top-1/2 h-5 w-5 -translate-y-1/2 text-moss" aria-hidden />
    <input
      type="search"
      value={value}
      onChange={e => onChange(e.target.value)}
      placeholder="Search products (typos are fine)"
      aria-label="Search products"
      className="field pl-11 pr-10"
    />
    {value && (
      <button
        type="button"
        onClick={() => onChange('')}
        aria-label="Clear search"
        className="absolute right-2 top-1/2 -translate-y-1/2 rounded-full p-1 text-moss hover:bg-mist hover:text-bark"
      >
        <X className="h-4 w-4" aria-hidden />
      </button>
    )}
  </div>
);

export default SearchBar;
