
using TestOracleToPostgre.Context;

using Microsoft.EntityFrameworkCore;
using System.Reflection;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.EntityFrameworkCore.Metadata;

namespace genericRepository
{

    public class GenericRepository
    {

        private readonly DbContextOptions<ModelContext> _oracleOptions;
        private readonly DbContextOptions<ModelContext> _postgresOptions;

        public GenericRepository(DbContextOptions<ModelContext> oracleOptions, DbContextOptions<ModelContext> postgresOptions)
        {
            _oracleOptions = oracleOptions;
            _postgresOptions = postgresOptions;
        }

        public void MoveAllDataFromAllEntitys()
        {
            List<IEntityType> entityToMove = new List<IEntityType>();
            List<IEntityType> entityUntracked = new List<IEntityType>();
            List<IEntityType> entityToRemove = new List<IEntityType>();
            List<IEntityType> entityCouldNotMove = new List<IEntityType>();
            List<IEntityType> entityWithoutPk = new List<IEntityType>();
            List<List<IEntityType>> listWithEntityToInclude = new List<List<IEntityType>>();

            foreach (var entity in GetAllEntityTypes(_oracleOptions))
            {
                if (entity.FindPrimaryKey() != null && entity.ClrType.Name != "Dictionary`2" && CheckIfTableInDatabaseIsEmpty(entity, _oracleOptions) != true)
                {
                    entityToMove.Add(entity);
                }

                else if (entity.ClrType.Name == "Dictionary`2")
                {
                    entityUntracked.Add(entity);
                }
                else
                {
                    entityWithoutPk.Add(entity);
                }

            }

            listWithEntityToInclude = SortUntrackedEntityKeys(entityUntracked);

            while (entityToMove.Count() > 0)
            {
                foreach (var entity in entityToMove)
                {
                    if (CheckIfEntityNeedIncludes(entity, listWithEntityToInclude) != true && CheckTableWithConstraint(entity))
                    {
                        for (int index = 0; index < 5000000; index += 100000)
                        {
                            var dataToMove = GetAllFromEntity(entity, _oracleOptions, index);
                            if (dataToMove.Count() > 0)
                            {
                                if (InsertData(dataToMove, _postgresOptions))
                                {
                                    System.Console.WriteLine("Flyttad {0} {1}", entity.ClrType.Name, index);
                                }
                                else { System.Console.WriteLine("Fel {0}", entity.ClrType.Name); break; }
                            }

                        }

                    }

                    else if (CheckTableWithConstraint(entity) != true)
                    {
                        continue;
                    }
                    else if (CheckIfEntityNeedIncludes(entity, listWithEntityToInclude))
                    {
                        var includes = GetEntitysToInclude(entity, listWithEntityToInclude);
                        if (CheckIfEntityCanInclude(entity, includes))
                        {

                            for (int index = 0; index < 5000000; index += 100000)
                            {
                                var dataToMove = GetAllFromEntityFk(entity, includes, _oracleOptions, index);
                                if (dataToMove.Count() > 0)
                                {
                                    if (InsertData(dataToMove, _postgresOptions))
                                    {
                                        System.Console.WriteLine("Flyttad {0} {1}", entity.ClrType.Name, index);
                                    }
                                    else { System.Console.WriteLine("Fel {0}", entity.ClrType.Name); break; }
                                }
                                else { entityToRemove.Add(entity); }

                            }

                        }
                        else
                        {

                            var isEmpty = CheckIfTableInDatabaseIsEmpty(entity, _postgresOptions);
                            if (isEmpty != true)
                            {
                                var pk = entity.FindPrimaryKey().Properties[0].Name;
                                for (int index = 0; index < 5000000; index += 100000)
                                {
                                    var oracleItems = GetAllFromEntity(entity, _oracleOptions, index);
                                    var postgresItems = GetAllFromEntity(entity, _postgresOptions, index);

                                    List<string> pks = new List<string>();
                                    foreach (var item in oracleItems)
                                    {
                                        var prop = item.GetType().GetProperty(pk).Name;
                                        pks.Add(prop);

                                    }
                                    var dataToMove = getDifferenceBetweenListsByPk(oracleItems, postgresItems, pks);
                                    if (dataToMove.Count() > 0)
                                    {
                                        if (InsertData(dataToMove, _postgresOptions))
                                        {
                                            System.Console.WriteLine("Flyttad");
                                        }
                                    }
                                }
                                entityToRemove.Add(entity);

                            }

                        }
                    }

                }
                foreach (var item in entityToRemove)
                {
                    var result = entityToMove.Find(e => e.ClrType.Name == item.ClrType.Name);

                    if (result != null)
                    {
                        entityToMove.Remove(result);
                    }
                }

                System.Console.WriteLine(entityToMove.Count());
                System.Console.WriteLine(entityCouldNotMove.Count());
                entityToRemove.Clear();
            }
        }


