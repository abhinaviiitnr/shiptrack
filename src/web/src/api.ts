export type Role = "Customer" | "Admin";

export interface AuthResponse {
  token: string;
  role: Role;
  userId: string;
}

export interface Product {
  id: string;
  sku: string;
  name: string;
  description: string;
  price: number;
  stockQuantity: number;
  createdAt: string;
}

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  total: number;
}

export interface OrderSummary {
  id: string;
  status: string;
  shippingMethod: string;
  shippingCost: number;
  totalAmount: number;
  createdAt: string;
}

export interface Order extends OrderSummary {
  customerId: string;
  updatedAt: string;
  items: { productId: string; productName: string; unitPrice: number; quantity: number }[];
  statusHistory: { fromStatus: string; toStatus: string; changedAt: string; changedBy: string }[];
}

export interface CursorResult<T> {
  items: T[];
  nextCursor: string | null;
}

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL ?? "";

export async function request<T>(path: string, init: RequestInit = {}): Promise<T> {
  const token = localStorage.getItem("shiptrack.token");
  const headers = new Headers(init.headers);
  headers.set("Content-Type", "application/json");
  if (token) headers.set("Authorization", `Bearer ${token}`);

  const response = await fetch(`${API_BASE_URL}${path}`, { ...init, headers });
  if (!response.ok) {
    const problem = await response.json().catch(() => null) as { detail?: string; title?: string } | null;
    throw new Error(problem?.detail ?? problem?.title ?? `Request failed (${response.status})`);
  }
  if (response.status === 204) return undefined as T;
  return response.json() as Promise<T>;
}

export const api = {
  login: (email: string, password: string) =>
    request<AuthResponse>("/api/auth/login", { method: "POST", body: JSON.stringify({ email, password }) }),
  register: (email: string, password: string) =>
    request<AuthResponse>("/api/auth/register", { method: "POST", body: JSON.stringify({ email, password }) }),
  products: (page = 1, name = "") =>
    request<PagedResult<Product>>(`/api/products?page=${page}&pageSize=24${name ? `&name=${encodeURIComponent(name)}` : ""}`),
  productsByIds: (ids: string[]) =>
    request<Product[]>(`/api/products/batch?${ids.map((id) => `ids=${encodeURIComponent(id)}`).join("&")}`),
  createProduct: (product: Omit<Product, "id" | "createdAt">) =>
    request<Product>("/api/products", { method: "POST", body: JSON.stringify(product) }),
  createOrder: (items: { productId: string; quantity: number }[], shippingMethod: number) =>
    request<Order>("/api/orders", { method: "POST", body: JSON.stringify({ items, shippingMethod }) }),
  myOrders: () => request<CursorResult<OrderSummary>>("/api/orders/mine?pageSize=50"),
  order: (id: string) => request<Order>(`/api/orders/${id}`),
  adminOrders: () => request<CursorResult<OrderSummary>>("/api/orders?pageSize=50"),
  updateOrderStatus: (id: string, status: string) =>
    request<void>(`/api/orders/${id}/status`, { method: "PATCH", body: JSON.stringify({ status }) }),
};
