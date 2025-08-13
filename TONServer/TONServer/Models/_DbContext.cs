using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace TONServer
{
    public class _DbContext : DbContext
    {
        public DbSet<Setting> Settings { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<Langs> Langs { get; set; }
        public DbSet<LangDesc> LangDescs { get; set; }
        public DbSet<Room> Rooms { get; set; }
        public DbSet<Image> Images { get; set; }
        public DbSet<ImageWeb> ImageWebs { get; set; }
        public DbSet<RoomWeb> RoomWebs { get; set; }

        public _DbContext(DbContextOptions<_DbContext> options) : base(options)
        {
            //При отсутствии базы данных автоматически создает ее
            //Database.EnsureCreated();
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<User>().HasIndex(b => b.Login).IsUnique();
            /*foreach (var property in modelBuilder.Model.GetEntityTypes().SelectMany(t => t.GetProperties()).Where(p => p.ClrType == typeof(decimal) || p.ClrType == typeof(decimal?)))
            {
                property.SetColumnType("decimal(18, 8)");
            }*/
        }

        public async Task<int> Execute(string query)
        {
            int result = 0;
            using (var conn = new SqlConnection(this.Database.GetDbConnection().ConnectionString))
            {
                conn.Open();
                using (var command = new SqlCommand(query, conn))
                {
                    result = await command.ExecuteNonQueryAsync();
                }
            }
            return result;
        }

        public DataTable Select(string query)
        {
            var result = new DataTable();
            using (var conn = new SqlConnection(this.Database.GetDbConnection().ConnectionString))
            {
                conn.Open();
                using (var adapter = new SqlDataAdapter(query, conn))
                {
                    var dset = new DataSet();
                    int i = adapter.Fill(dset);
                    result = dset.Tables[0];
                }
            }
            return result;
        }

        public List<T> Select<T>(string query)
        {
            var r = new List<T>();
            var recs = Select(query).Rows;
            foreach (DataRow rec in recs)
            {
                T item = Activator.CreateInstance<T>();
                foreach (PropertyInfo property in item.GetType().GetProperties())
                {
                    var value = rec[property.Name];
                    property.SetValue(item, value, null);
                }
                r.Add(item);
            }
            return r;
        }
    }

    public static class DecimalExtensions
    {
        public static string ToString(this decimal some, bool compactFormat)
        {
            if (compactFormat)
            {
                return some.ToString("# ### ##0.########");
            }
            else
            {
                return some.ToString();
            }
        }
    }
}