        public bool CheckIfTableInDatabaseIsEmpty(IEntityType entityType, DbContextOptions<ModelContext> optionsbuilder)
        {
            using (var _context = new ModelContext(optionsbuilder))
            {
                bool isEmpty = true;
                try
                {
                    var queryItem = (IQueryable<object>)_context.Query(entityType.Name, entityType.ClrType);
                    if (queryItem.Any())
                    {
                        isEmpty = false;
                    }

                    return isEmpty;
                }
                catch (System.Exception)
                {

                    return true;
                }

            }

        }

        public List<IEntityType> GetAllEntityTypes(DbContextOptions<ModelContext> optionsbuilder)
        {
            using (var context = new ModelContext(optionsbuilder))
            {
                return context.Model.GetEntityTypes().ToList();
            }

        }

        public List<Object> GetAllFromEntity(IEntityType entityType, DbContextOptions<ModelContext> optionsbuilder, int index)
        {

            using (var _context = new ModelContext(optionsbuilder))
            {
                if (entityType.FindPrimaryKey() == null)
                {
                    return null;

                }

                else if (entityType.FindPrimaryKey() != null)
                {
                    var queryItem = (IQueryable<object>)_context.Query(entityType.Name, entityType.ClrType);

                    try
                    {
                        //return queryItem.ToList();
                        return queryItem.Skip(index).Take(100000).ToList();
                    }
                    catch (System.Exception e)
                    {

                        System.Console.WriteLine(e.InnerException.TargetSite);
                        return null;

                    }

                }
                else { return null; }

            }
        }

        public List<Object> GetAllFromEntityFk(IEntityType entityType, List<IEntityType> entityToInclude, DbContextOptions<ModelContext> optionsbuilder, int index)
        {

            using (var _context = new ModelContext(optionsbuilder))
            {
                if (entityType.FindPrimaryKey() == null)
                {
                    return null;

                }

                else if (entityType.FindPrimaryKey().GetKeyType() != typeof(System.Object[]))
                {
                    var queryItem = (IQueryable<object>)_context.Query(entityType.Name, entityType.ClrType);

                    var properties = entityType.ClrType.GetProperties().Where(x => x.PropertyType.IsGenericType && entityToInclude.Any(y => y.ClrType == x.PropertyType.GenericTypeArguments[0]));

                    foreach (var property in properties)
                    {

                        queryItem = queryItem.Include(property.Name).IgnoreAutoIncludes();

                    }

                    try
                    {
                        return queryItem.Skip(index).Take(100000).ToList();
                    }
                    catch (System.Exception)
                    {
                        return null;
                    }

                }
                else { return null; }
            }

        }

        public bool InsertData(IEnumerable<object> list, DbContextOptions<ModelContext> optionsbuilder)
        {
            using (var _context = new ModelContext(optionsbuilder))
            {
                if (list != null)
                {
                    try
                    {
                        _context.AddRange(list);
                        _context.SaveChanges();

                        return true;
                    }
                    catch (Exception e)
                    {
                        System.Console.WriteLine(e.InnerException.Source);
                        return false;
                    }
                }
                else { return false; }

            }

        }

        public List<Object> getDifferenceBetweenListsByPk(List<Object> oracleData, List<Object> postgresData, List<string> pks)
        {
            List<Object> data = new();
            data = oracleData.Except(postgresData, new PkEqualityComparer(pks)).ToList();
            return data;
        }

