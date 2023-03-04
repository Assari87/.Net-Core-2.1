using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Linq.Expressions;
using System.Reflection;

namespace Check.Core.Extensions
{
    public static class LinqExtensions
    {
        public static IHttpContextAccessor httpContextAccessor;

        public static IQueryable<T> OrderBy<T>(this IQueryable<T> source, string orderField, int orderSort)
        {
            try
            {
                var type = typeof(T);

                if (orderField == null || orderField == "null" || orderField == "undefined")
                {
                    var createDateField = type.GetProperties().FirstOrDefault(p => p.Name.ToLower() == "createdate");
                    if (createDateField != null)
                    {
                        return source.OrderBy(createDateField.Name, 2);
                    }
                    else
                    {
                        return source.OrderBy(type.GetProperties().First().Name, 2);
                    }
                }

                var parameter = Expression.Parameter(type, "p");
                PropertyInfo property;

                property = type.GetProperties()
                    .SingleOrDefault(p => p.Name.ToLower() == orderField.ToLower());

                if (property != null)
                {
                    var orderFieldProp = Expression.MakeMemberAccess(parameter, property);
                    var orderByMethod = (orderSort == 1 ? "OrderBy" : "OrderByDescending");

                    var orderByExp = Expression.Lambda(orderFieldProp, parameter);
                    MethodCallExpression resultExp = Expression.Call(typeof(Queryable),
                                                                     orderByMethod,
                                                                     new[] { type, property.PropertyType }, source.Expression,
                                                                     Expression.Quote(orderByExp));
                    return source.Provider.CreateQuery<T>(resultExp);
                }

                return source;
            }
            catch
            {
                return source;
            }
        }

        public static IQueryable<T> OrderByName<T>(this IQueryable<T> source, string propertyName, Boolean isDescending)
        {

            if (source == null) throw new ArgumentNullException("source");
            if (propertyName == null) throw new ArgumentNullException("propertyName");

            Type type = typeof(T);
            ParameterExpression arg = Expression.Parameter(type, "x");

            PropertyInfo pi = type.GetProperty(propertyName);
            Expression expr = Expression.Property(arg, pi);
            type = pi.PropertyType;

            Type delegateType = typeof(Func<,>).MakeGenericType(typeof(T), type);
            LambdaExpression lambda = Expression.Lambda(delegateType, expr, arg);

            String methodName = isDescending ? "OrderByDescending" : "OrderBy";
            object result = typeof(Queryable).GetMethods().Single(
                method => method.Name == methodName
                        && method.IsGenericMethodDefinition
                        && method.GetGenericArguments().Length == 2
                        && method.GetParameters().Length == 2)
                .MakeGenericMethod(typeof(T), type)
                .Invoke(null, new object[] { source, lambda });
            return (IQueryable<T>)result;
        }
        public static IQueryable<T> OrderByDynamic<T>(this IQueryable<T> source, string propertyName, bool isDescending)
        {
            if (source == null) throw new ArgumentNullException("source");
            if (propertyName == null) throw new ArgumentNullException("propertyName");

            object result = isDescending ?
                            source.OrderByDescending(c => (c as IDictionary<string, object>)[propertyName]) :
                            source.OrderBy(c => (c as IDictionary<string, object>)[propertyName]);

            return (IQueryable<T>)result;
        }
        public static List<TitleValue<TKey>> ProjectToTitleValue<T, TKey>(this List<T> list)
        {
            return list.Select(item => new TitleValue<TKey>()
            {
                Value = item.GetKey<TKey>(),
                Title = item.ToString(),
            }).ToList();
        }

        public static IQueryable<T> GetAll<T>(this DbSet<T> dbSet, bool asNoTracking = false) where T : BaseEntity
        {
            var branchId = httpContextAccessor.BranchId();

            var isBranchId = typeof(T).GetProperty("BranchID") != null;

            if (isBranchId && branchId > 0)
                return asNoTracking ? dbSet.Where(c => c.BranchID == 0 || c.BranchID == branchId).AsNoTracking() : dbSet.Where(c => c.BranchID == 0 || c.BranchID == branchId);

            return asNoTracking ? dbSet.AsNoTracking() : dbSet;
        }
        public static IQueryable<T> GetAllIgnoreFilter<T>(this DbSet<T> dbSet, bool asNoTracking = false) where T : BaseEntity
        {
            return asNoTracking ? dbSet.AsNoTracking() : dbSet;
        }

        public static IQueryable<T> GetAll<T>(this DbContext context, bool asNoTracking = false) where T : BaseEntity
        {
            return asNoTracking ? context.Set<T>().GetAll(asNoTracking) : context.Set<T>().GetAll();
        }
        public static IQueryable<T> GetAllIgnoreFilter<T>(this DbContext context, bool asNoTracking = false) where T : BaseEntity
        {
            return asNoTracking ? context.Set<T>().GetAllIgnoreFilter(asNoTracking) : context.Set<T>().GetAllIgnoreFilter();
        }
        public static IQueryable<T> GetAllLimitedByRegion<T>(this DbContext context, List<string> regionIdList, bool asNoTracking = false)
            where T : BaseEntity, Check.Models.Base.Entities.IActivityRegionLimit<T>
        {
            return asNoTracking ? context.Set<T>().GetAll(asNoTracking)
                .Where(d => regionIdList.Contains(d.Regionkey()(d))) :
                context.Set<T>().GetAll();
        }
        public static IQueryable<T> GetAllLimitedByRegionIgnoreFilter<T>(this DbContext context, List<string> regionIdList, bool asNoTracking = false)
            where T : BaseEntity, Check.Models.Base.Entities.IActivityRegionLimit<T>
        {
            return asNoTracking ? context.Set<T>().GetAllIgnoreFilter(asNoTracking)
                                .Where(d => regionIdList.Contains(d.Regionkey()(d))) :
                                context.Set<T>().GetAllIgnoreFilter();
        }
        public static T FindByID<T>(this IQueryable<T> query, long id) where T : BaseEntity
        {
            return query.FirstOrDefault(q => q.ID == id);
        }

        /// <summary>
        /// دریافت کلید و کد بعدی
        /// </summary>
        /// <typeparam name="TEntity"></typeparam>
        /// <param name="dbset"></param>
        /// <returns></returns>
        public static string GetNextKey<TEntity>(this DbSet<TEntity> dbset, bool? ignoreFilter = false) where TEntity : KeyEntity
        {
            try
            {
                if (ignoreFilter != null && ignoreFilter == true)
                {
                    var branchId = httpContextAccessor.BranchId();

                    var isBranchId = typeof(TEntity).GetProperty("BranchID") != null;


                    var latestRecord = dbset.Where(c => c.BranchID == 0 || c.BranchID == branchId).OrderByDescending(d => Convert.ToInt64(d.Key.Trim())).FirstOrDefault();
                    if (latestRecord == null)
                        return "1";

                    var latestKey = Convert.ToInt64(latestRecord.Key);
                    return (latestKey + 1).ToString();
                }
                else
                {

                    var latestRecord = dbset.OrderByDescending(d => Convert.ToInt64(d.Key.Trim())).FirstOrDefault();
                    if (latestRecord == null)
                        return "1";

                    var latestKey = Convert.ToInt64(latestRecord.Key);
                    return (latestKey + 1).ToString();
                }
            }
            catch(Exception ex)
            {
                return "NaN";
            }
        }
    }
}
