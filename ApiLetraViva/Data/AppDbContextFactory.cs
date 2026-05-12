using ApiLetraViva.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using System;


namespace ApiLetraViva.Data
{
    public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();

            var connectionString =
                "Host=yamabiko.proxy.rlwy.net;" +
                "Port=45610;" +
                "Database=railway;" +
                "Username=postgres;" +
                "Password=hyukTiaGOigNuvyUqQJIgbFCmYfYvieR;" +
                "SSL Mode=Require;" +
                "Trust Server Certificate=true";

            optionsBuilder.UseNpgsql(connectionString);

            return new AppDbContext(optionsBuilder.Options);
        }
    }
}
