using Microsoft.EntityFrameworkCore;
using SCRM.API.Models.Entities;
using SCRM.SHARED.Models;

namespace SCRM.UI.Services.Data
{
    public class ClientDbContext : DbContext, IClientDbContext
    {
        public DbSet<Contact> Contacts { get; set; }
        public DbSet<WechatAccount> WechatAccounts { get; set; }
        public DbSet<Message> Messages { get; set; }
        public DbSet<SCRM.API.Models.Entities.MomentsTimeline> MomentsTimelines { get; set; }

        // Interface Implementation via Wrapper
        IClientDataSet<Contact> IClientDbContext.Contacts => new EfClientDataSet<Contact>(this);
        IClientDataSet<Message> IClientDbContext.Messages => new EfClientDataSet<Message>(this);
        IClientDataSet<SCRM.API.Models.Entities.MomentsTimeline> IClientDbContext.MomentsTimelines => new EfClientDataSet<SCRM.API.Models.Entities.MomentsTimeline>(this);

        public ClientDbContext(DbContextOptions<ClientDbContext> options) : base(options)
        {
            // Ensure Database is created in WASM
            Database.EnsureCreated();
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Break dependency chain: WechatAccount -> ApplicationUser -> SrClient -> Device (No Key)
            // On client side, we don't need the Owner (ApplicationUser) relationship.
            modelBuilder.Ignore<ApplicationUser>();
            modelBuilder.Ignore<SrClient>();
            modelBuilder.Entity<WechatAccount>().Ignore(w => w.Owner);
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                // Fallback for design-time
                optionsBuilder.UseSqlite("Data Source=client.db");
            }
        }
    }
}
