using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;

namespace Check.Core.WebAPI
{
    public interface IEntityController<TDbContext, TEntity, TKey>
        where TDbContext : DbContext
    {
        IEntityService<TDbContext, TEntity, TKey> Service { get; set; }

        ServerResult<TableData<TResult>> GetPaged<TResult>(QueryInfo queryInfo);
        ServerResult<List<TitleValue<TKey>>> GetTitleValueList(string text = "", bool includeChoose = false);
        ServerResult<TResult> GetByID<TResult>(TKey id);
        ServerResult<TKey> Insert<TDTO>([FromBody]TDTO entity);
        ServerResult<TKey> Update<TDTO>([FromBody]TDTO entity);
        ServerResult<TKey> DeleteByID(TKey id);
        ServerResult<long> DeleteByIDs(List<TKey> ids);
    }
}
