using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ClubManagement.Infrastructure.Migrations
{
    public partial class ApplicationDbContextModelSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "10.0.0")
                .HasAnnotation("Relational:MaxIdentifierLength", 63);

            NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);

            // Tenant configuration
            modelBuilder.Entity("ClubManagement.Core.Entities.Tenant", b =>
            {
                b.Property<Guid>("Id").ValueGeneratedOnAdd().HasColumnType("uuid");
                b.Property<string>("ConfigJson").HasColumnType("jsonb");
                b.Property<DateTime>("CreatedAt").HasColumnType("timestamp with time zone");
                b.Property<bool>("IsActive").HasColumnType("boolean");
                b.Property<string>("Name").IsRequired().HasColumnType("text");
                b.Property<string>("Subdomain").IsRequired().HasColumnType("text");
                b.Property<string>("StripeAccountId").HasColumnType("text");
                b.Property<DateTime>("UpdatedAt").HasColumnType("timestamp with time zone");

                b.HasKey("Id");
                b.HasIndex("Subdomain").IsUnique();
                b.ToTable("Tenants");
            });

            // Other entities would be configured similarly...
#pragma warning restore 612, 618
        }
    }
}
