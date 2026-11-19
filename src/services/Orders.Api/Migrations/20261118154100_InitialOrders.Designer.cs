using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Orders.Api.Infrastructure;

#nullable disable

namespace Orders.Api.Migrations;

[DbContext(typeof(OrdersDbContext))]
[Migration("20261118154100_InitialOrders")]
partial class InitialOrders
{
    protected override void BuildTargetModel(ModelBuilder modelBuilder)
    {
        modelBuilder
            .HasAnnotation("ProductVersion", "10.0.0");
    }
}
