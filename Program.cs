using TestOracleToPostgre.Context;
using Microsoft.EntityFrameworkCore;
using genericRepository;
using TestOracleToPostgre;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore.Metadata;
using System.Collections.Generic;

public class Program
{

    private static void Main(string[] args)
    {

        IConfigurationBuilder builder = new ConfigurationBuilder().AddJsonFile("appsettings.json", false, true);
        IConfigurationRoot root = builder.Build();

        string oracleConnectionString = root.GetConnectionString("oracle");
        string postgresConnectionString = root.GetConnectionString("postgres");

        var _oracleOptions = new DbContextOptionsBuilder<ModelContext>().UseOracle(oracleConnectionString).Options;
        var _postgresOptions = new DbContextOptionsBuilder<ModelContext>().UseNpgsql(postgresConnectionString).Options;

        var repository = new GenericRepository(_oracleOptions, _postgresOptions);
        repository.MoveAllDataFromAllEntitys();

    }

}