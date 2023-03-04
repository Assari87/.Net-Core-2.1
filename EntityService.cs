using AutoMapper;
using AutoMapper.QueryableExtensions;
using Dapper;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using PersianDate.Standard;
using RestSharp;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Linq.Expressions;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace Check.Core.Services
{
    public abstract class EntityService<TDbContext, TEntity, TKey> : ServiceBase, IEntityService<TDbContext, TEntity, TKey>
        where TDbContext : CheckDbContext
        where TEntity : class
    {
        protected TDbContext Db { get; set; }
		
        public IHttpContextAccessor HttpContextAccessor { get; set; }
        private readonly long _branchId;
        
        protected IQueryable<TEntity> DbSetToQueryable()
        {

            IQueryable<TEntity> queryable = Db.Set<TEntity>().AsQueryable();
            var isBranchId = typeof(TEntity).GetProperty("BranchID") != null;

            if (isBranchId && _branchId > 0)
            {                
                var item = Expression.Parameter(typeof(TEntity), "item");
                var property = Expression.Property(item, "BranchID");
                var zero = Expression.Constant((long)0);
                var branchId = Expression.Constant(_branchId);
                var equalZero = Expression.Equal(property, zero);
                var equalBrachId = Expression.Equal(property, branchId);
                var exp = Expression.Or(equalZero, equalBrachId);

                var lambda = Expression.Lambda<Func<TEntity, bool>>(exp, item);

                queryable = queryable.Where(lambda).Cast<TEntity>();
            }

            return queryable;
        }//=> Db.Set<TEntity>().GetAll();

        public Guid UserId =>
            Guid.Parse(
                HttpContextAccessor.HttpContext == null || HttpContextAccessor.HttpContext.User == null || !HttpContextAccessor.HttpContext.User.Identity.IsAuthenticated ?
                Constants.CITIZEN_USER_ID :
                UserManager.GetUserId(HttpContextAccessor.HttpContext.User));

        public EntityService(
            IHttpContextAccessor httpContextAccessor,
            UserManager<User> userManager,
            TDbContext db
        ) : base(userManager)
        {
            this.HttpContextAccessor = httpContextAccessor;
            this.Db = db;
            _branchId = HttpContextAccessor.BranchId();
        }

        public List<string> GetUserRegionList(string activityRegionTypeKey)
        {

            var queryable = CheckBranchID<UserActivityRegion>();
            return queryable.Where(p => p.UserId == UserId.ToString() && p.ActivityRegionTypeKey == activityRegionTypeKey).Select(p => p.RegionKey).ToList();
        }
		
        private void GenerateTree<T>(
         IEnumerable<T> orginal,
         IEnumerable<T> items,
         bool isFirst = true,
         dynamic grandParentSortID = null)
        {
            var org = orginal.AsQueryable();
            var collection = items.AsQueryable();
            if (isFirst)
            {
                var conditional = collection.Where("c => c.ParentID == null");
                if (conditional.Any())
                {
                    foreach (dynamic c in conditional)
                    {
                        c.SortID = c.ID;
                        var childs = collection.Where($"z => z.ParentID == {c.ID}");
                        if (childs.Any())
                            c.IsRoot = true;

                        foreach (dynamic k in childs)
                        {
                            k.SortID = k.ParentID;
                            var grandChilds = collection.Where($"z => z.ParentID == {k.ID}");
                            GenerateTree(org, grandChilds, false, k.SortID + 1);
                        }
                    }
                }
                else
                {
                    foreach (dynamic c in collection)
                    {
                        dynamic parent = org.Where($"z => z.ID == {c.ParentID}").FirstOrDefault();
                        if (parent != null)
                            parent.SortID = parent.SortID is null ? parent.ParentID : parent.SortID;

                        c.SortID = parent is null ? c.ParentID : parent.SortID + 1;
                        var childs = org.Where($"z => z.ParentID == {c.ID}");
                        if (childs.Any())
                            c.IsRoot = true;

                        foreach (dynamic k in childs)
                        {
                            k.SortID = k.SortID is null ? c.SortID : k.SortID;
                            var grandChilds = collection.Where($"z => z.ParentID == {k.ID}");
                            GenerateTree(org, grandChilds, true, k.SortID + 1);
                        }
                    }
                }
            }
            else
                foreach (dynamic c in collection)
                {
                    c.SortID = grandParentSortID;
                    var childs = org.Where($"z => z.ParentID == {c.ID}");
                    foreach (dynamic k in childs)
                    {
                        k.SortID = k.SortID is null ? c.SortID : k.SortID;
                        var grandChilds = collection.Where($"z => z.ParentID == {k.ID}");
                        GenerateTree(org, grandChilds, true, k.SortID + 1);
                    }
                }
        }

        public virtual List<TResult> Get<TResult>()
        {
            var queryable = CheckBranchID<TEntity>();

            return queryable.ProjectTo<TResult>().ToList();
        }

        protected virtual List<TResult> Get<TResult>(Expression<Func<TEntity, bool>> where)
        {
            var queryable = CheckBranchID<TEntity>();

            return queryable
                     .Where(where)
                     .ProjectTo<TResult>()
                     .ToList();
        }

        public virtual List<TResult> GetPaged<TResult>(QueryInfo queryInfo, out int totalRowCount)
        {
            var queryable = CheckBranchID<TEntity>();

            var query = queryable.ProjectTo<TResult>();


            var result = ConvertToPaged<TResult>(query, queryInfo, out int afterPagedTotalRowCount);
            totalRowCount = afterPagedTotalRowCount;
            return result;
        }


        /// <summary>
        /// Gets IQueryable<TResult> and returns a Paged List<TResult>
        /// </summary>
        /// <typeparam name="TResult"></typeparam>
        /// <param name="query"></param>
        /// <param name="queryInfo"></param>
        /// <param name="totalRowCount"></param>
        /// <returns></returns>
        public virtual List<TResult> ConvertToPaged<TResult>(IQueryable<TResult> query, QueryInfo queryInfo, out int totalRowCount)
        {
            query = ParseColumnFilters<TResult>(queryInfo.Filters, query);
            totalRowCount = query.Count();

            if (queryInfo.PageSize == -1)
                queryInfo.PageSize = int.MaxValue;

            query = query.OrderBy(queryInfo.OrderField, queryInfo.OrderSort);

            return query.Skip(queryInfo.PageSkip)
                        .Take(queryInfo.PageSize)
                        .ToList();

        }
        public virtual List<TResult> ConvertToTree<TResult>(IQueryable<TResult> query, QueryInfo queryInfo, out int totalRowCount)
        {
            query = ParseColumnFilters<TResult>(queryInfo.Filters, query);
            totalRowCount = query.Count();

            if (queryInfo.PageSize == -1)
                queryInfo.PageSize = int.MaxValue;

            query = query.OrderBy(queryInfo.OrderField, queryInfo.OrderSort);

            query = query.Skip(queryInfo.PageSkip)
                         .Take(queryInfo.PageSize);
            if (query.Any("c=>c.ParentID != null"))
            {
                GenerateTree(query, query, true);
                query = query.OrderBy("c => c.SortID");
                var order = OrderTree(query);
                var output = order;
                return output.ToList();
            }
            return query.ToList();
        }
		
        // ToDo: Optimization
        private IList<TResult> OrderTree<TResult>(IEnumerable<TResult> list)
        {
            var dcList = (IEnumerable<dynamic>)list;
            var output = new List<TResult>();
            var query = dcList.AsQueryable();
            foreach (var item in query)
            {
                if (!output.AsQueryable().Any($"c => c.ID == {item.ID}"))
                    output.Add(item);

                foreach (var lev1 in dcList.Where(c => c.ParentID == item.ID))
                {
                    if (!output.AsQueryable().Any($"c => c.ID == {lev1.ID}"))
                        output.Add(lev1);

                    foreach (var lev2 in dcList.Where(c => c.ParentID == lev1.ID))
                    {
                        if (!output.AsQueryable().Any($"c => c.ID == {lev2.ID}"))
                            output.Add(lev2);
                    }
                }
            }
            return output;
        }

        /// <summary>
        /// Gets IQueryable<TResult> and returns a TableData<TResult>
        /// </summary>
        /// <typeparam name="TResult"></typeparam>
        /// <param name="query"></param>
        /// <param name="queryInfo"></param>
        /// <returns></returns>
        public virtual TableData<TResult> GetTableData<TResult>(IQueryable<TResult> query, QueryInfo queryInfo)
        {
            var result = ConvertToPaged<TResult>(query, queryInfo, out var totalRowCount);
            return new TableData<TResult>
            {
                Rows = result,
                TotalRowCount = totalRowCount
            };
        }
        public virtual TableData<TResult> GetTree<TResult>(IQueryable<TResult> query, QueryInfo queryInfo)
        {

            var result = ConvertToTree<TResult>(query, queryInfo, out var totalRowCount);
            return new TableData<TResult>
            {
                Rows = result,
                TotalRowCount = totalRowCount
            };
        }

        protected virtual IQueryable<TResult> ParseColumnFilters<TResult>(List<ColumnFilterOption> filters, IQueryable<TResult> query)
        {
            foreach (var filter in filters)
            {
                string field = filter.Key;
                object value = filter.Value;

                switch (filter.FieldType)
                {
                    case Check.Models.Enums.TableColumnType.Date:
                        var date = value.ToString().ToEn();
                        var from = date.Date;
                        var to = date.Date.AddDays(1).AddSeconds(-1);
                        query = query
                            .Where($"{field} >= \"{from}\"")
                            .Where($"{field} <= \"{to}\"");
                        break;
                    case Check.Models.Enums.TableColumnType.Boolean:
                        var v = (bool.TryParse(value?.ToString(), out var b) && b ? "true" : "false");
                        query = query.Where($"{field} == {v}");
                        break;
                    case Check.Models.Enums.TableColumnType.Number:
                        query = query.Where($"{field} == {(long.TryParse(value?.ToString(), out var num) ? num : long.MinValue)}");
                        break;
                    case Check.Models.Enums.TableColumnType.Enum:
                        query = query.Where($"{field} == \"{value}\"");
                        break;
                    default:
                        if (filter.MatchMode == "equals")
                            query = query.Where($"{field} == \"{value}\"");
                        else if (filter.MatchMode == "notEquals")
                            query = query.Where($"{field} != \"{value}\"");

                        //else if(filter.MatchMode == "persianDateEquals")
                        //    query = query.Where($"{field} = \"{value.ToString().ToEn()}\"");
                        else
                        {
                            // old
                            //query = query.Where($"{field} != null && {field}.Contains(\"{value}\")");
                            // new 
                            // اعداد رو انگلیسی کن
                            var valueEn = value.ToString().Fa2En();

                            var toArabic = value.ToString().ToArabicChars();
                            var toPersian = value.ToString().FixPersianChars();

                            // عربی که اعدادش انگلیسیه
                            var toArabicEn = toArabic.Fa2En();
                            // عربی که اعدادش انگلیسیه
                            var toPersianEn = toPersian.Fa2En();

                            if (toPersian.Trim() != toArabic.Trim())
                            {
                                if (value.ToString() == valueEn)
                                {
                                    var lambda = $"({field} != null && {field}.Contains(\"{toPersian}\")) || ({field} != null && {field}.Contains(\"{toArabic}\"))";
                                    query = query.Where(lambda);

                                }
                                else
                                {
                                    var lambda = $"({field} != null && {field}.Contains(\"{toPersian}\")) ||" +
                                        $" ({field} != null && {field}.Contains(\"{toArabic}\")) ||" +
                                        $"({field} != null && {field}.Contains(\"{toPersianEn}\"))||" +
                                        $"({field} != null && {field}.Contains(\"{toArabicEn}\"))";
                                    query = query.Where(lambda);
                                }
                            }
                            else
                            {
                                if (value.ToString() == valueEn)
                                    query = query.Where($"{field} != null && {field}.Contains(\"{value}\")");
                                else
                                {
                                    var lambda = $"({field} != null && {field}.Contains(\"{value}\")) || ({field} != null && {field}.Contains(\"{valueEn}\"))";
                                    query = query.Where(lambda);
                                }

                            }

                        }
                        break;
                }

            }
            return query;
        }

        public virtual List<TitleValue<TKey>> GetTitleValues(string text = "", bool includeChoose = false)
        {
            var query = CheckBranchID<TEntity>();

            var result = query.ToList()
                              .ProjectToTitleValue<TEntity, TKey>();

            if (!String.IsNullOrWhiteSpace(text))
                result = result.Where(item => item.Title.ToLower().Contains(text.ToLower())).ToList();


            if (includeChoose)
                result.Insert(0, new TitleValue<TKey> { Title = "{بدون انتخاب}" });

            return result;
        }

        public virtual int Count()
        {
            var queryable = CheckBranchID<TEntity>();
            return queryable.Count();
        }

        public virtual TEntity GetByID(TKey id)
        {
            return Db.Set<TEntity>().Find(id);
        }

        public virtual TResult GetByID<TResult>(TKey id)
        {
            return Mapper.Map<TEntity, TResult>(GetByID(id));
        }

        public virtual ValidationException Validate<T>(T entity, ValidateType eventType)
        {
            var ex = new ValidationException();
            ex.AddRangeError(CheckValidator.ValidateModel(entity));

            if (ex.HasError)
                throw ex;

            return ex;
        }

        /// <summary>
        /// Only Add entity to DbContext, not save it!
        /// </summary>
        /// <param name="entity"></param>
        public virtual void Add(TEntity entity)
        {
            Validate<TEntity>(entity, ValidateType.Insert);

            if (entity is IBaseEntity baseEntity)
            {
                baseEntity.CreateBy = UserId;
                baseEntity.CreateDate = DateTime.Now;
            }


            Db.Set<TEntity>().Add(entity);
        }

        /// <summary>
        /// Only add DTO to DbContext, not save it!
        /// </summary>
        /// <typeparam name="TDTO"></typeparam>
        /// <param name="dtoEntity"></param>
        /// <returns></returns>
        public virtual void Add<TDTO>(TDTO dtoEntity)
        {
            Validate<TDTO>(dtoEntity, ValidateType.Insert);

            var entity = Mapper.Map<TEntity>(dtoEntity);

            this.Add(entity);
        }

        /// <summary>
        /// Add entity and SaveChanges in DB
        /// </summary>
        /// <param name="entity"></param>
        /// <returns></returns>
        public virtual TEntity Insert(TEntity entity, bool? ignoreSaveBranch = null)
        {
            Add(entity);
            SaveChanges(ignoreSaveBranch);

            return entity;
        }

        /// <summary>
        /// Add DTO and SaveChanges in DB
        /// </summary>
        /// <param name="entity"></param>
        /// <returns></returns>
        public virtual TEntity Insert<TDTO>(TDTO dtoEntity, bool? ignoreSaveBranch = null)
        {
            Validate<TDTO>(dtoEntity, ValidateType.Insert);

            var entity = Mapper.Map<TEntity>(dtoEntity);

            return this.Insert(entity, ignoreSaveBranch);
        }
        /// <summary>
        /// Add entity and SaveChanges in DB
        /// </summary>
        /// <param name="entity"></param>
        /// <returns></returns>
        public virtual TEntity Insert(TEntity entity)
        {
            Add(entity);
            SaveChanges();

            return entity;
        }

        /// <summary>
        /// Add DTO and SaveChanges in DB
        /// </summary>
        /// <param name="entity"></param>
        /// <returns></returns>
        public virtual TEntity Insert<TDTO>(TDTO dtoEntity)
        {
            Validate<TDTO>(dtoEntity, ValidateType.Insert);

            var entity = Mapper.Map<TEntity>(dtoEntity);

            return this.Insert(entity);
        }
        /// <summary>
        /// Only modify entity to DbContext, not save it!
        /// </summary>
        /// <param name="entity"></param>
        public virtual void Modify(TEntity entity)
        {
            Validate<TEntity>(entity, ValidateType.Update);

            var item = GetByID(entity.GetKey<TKey>());
            item = Mapper.Map(entity, item);

            if (item is IBaseEntity baseEntity)
            {
                baseEntity.UpdateBy = UserId;
                baseEntity.UpdateDate = DateTime.Now;
            }

            Db.Entry(item).State = EntityState.Modified;
        }

        /// <summary>
        /// Only modify DTO to DbContext, not save it!
        /// </summary>
        /// <typeparam name="TDTO"></typeparam>
        /// <param name="dtoEntity"></param>
        public void Modify<TDTO>(TDTO dtoEntity)
        {
            Validate<TDTO>(dtoEntity, ValidateType.Update);

            var entity = Mapper.Map<TEntity>(dtoEntity);

            Modify(entity);
        }

        /// <summary>
        /// update entity and SaveChanges in DB
        /// </summary>
        /// <param name="entity"></param>
        public virtual void Update(TEntity entity, bool? ignoreSaveBranch = null)
        {
            Modify(entity);
            SaveChanges(ignoreSaveBranch);
        }

        /// <summary>
        /// update DTO and SaveChanges in DB
        /// </summary>
        /// <typeparam name="TDTO"></typeparam>
        /// <param name="dtoEntity"></param>
        public void Update<TDTO>(TDTO dtoEntity, bool? ignoreSaveBranch = null)
        {
            var entity = Mapper.Map<TEntity>(dtoEntity);

            Update(entity, ignoreSaveBranch);
        }
        /// <summary>
        /// update entity and SaveChanges in DB
        /// </summary>
        /// <param name="entity"></param>
        public virtual void Update(TEntity entity)
        {
            Modify(entity);
            SaveChanges();
        }

        /// <summary>
        /// update DTO and SaveChanges in DB
        /// </summary>
        /// <typeparam name="TDTO"></typeparam>
        /// <param name="dtoEntity"></param>
        public void Update<TDTO>(TDTO dtoEntity)
        {
            var entity = Mapper.Map<TEntity>(dtoEntity);

            Update(entity);
        }

        /// <summary>
        /// only remove entity from DbContext, not save it
        /// </summary>
        /// <param name="id"></param>
        public virtual void Remove(TKey id)
        {
            var entity = Db.Set<TEntity>().Find(id);

            Validate<TEntity>(entity, ValidateType.Delete);

            Db.Set<TEntity>().Remove(entity);
        }

        /// <summary>
        /// remove entity and saveChanges in db
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public virtual TKey Delete(TKey id)
        {
            Remove(id);
            SaveChanges();

            return id;
        }

        /// <summary>
        /// حذف گروهی
        /// </summary>
        /// <param name="ids"></param>
        public long DeleteByIDs(List<TKey> ids)
        {
            long deleted = 0;

            foreach (var id in ids)
                try
                {
                    Delete(id);
                    deleted++;
                }
                catch (Exception)
                {
                    return deleted;
                }

            return deleted;
        }

        /// <summary>
        /// تهیه فایل اکسل
        /// </summary>
        /// <typeparam name="TResult"></typeparam>
        /// <param name="exportDataModel"></param>
        /// <returns>FileName</returns>
        public virtual void ExportToExcel<TResult>(ExportDataModel<TResult> exportDataModel)
        {
            this.ExportToExcelWithFileName<TResult>(exportDataModel);
        }

        public virtual string ExportToExcelWithFileName<TResult>(ExportDataModel<TResult> exportDataModel)
        {
            var tableColumns = exportDataModel.TableColumns;
            var tableDataRows = exportDataModel.TableData.Rows;
            string fileName = exportDataModel.FileName;

            return Utility.ExcelHelper.GenerateExcel(tableColumns, tableDataRows, fileName);         
        }
		
        public virtual IQueryable<TView> GetSelectedViews<TView>(List<TKey> ids)
        {
            var queryable = CheckBranchID<TEntity>();
            var query = queryable.ProjectTo<TView>();
            if (ids != null && ids.Any())
                query = query.Where(q => ids.Any(i => i.Equals(q.GetKey<TKey>())));

            return query;
        }

        public virtual List<TView> GetSelectedViews<TView>(List<TKey> ids, List<TView> query)
        {
            if (ids != null && ids.Any())
                query = query.Where(q => ids.Any(i => i.Equals(q.GetKey<TKey>()))).ToList();

            return query;
        }

        public virtual List<TView> GridPrintData<TView>(GridPrintDTO<TKey> printDTO)
        {
            UpdateQueryInfoForPrint(ref printDTO);
            var dataAfterFilter = GetTableData<TView>(query: GetSelectedViews<TView>(printDTO.Ids), queryInfo: printDTO.QueryInfo).Rows;

            return dataAfterFilter;
        }

        public virtual GridPrintData<TView> GridPrintDataWithVariables<TView>(GridPrintDTO<TKey> printDTO, List<PrintVariableObject> variables)
        {
            UpdateQueryInfoForPrint(ref printDTO);
            var dataAfterFilter = GetTableData<TView>(query: GetSelectedViews<TView>(printDTO.Ids), queryInfo: printDTO.QueryInfo).Rows;

            return new GridPrintData<TView>()
            {
                List = dataAfterFilter,
                Variables = variables
            };
        }

        public virtual void UpdateQueryInfoForPrint(ref GridPrintDTO<TKey> printDTO)
        {
            if (printDTO.QueryInfo.Filters == null)
                printDTO.QueryInfo.Filters = new List<ColumnFilterOption>();

            printDTO.QueryInfo.PageSize = -1;
        }


        public bool IsUnique(string propertyName, object value)
        {
            return false;
        }

        /// <summary>
        /// بررسی یکتا بودن کلید از طریق مقایسه با سایر رکوردهای جدول
        /// </summary>
        /// <typeparam name="TEntity"></typeparam>
        /// <param name="key"></param>
        /// <returns>Throws validation exception if it's not unique</returns>
        public void CheckKeyUnity<T>(string key, string keyName = "کد") where T : KeyEntity
        {
            var queryable = CheckBranchID<T>();
            var isDuplicate = /*Db.Set<T>()*/queryable.Any(d => d.Key.Trim().ToLower() == key.Trim().ToLower());
            if (isDuplicate)
            {
                var ex = new ValidationException();
                ex.AddError(keyName + " وارد شده تکراری است!");
                throw ex;
            }
        }

        public int SaveChanges(bool? ignoreSaveBranch = null)
        {
            return Db.SaveChanges(ignoreSaveBranch is null ? false : ignoreSaveBranch.Value);
        }
        public Task<int> SaveChangesAsync(bool? ignoreSaveBranch = null)
        {
            return Db.SaveChangesAsync(ignoreSaveBranch is null ? false : ignoreSaveBranch.Value);
        }

        public List<TreeviewDTO> GetTreeView<T>(bool isChecked = false, bool isCollapsed = true, bool isDisabled = false, List<long> expandedItems = null) where T : TreeViewEntity<T>
        {
            var queryable = CheckBranchID<T>();
            var query = queryable.ToList();
            return query
                .ToTreeView<T>(isChecked: isChecked, isCollapsed: isCollapsed, isDisabled: isDisabled, expandedItems: expandedItems);
        }

        public void Dispose()
        {
            this.Db.Dispose();
        }

        public bool UserAuthenticated()
        {
            return HttpContextAccessor.HttpContext.User.Identity.IsAuthenticated;
        }

        ~EntityService()
        {
            this.Dispose();
        }

        /// <summary>
        //  GetHistory
        /// </summary>
        /// <param name="recordID"></param>
        /// <param name="fromDate"></param>
        /// <param name="toDate"></param>
        /// <param name="tableName">نام جدول - در صورت عدم مقدار دهی، نام جدول مربوط به سرویس جاری</param>
        /// <returns></returns>

        public TableData<DataHistoryViewModel> GetHistory(QueryInfo queryInfo, string recordID = null, DateTime? fromDate = null, DateTime? toDate = null, string tableName = "")
        {
            tableName = string.IsNullOrWhiteSpace(tableName) ? GetTableName() : tableName;

            var query = (from mainEntityHistory in Db
                         .Set<DataHistory>()
                         .AsNoTracking()
                         .Where(d =>
                            d.TableName.ToLower() == tableName.ToLower() &&
                            (string.IsNullOrEmpty(recordID) || d.RecordID == recordID) &&
                            (!fromDate.HasValue || d.DateTime >= fromDate) &&
                            (!toDate.HasValue || d.DateTime <= toDate))
                         join relatedEntityHistory in Db.Set<DataHistory>().AsNoTracking() on mainEntityHistory.BatchKey equals relatedEntityHistory.BatchKey
                         join us in Db.Set<User>().AsNoTracking() on mainEntityHistory.User equals us.Id into actionUser
                         from user in actionUser.DefaultIfEmpty()
                         orderby mainEntityHistory.ID descending
                         select DataHistoryViewModel.FromDataHistory(relatedEntityHistory, user))
                         .GroupBy(d => d.BatchKey)
                         .Select(g => new DataHistoryViewModel
                         {
                             ID = g.FirstOrDefault().ID,
                             BatchKey = g.FirstOrDefault().BatchKey,
                             DateTime = g.FirstOrDefault().DateTime,
                             Time = g.FirstOrDefault().DateTime.TimeOfDay.ToString().Substring(0, 8),
                             Type = g.FirstOrDefault().Type,
                             UserFullName = g.FirstOrDefault().UserFullName,
                             ValuesAsString = g.SelectMany(s => s.GetValueAsString())

                         }).Where(g => g.ValuesAsString.Any());

            return GetTableData(query, queryInfo);
        }

        /// <summary>
        /// GetDataHistory
        /// </summary>
        /// <param name="queryInfo"></param>
        /// <param name="recordID"></param>
        /// <param name="fromDate"></param>
        /// <param name="toDate"></param>
        /// <param name="tableName"></param>
        /// <returns></returns>
        public TableData<DataHistoryFinalView> GetHistoryFinal(QueryInfo queryInfo, string recordID = null, DateTime? fromDate = null, DateTime? toDate = null, string tableName = "")
        {
            tableName = string.IsNullOrWhiteSpace(tableName) ? GetTableName() : tableName;

            DynamicParameters queryParameters = new DynamicParameters();
            queryParameters.Add("@tableName", tableName);
            queryParameters.Add("@recordID", recordID);
            queryParameters.Add("@fromDate", fromDate);
            queryParameters.Add("@toDate", toDate);
            queryParameters.Add("@operationType", 1);
            queryParameters.Add("@orderBy", queryInfo.OrderField);
            queryParameters.Add("@RecordCount", 0, DbType.Int32, ParameterDirection.Output);
            queryParameters.Add("@orderIsDesc", queryInfo.OrderSort);
            queryParameters.Add("@pageSize", queryInfo.PageSize);
            queryParameters.Add("@rowSkip", queryInfo.PageSkip);
            queryParameters.Add("@CompressData",CheckDbContext.EnableCompressDataHistory);

            var list = SqlMapper.Query<DataHistoryFinalView>(
                cnn: Db.Database.GetDbConnection(),
                sql: "dbo.spGetDataHistory",
                param: queryParameters,
                commandType: CommandType.StoredProcedure);
            var rowCount = queryParameters.Get<int>("@RecordCount");

            return new TableData<DataHistoryFinalView>
            {
                Rows = list.ToList(),
                TotalRowCount = rowCount
            };
        }

        private string GetTableName()
        {
            var customAttributes = typeof(TEntity).GetCustomAttributes(false);

            foreach (var item in customAttributes)
                if (item is TableAttribute && item.GetType().Name == "TableAttribute")
                    return (item as TableAttribute).Name;

            return null;
        }

        private IQueryable<T> CheckBranchID<T>() where T : class
        {
            IQueryable<T> queryable = Db.Set<T>().AsQueryable().AsNoTracking();
            var isBranchId = typeof(TEntity).GetProperty("BranchID") != null;

            if (isBranchId && _branchId > 0)
            {
                var item = Expression.Parameter(typeof(TEntity), "item");
                var property = Expression.Property(item, "BranchID");
                var zero = Expression.Constant((long)0);
                var branchId = Expression.Constant(_branchId);
                var equalZero = Expression.Equal(property, zero);
                var equalBrachId = Expression.Equal(property, branchId);
                var exp = Expression.Or(equalZero, equalBrachId);

                var lambda = Expression.Lambda<Func<TEntity, bool>>(exp, item);

                queryable = queryable.Where(lambda).Cast<T>();
            }
            return queryable;
        }
    }
}
