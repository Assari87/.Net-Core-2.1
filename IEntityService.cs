using System;
using System.Collections.Generic;
using System.Linq;

namespace Check.Core.Services
{
    public interface IEntityService<TDbContext, TEntity, TKey> : IDisposable
    {
        List<TResult> Get<TResult>();
        TEntity GetByID(TKey id);
        TResult GetByID<TResult>(TKey id);
        List<TResult> GetPaged<TResult>(QueryInfo queryInfo, out int totalRowCount);
        List<TitleValue<TKey>> GetTitleValues(string text = "", bool includeChoose = false);

        int Count();

        TEntity Insert(TEntity entity, bool? ignoreSaveBranch = null);
        TEntity Insert<TDTO>(TDTO entity, bool? ignoreSaveBranch = null);
        TEntity Insert(TEntity entity);
        TEntity Insert<TDTO>(TDTO entity);

        void Update(TEntity entity, bool? ignoreSaveBranch = null);
        void Update<TDTO>(TDTO entity, bool? ignoreSaveBranch = null);
        void Update(TEntity entity);
        void Update<TDTO>(TDTO entity);

        TKey Delete(TKey id);
        long DeleteByIDs(List<TKey> ids);

        bool IsUnique(string propertyName, object value);
        void CheckKeyUnity<T>(string key, string keyName = "کد") where T : KeyEntity;
        bool UserAuthenticated();

        List<TreeviewDTO> GetTreeView<T>(bool isChecked = false, bool isCollapsed = true, bool isDisabled = false, List<long> expandedItems = null) where T : TreeViewEntity<T>;

        void ExportToExcel<TResult>(ExportDataModel<TResult> exportDataModel);
        string ExportToExcelWithFileName<TResult>(ExportDataModel<TResult> exportDataModel);

        IQueryable<TView> GetSelectedViews<TView>(List<TKey> ids);
        List<TView> GetSelectedViews<TView>(List<TKey> ids, List<TView> query = null);
        List<TView> GridPrintData<TView>(GridPrintDTO<TKey> printDto);
        GridPrintData<TView> GridPrintDataWithVariables<TView>(GridPrintDTO<TKey> printDTO, List<PrintVariableObject> variables);

        void UpdateQueryInfoForPrint(ref GridPrintDTO<TKey> printDTO);

        TableData<DataHistoryViewModel> GetHistory(QueryInfo queryInfo, string recordID, DateTime? fromDate = null, DateTime? toDate = null, string tabelName = "");
        TableData<DataHistoryFinalView> GetHistoryFinal(QueryInfo queryInfo, string recordID = null, DateTime? fromDate = null, DateTime? toDate = null, string tableName = "");
    }
}