        public List<IEntityType> GetEntitysToInclude(IEntityType entity, List<List<IEntityType>> includes)
        {
            for (var i = 0; i < includes.Count(); i++)
            {
                var exist = includes[i].Find(e => e.ClrType.Name == entity.ClrType.Name);
                if (exist != null)
                {

                    return includes[i];
                }
                else
                {
                    continue;
                }
            }

            return null;

        }
        public bool CheckIfEntityCanInclude(IEntityType entity, List<IEntityType> includes)
        {
            var properties = entity.ClrType.GetProperties().Where(p => !p.PropertyType.IsClass && p.GetGetMethod().IsVirtual);
            foreach (var item in includes)
            {
                var exist = properties.Any(k => k.PropertyType.GenericTypeArguments[0].Name == item.ClrType.Name);
                if (exist || item.ClrType.Name == entity.ClrType.Name)
                {
                    continue;
                }
                else { return false; }
            }
            return true;
        }


        public List<List<IEntityType>> SortUntrackedEntityKeys(List<IEntityType> entitys)
        {

            List<List<IEntityType>> entitiesThatNeedInclude = new();

            foreach (var entity in entitys)
            {

                List<IEntityType> entityFk = new List<IEntityType>();
                var keys = entity.GetDeclaredForeignKeys();
                foreach (var key in keys)
                {
                    entityFk.Add(key.PrincipalEntityType);
                }
                if (entitiesThatNeedInclude.Any())
                {
                    bool checkUniqueValues = false;
                    for (int i = 0; i < entitiesThatNeedInclude.Count; i++)
                    {
                        checkUniqueValues = entitiesThatNeedInclude[i].Intersect(entityFk).Any();
                        if (checkUniqueValues)
                        {
                            entitiesThatNeedInclude[i] = entityFk.Union(entitiesThatNeedInclude[i]).ToList();
                        }
                    }
                    if (!checkUniqueValues)
                    {
                        entitiesThatNeedInclude.Add(entityFk);
                    }
                }

                else
                {
                    entitiesThatNeedInclude.Add(entityFk);
                }

            }

            return entitiesThatNeedInclude;
        }

        public bool CheckTableWithConstraint(IEntityType entity)
        {

            var keys = entity.GetDeclaredForeignKeys();

            foreach (var item in keys)
            {
                var resultOracle = CheckIfTableInDatabaseIsEmpty(item.PrincipalEntityType, _oracleOptions);
                var result = CheckIfTableInDatabaseIsEmpty(item.PrincipalEntityType, _postgresOptions);
                if (resultOracle == false && result == false || resultOracle && result)
                {
                    continue;
                }

                else
                {
                    return false;
                }

            }


            return true;
        }


        public bool CheckIfEntityNeedIncludes(IEntityType entity, List<List<IEntityType>> listWithEntityToInclude)
        {
            for (var i = 0; i < listWithEntityToInclude.Count(); i++)
            {
                var exist = listWithEntityToInclude[i].Find(e => e.ClrType.Name == entity.ClrType.Name);
                if (exist != null)
                {
                    return true;

                }
                else
                {
                    continue;
                }
            }

            return false;
        }

        public List<object> CompareData(IEntityType entity, int index)
        {
            var pk = entity.FindPrimaryKey().Properties[0].Name;

            var oracleItems = GetAllFromEntity(entity, _oracleOptions, index);
            var postgresItems = GetAllFromEntity(entity, _postgresOptions, index);

            List<string> pks = new List<string>();
            foreach (var item in oracleItems)
            {
                var prop = item.GetType().GetProperty(pk).Name;
                pks.Add(prop);

            }
            var dataToMove = getDifferenceBetweenListsByPk(oracleItems, postgresItems, pks);

            return dataToMove;
        }


    }

    public static class DynamicContextExtensions
    {
        public static IQueryable Query(this ModelContext context, string entityName) =>
            context.Query(entityName, context.Model.FindEntityType(entityName).ClrType);

        static readonly MethodInfo SetMethod =
            typeof(DbContext).GetMethod(nameof(ModelContext.Set), 1, new[] { typeof(string) }) ??
            throw new Exception($"Type not found: DbContext.Set");

        public static IQueryable Query(this ModelContext context, string entityName, Type entityType) =>
            (IQueryable)SetMethod.MakeGenericMethod(entityType)?.Invoke(context, new[] { entityName }) ??
            throw new Exception($"Type not found: {entityType.FullName}");

    }

}

