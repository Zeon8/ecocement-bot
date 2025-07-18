﻿using EcocementBot.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace EcocementBot.Data;

public class ApplicationDbContext : DbContext
{
    public DbSet<Client> Clients { get; set; }

    public DbSet<Mark> Marks { get; set; }

    public DbSet<User> Users { get; set; }

    public ApplicationDbContext(DbContextOptions options) : base(options)
    {
    }
}
