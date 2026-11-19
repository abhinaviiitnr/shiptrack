import { useQuery } from "@tanstack/react-query";
import { Link } from "react-router-dom";
import { api } from "../api";
export function OrdersPage() {
  const orders = useQuery({ queryKey: ["orders"], queryFn: api.myOrders });
  return <section><div className="page-heading"><div><div className="eyebrow">ACCOUNT</div><h1>My orders</h1></div><Link className="primary" to="/">Continue shopping</Link></div>
    {orders.isLoading && <p className="muted">Loading orders...</p>}{orders.error && <p className="error">{orders.error.message}</p>}
    <div className="order-list">{orders.data?.items.map((order) => <Link className="order-row" to={`/orders/${order.id}`} key={order.id}><div><strong>#{order.id.slice(0, 8)}</strong><span>{new Date(order.createdAt).toLocaleDateString()}</span></div><span className={`status ${order.status.toLowerCase()}`}>{order.status}</span><strong>${order.totalAmount.toFixed(2)}</strong></Link>)}</div>
    {!orders.isLoading && !orders.data?.items.length && <div className="empty"><h2>No orders yet</h2><p className="muted">Your next delivery starts here.</p><Link className="primary" to="/">Browse catalogue</Link></div>}
  </section>;
}
