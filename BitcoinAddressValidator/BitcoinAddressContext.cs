using Microsoft.EntityFrameworkCore;

namespace BitcoinAddressValidator
{
    public class BitcoinAddressContext : DbContext
    {
        public DbSet<BitcoinAddressRecord> Addresses { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlite("Data Source=bitcoin_addresses.db");
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<BitcoinAddressRecord>()
                .HasIndex(b => b.Address)
                .IsUnique(); // Ensure the Address field is unique
        }


        public void InitializeDatabase()
        {
            this.Database.EnsureCreated(); // Ensures the database and tables are created
        }
    }

}
