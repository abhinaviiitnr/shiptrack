using Microsoft.EntityFrameworkCore;
using Npgsql;
using Orders.Api.Domain;

namespace Orders.Api.Infrastructure;

public static class LargeOrderSeeder
{
    public static async Task SeedAsync(OrdersDbContext db, ILogger logger, CancellationToken ct = default)
    {
        var existingCount = await db.Orders.CountAsync(ct);
        if (existingCount >= 300_000)
        {
            logger.LogInformation("Database already contains {Count} orders. Skipping seeder.", existingCount);
            return;
        }

        logger.LogInformation("Starting large order seed (target: 300,000 orders)...");
        var connection = (NpgsqlConnection)db.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync(ct);
        }

        var sampleCustomers = Enumerable.Range(1, 1000)
            .Select(i => Guid.Parse($"a0000000-0000-0000-0000-{i:D12}"))
            .ToArray();

        var baseDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        const int totalOrders = 300_000;
        const int batchSize = 10_000;

        var random = new Random(42);

        for (var batchStart = 0; batchStart < totalOrders; batchStart += batchSize)
        {
            var currentBatchCount = Math.Min(batchSize, totalOrders - batchStart);
            var seededOrders = new List<SeededOrder>(currentBatchCount);

            // Use binary copy for orders table for maximum performance
            using (var writer = await connection.BeginBinaryImportAsync(
                "COPY \"Orders\" (\"Id\", \"CustomerId\", \"Status\", \"ShippingMethod\", \"ShippingCost\", \"TotalAmount\", \"CreatedAt\", \"UpdatedAt\") FROM STDIN (FORMAT BINARY)", ct))
            {
                for (var i = 0; i < currentBatchCount; i++)
                {
                    var orderIndex = batchStart + i;
                    var orderId = Guid.NewGuid();
                    var customerId = sampleCustomers[random.Next(sampleCustomers.Length)];
                    var status = (OrderStatus)(random.Next(1, 6)); // 1 to 5
                    var shippingMethod = (ShippingMethod)(random.Next(1, 3));
                    var shippingCost = shippingMethod == ShippingMethod.Standard ? 50m : 150m;
                    var total = (decimal)random.Next(100, 2000) + shippingCost;
                    var created = baseDate.AddMinutes(orderIndex * 2.5);
                    var productId = Guid.NewGuid();
                    var productName = $"Seeded Product {random.Next(1, 10000)}";
                    var unitPrice = total / random.Next(1, 4);

                    seededOrders.Add(new SeededOrder(
                        orderId, productId, productName, unitPrice));

                    await writer.StartRowAsync(ct);
                    await writer.WriteAsync(orderId, NpgsqlTypes.NpgsqlDbType.Uuid, ct);
                    await writer.WriteAsync(customerId, NpgsqlTypes.NpgsqlDbType.Uuid, ct);
                    await writer.WriteAsync((int)status, NpgsqlTypes.NpgsqlDbType.Integer, ct);
                    await writer.WriteAsync((int)shippingMethod, NpgsqlTypes.NpgsqlDbType.Integer, ct);
                    await writer.WriteAsync(shippingCost, NpgsqlTypes.NpgsqlDbType.Numeric, ct);
                    await writer.WriteAsync(total, NpgsqlTypes.NpgsqlDbType.Numeric, ct);
                    await writer.WriteAsync(created, NpgsqlTypes.NpgsqlDbType.TimestampTz, ct);
                    await writer.WriteAsync(created, NpgsqlTypes.NpgsqlDbType.TimestampTz, ct);
                }
                await writer.CompleteAsync(ct);
            }

            using (var writer = await connection.BeginBinaryImportAsync(
                "COPY \"OrderItems\" (\"Id\", \"OrderId\", \"ProductId\", \"ProductNameSnapshot\", \"UnitPriceSnapshot\", \"Quantity\") FROM STDIN (FORMAT BINARY)", ct))
            {
                foreach (var seededOrder in seededOrders)
                {
                    await writer.StartRowAsync(ct);
                    await writer.WriteAsync(Guid.NewGuid(), NpgsqlTypes.NpgsqlDbType.Uuid, ct);
                    await writer.WriteAsync(seededOrder.OrderId, NpgsqlTypes.NpgsqlDbType.Uuid, ct);
                    await writer.WriteAsync(seededOrder.ProductId, NpgsqlTypes.NpgsqlDbType.Uuid, ct);
                    await writer.WriteAsync(seededOrder.ProductName, NpgsqlTypes.NpgsqlDbType.Varchar, ct);
                    await writer.WriteAsync(seededOrder.UnitPrice, NpgsqlTypes.NpgsqlDbType.Numeric, ct);
                    await writer.WriteAsync(1, NpgsqlTypes.NpgsqlDbType.Integer, ct);
                }
                await writer.CompleteAsync(ct);
            }

            logger.LogInformation("Seeded {Count} / {Total} orders...", batchStart + currentBatchCount, totalOrders);
        }

        logger.LogInformation("Large order seed completed successfully.");
    }

    private sealed record SeededOrder(
        Guid OrderId,
        Guid ProductId,
        string ProductName,
        decimal UnitPrice);
}
