import { useQuery } from "@tanstack/react-query";
import { Link, useParams } from "react-router-dom";
import { api } from "../api";

export function OrderDetailsPage() {
  const { id = "" } = useParams();
  const order = useQuery({ queryKey: ["order", id], queryFn: () => api.order(id), enabled: Boolean(id) });
  if (order.isLoading) return <p className="muted">Loading order...</p>;
  if (order.error || !order.data) return <p className="error">{order.error?.message ?? "Order not found"}</p>;
  const data = order.data;
  return <section>
    <Link className="muted back-link" to="/orders">← Back to orders</Link>
    <div className="page-heading"><div><div className="eyebrow">ORDER DETAILS</div><h1>#{data.id.slice(0, 8)}</h1></div><span className={`status ${data.status.toLowerCase()}`}>{data.status}</span></div>
    <div className="detail-grid"><div className="panel"><h2>Items</h2>{data.items.map((item) => <div className="admin-order" key={item.productId}><div><strong>{item.productName}</strong><span>× {item.quantity}</span></div><strong>${(item.unitPrice * item.quantity).toFixed(2)}</strong></div>)}</div>
      <div className="panel"><h2>Summary</h2><div className="summary-row"><span>Shipping</span><strong>{data.shippingMethod}</strong></div><div className="summary-row"><span>Shipping cost</span><strong>${data.shippingCost.toFixed(2)}</strong></div><div className="summary-row total"><span>Total</span><strong>${data.totalAmount.toFixed(2)}</strong></div></div></div>
  </section>;
}
