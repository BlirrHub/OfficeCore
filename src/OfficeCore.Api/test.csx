using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OfficeCore.Api.Data;
using OfficeCore.Api.Models;

var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
optionsBuilder.UseNpgsql(""Host=localhost;Database=OfficeCore;Username=postgres;Password=postgres"");
using var db = new AppDbContext(optionsBuilder.Options);
foreach (var bm in db.BoardMembers)
{
    Console.WriteLine($""BM Id: {bm.Id}, BoardId: {bm.BoardId}, UserId: {bm.UserId}, Role: {bm.Role}"");
}
