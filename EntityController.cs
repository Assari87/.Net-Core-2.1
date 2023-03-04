using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Suggestions.WebAPI.Base;
using System;
using System.Collections.Generic;

namespace Check.Core.WebAPI
{

    public abstract class EntityController<TDbContext, TEntity, TKey> : BaseController, IEntityController<TDbContext, TEntity, TKey>
        where TDbContext : DbContext
        where TEntity : IBaseEntity
    {
        public IEntityService<TDbContext, TEntity, TKey> Service { get; set; }
        public ILogger Logger { get; set; }
        public EntityController(
            UserManager<User> userManager,
            IEntityService<TDbContext, TEntity, TKey> service,
            ILogger logger = null)
            : base(userManager)
        {
            this.Service = service;
            this.Logger = logger ?? LoggerProvider.Instance;

        }

        [HttpPost]
        [CheckAuth(PrivilegeType.Read)]
        public virtual ServerResult<TableData<TResult>> GetPaged<TResult>([FromBody]QueryInfo queryInfo)
        {
            var result = new ServerResult<TableData<TResult>>();

            try
            {
                var rows = Service.GetPaged<TResult>(queryInfo, out var totalRowCount);

                result.Data = new TableData<TResult>()
                {
                    Rows = rows,
                    TotalRowCount = totalRowCount
                };

                result.Success = true;
            }
            catch (Exception ex)
            {
                result = ex.ToServerResult<TableData<TResult>>();
            }

            return result;
        }

        [HttpGet]
        [CheckAuth(PrivilegeType.Read)]
        public virtual ServerResult<List<TitleValue<TKey>>> GetTitleValueList(string text = "", bool includeChoose = false)
        {
            var result = new ServerResult<List<TitleValue<TKey>>>();

            try
            {
                result.Data = Service.GetTitleValues(text);

                if (includeChoose)
                    result.Data.Insert(0, new TitleValue<TKey> { Title = "{بدون انتخاب}" });

                result.Success = true;
            }
            catch (Exception ex)
            {
                result = ex.ToServerResult<List<TitleValue<TKey>>>();
            }

            return result;
        }

        [HttpGet]
        [CheckAuth(PrivilegeType.Read)]
        public virtual ServerResult<TResult> GetByID<TResult>(TKey id)
        {
            var result = new ServerResult<TResult>();

            try
            {
                result.Data = Service.GetByID<TResult>(id);
                result.Success = true;
            }
            catch (Exception ex)
            {
                result = ex.ToServerResult<TResult>();
            }

            return result;
        }

        [HttpPost]
        [CheckAuth(PrivilegeType.Create)]
        public virtual ServerResult<TKey> Insert<TDTO>([FromBody]TDTO dtoEntity)
        {
            var result = new ServerResult<TKey>();

            try
            {
                var entity = Service.Insert<TDTO>(dtoEntity);

                result.Data = entity.GetKey<TKey>();
                result.Success = true;
            }
            catch (Exception ex)
            {
                result = ex.ToServerResult<TKey>(logger: Logger);
            }

            return result;
        }

        [HttpPut]
        [CheckAuth(PrivilegeType.Update)]
        public virtual ServerResult<TKey> Update<TDTO>([FromBody]TDTO dtoEntity)
        {
            var result = new ServerResult<TKey>();

            try
            {
                //var entity = Mapper.Map<TDTO, TEntity>(entity);
                //entity.UpdateBy = Guid.Parse("d47e554d-6175-4561-8b29-fb54ccfa9149");
                //entity.UpdateDate = DateTime.Now;

                Service.Update<TDTO>(dtoEntity);

                result.Data = dtoEntity.GetKey<TKey>();
                result.Success = true;
            }
            catch (Exception ex)
            {
                result = ex.ToServerResult<TKey>();
            }

            return result;
        }

        /// <summary>
        /// Get treeview if T class derived from TreeViewEntity<T>
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="isChecked"></param>
        /// <param name="isCollapsed"></param>
        /// <param name="isDisabled"></param>
        /// <returns></returns>
        [NonAction]
        public ServerResult<List<TreeviewDTO>> GetTreeView<T>(bool isChecked = false, bool isCollapsed = true, bool isDisabled = false, List<long> expandedItems = null) where T : TreeViewEntity<T>
        {
            try
            {
                var result = Service.GetTreeView<T>(isChecked, isCollapsed, isDisabled, expandedItems);
                return ServerResult<List<TreeviewDTO>>.SendData(result);
            }
            catch (Exception ex)
            {
                return ex.ToServerResult<List<TreeviewDTO>>();
            }
        }

        [HttpDelete]
        [CheckAuth(PrivilegeType.Delete)]
        public virtual ServerResult<TKey> DeleteByID(TKey id)
        {
            var result = new ServerResult<TKey>();

            try
            {
                result.Data = Service.Delete(id);
                result.Success = true;
            }
            catch (Exception ex)
            {
                result = ex.ToServerResult<TKey>();
            }

            return result;
        }

        [HttpDelete]
        [CheckAuth(PrivilegeType.Delete)]
        public virtual ServerResult<long> DeleteByIDs(List<TKey> ids)
        {
            try
            {
                return Service.DeleteByIDs(ids).ToServerResult();
            }
            catch (Exception ex)
            {
                return ex.ToServerResult<long>();
            }
        }

        [HttpPost]
        [CheckAuth(PrivilegeType.Read)]
        public virtual ServerResult<string> ExportToExcel<TResult>([FromBody] ExportDataModel<TResult> exportDataModel)
        {
            var result = new ServerResult<string>();
            try
            {
                result.Data = Service.ExportToExcelWithFileName<TResult>(exportDataModel);
                result.Success = true;
            }
            catch (Exception ex)
            {
                result = ex.ToServerResult<string>();
            }

            return result;
        }

        [HttpPost]
        [CheckAuth(PrivilegeType.Read)]
        public virtual ServerResult<List<TView>> GridPrintData<TView>(GridPrintDTO<TKey> printModel)
        {
            try
            {
                return Service.GridPrintData<TView>(printModel).ToServerResult();
            }
            catch (Exception ex)
            {
                return ex.ToServerResult<List<TView>>();
            }
        }
    }
}
