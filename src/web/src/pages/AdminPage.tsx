import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api } from "../api";
export function AdminPage() {
  const [name, setName] = useState(""); const [sku, setSku] = useState(""); const [price, setPrice] = useState("0"); const [stock, setStock] = useState("0");
  const client = useQueryClient(); const orders = useQuery({ queryKey: ["admin-orders"], queryFn: api.adminOrders });
  const create = useMutation({ mutationFn: () => api.createProduct({ name, sku, description: "", price: Number(price), stockQuantity: Number(stock) }), onSuccess: () => { setName(""); setSku(""); client.invalidateQueries({ queryKey: ["products"] }); } });
  const update = useMutation({ mutationFn: ({ id, status }: { id: string; status: string }) => api.updateOrderStatus(id, status), onSuccess: () => client.invalidateQueries({ queryKey: ["admin-orders"] }) });
  return <section><div className="page-heading"><div><div className="eyebrow">OPERATIONS</div><h1>Admin console</h1></div></div>
    <div className="admin-grid"><div className="panel"><h2>Add product</h2><form onSubmit={(e) => { e.preventDefault(); create.mutate(); }}><label>SKU<input required value={sku} onChange={(e) => setSku(e.target.value)} /></label><label>Name<input required value={name} onChange={(e) => setName(e.target.value)} /></label><div className="form-row"><label>Price<input type="number" min="0" step="0.01" value={price} onChange={(e) => setPrice(e.target.value)} /></label><label>Stock<input type="number" min="0" value={stock} onChange={(e) => setStock(e.target.value)} /></label></div><button className="primary" disabled={create.isPending}>Create product</button></form></div>
      <div className="panel"><h2>Recent orders</h2>{orders.data?.items.map((order) => <div className="admin-order" key={order.id}><div><strong>#{order.id.slice(0, 8)}</strong><span>${order.totalAmount.toFixed(2)}</span></div><select value={order.status} onChange={(e) => update.mutate({ id: order.id, status: e.target.value })}><option>Pending</option><option>Confirmed</option><option>Shipped</option><option>Delivered</option><option>Cancelled</option></select></div>)}</div></div>
  </section>;
}
