export function ErrorMessage({ error }: { error: unknown }) {
  return error ? <p role="alert" className="error">{error instanceof Error ? error.message : 'Something went wrong.'}</p> : null;
}
export function Pagination({ page, total, size, change }: { page: number; total: number; size: number; change: (n: number) => void }) {
  return <div className="pagination"><button disabled={page <= 1} onClick={() => change(page - 1)}>Previous</button>
    <span>Page {page} · {total} results</span><button disabled={page * size >= total} onClick={() => change(page + 1)}>Next</button></div>;
}
export const money = (value: number) => new Intl.NumberFormat('en-AU', { style: 'currency', currency: 'AUD', maximumFractionDigits: 0 }).format(value);
